using SMGS.Application.Interfaces;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;
using SMGS.Application.DTOs.Account;
using SMGS.Application.DTOs.Artifact;
using SMGS.Application.DTOs.ArtifactContent.Request;
using SMGS.Application.DTOs.IndoorMap;
using SMGS.Application.DTOs.Topic;
using SMGS.Application.Services;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Identity;
using SMGS.Infrastructure.Repositories;
using SMGS.Infrastructure.Services;

internal static class DatabaseChecks
{
    public static async Task RunAsync()
    {
        var api = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../SMGS.API"));
        var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(api, "appsettings.json"))
            .AddJsonFile(Path.Combine(api, "appsettings.Development.json"), true)
            .AddUserSecrets("smgs-api-local-development").AddEnvironmentVariables().Build();
        var connection = Environment.GetEnvironmentVariable("SMGS_TEST_CONNECTION") ?? config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Set SMGS_TEST_CONNECTION for database checks.");

        var schema = "smgs_content_test_" + Guid.NewGuid().ToString("N");
        var tempAssetsDir = Path.Combine(Path.GetTempPath(), schema);
        var builder = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false };
        ApplicationDbContext Open() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(builder.ConnectionString).Options);

        await using var setup = Open();
        var createSchemaSql = $"CREATE SCHEMA \"{schema}\"";
        var dropSchemaSql = $"DROP SCHEMA \"{schema}\" CASCADE";
        await setup.Database.ExecuteSqlRawAsync(createSchemaSql);

        try
        {
            await setup.Database.ExecuteSqlRawAsync(setup.Database.GenerateCreateScript());

            // Build DI container for Identity + Services on the isolated schema
            var testConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = builder.ConnectionString,
                ["JwtSettings:Key"] = "P6Et/OViXllf/D6PSob0NZQRTnrQ7mgj17Jg0AkrkcM=",
                ["JwtSettings:Issuer"] = "SmartMuseumGuide",
                ["JwtSettings:Audience"] = "SmartMuseumGuide.Client",
                ["Osrm:BaseUrl"] = "https://router.project-osrm.org/",
            }).Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment(tempAssetsDir));
            services.AddSingleton<IConfiguration>(testConfig);
            services.AddInfrastructure(testConfig);
            await using var provider = services.BuildServiceProvider();

            await DataSeeder.SeedDevelopmentDataAsync(provider);

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var accountService = new AccountAdminService(
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(), db);
            var indoorService = new IndoorMapService(db);
            var topicService = new TopicService(db);
            using var storageProvider = new ServiceCollection().AddSingleton<IFileStorageService, TestFileStorage>().BuildServiceProvider();
            var assetStore = new ArtifactAssetStore(testConfig, storageProvider);
            var assetService = new ArtifactAssetService(db, assetStore);
            var artifactService = new ArtifactService(
                new ArtifactRepository(db),
                new ArtifactContentRepository(db),
                new QrCodeService());

            var adminUser = await db.Users.SingleAsync(u => u.Email == "admin@museum.vn");
            var curatorUser = await db.Users.SingleAsync(u => u.Email == "curator@museum.vn");
            var museum = await db.Museums.FirstAsync();
            var seededArtifact = await db.Artifacts.FirstAsync(a => a.Code == "HV-001");

            // =========================================================================
            // 1. TEST CD-01: Quản trị tài khoản và phân quyền
            // =========================================================================
            var staffCreate = await accountService.CreateStaffAsync(new CreateStaffAccountRequestDto
            {
                Email = "newstaff@museum.vn",
                FullName = "Nhân viên Test CD01",
                Password = "Password123",
                PhoneNumber = "0912345678",
                Roles = [RoleNames.MuseumStaff]
            });
            Check(staffCreate.StatusCode == 201 && staffCreate.Data is not null, "CD-01: Create staff failed.");
            var staffId = staffCreate.Data!.Id;

            var accountsList = await accountService.GetAccountsAsync("newstaff", RoleNames.MuseumStaff, true, null, 1, 10);
            Check(accountsList.StatusCode == 200 && accountsList.Data?.TotalCount == 1, "CD-01: Filter accounts failed.");

            var updatedAcc = await accountService.UpdateAccountAsync(staffId, new UpdateAccountRequestDto
            {
                FullName = "Nhân viên CD01 Updated",
                Email = "newstaff@museum.vn",
                PhoneNumber = "0999999999"
            });
            Check(updatedAcc.StatusCode == 200 && updatedAcc.Data?.FullName == "Nhân viên CD01 Updated", "CD-01: Update account failed.");

            var disabledAcc = await accountService.SetAccountStatusAsync(staffId, false, adminUser.Id);
            Check(disabledAcc.StatusCode == 200 && disabledAcc.Data is { IsActive: false, IsLockedOut: true }, "CD-01: Disable account failed.");

            var selfDisable = await accountService.SetAccountStatusAsync(adminUser.Id, false, adminUser.Id);
            Check(selfDisable.StatusCode == 400, "CD-01: Self-disable must be blocked.");

            var enabledAcc = await accountService.SetAccountStatusAsync(staffId, true, adminUser.Id);
            Check(enabledAcc.StatusCode == 200 && enabledAcc.Data is { IsActive: true, IsLockedOut: false }, "CD-01: Enable account failed.");

            var roleChanged = await accountService.AssignRolesAsync(staffId, new AssignRolesRequestDto
            {
                Roles = [RoleNames.MuseumStaff, RoleNames.Curator]
            }, adminUser.Id);
            Check(roleChanged.StatusCode == 200 && roleChanged.Data!.Roles.Contains(RoleNames.Curator), "CD-01: Assign roles failed.");

            // Phân công phạm vi: Bảo tàng, Gallery, Hiện vật
            var assignMuseum = await accountService.AssignScopeAsync(staffId, new ScopeAssignmentRequestDto
            {
                MuseumId = museum.Id
            });
            Check(assignMuseum.StatusCode == 201 && assignMuseum.Data?.ScopeLevel == "museum", "CD-01: Assign museum scope failed.");
            Check(await accountService.CanManageMuseumAsync(staffId, false, museum.Id), "CD-01: Museum scope check failed.");

            var revokeRes = await accountService.RevokeScopeAsync(assignMuseum.Data!.Id, staffId);
            Check(revokeRes.StatusCode == 200 && !await accountService.CanManageMuseumAsync(staffId, false, museum.Id), "CD-01: Revoke scope failed.");

            var assignArtifact = await accountService.AssignScopeAsync(staffId, new ScopeAssignmentRequestDto
            {
                MuseumId = museum.Id,
                ArtifactId = seededArtifact.Id
            });
            Check(assignArtifact.StatusCode == 201 && assignArtifact.Data?.ScopeLevel == "artifact", "CD-01: Assign artifact scope failed.");
            Check(await accountService.CanManageArtifactAsync(staffId, false, seededArtifact.Id), "CD-01: Artifact scope check failed.");
            Console.WriteLine("PASS [CD-01]: Danh sách tài khoản, tạo nhân viên, cập nhật, bật/tắt, gán role, phân công & thu hồi phạm vi.");

            // =========================================================================
            // 2. TEST CD-02: Cấu trúc bảo tàng (Building, Floor, Gallery + Ràng buộc xóa)
            // =========================================================================
            var buildingId = await indoorService.AddBuilding(museum.Id, new BuildingRequest("Tòa nhà Phụ B"), adminUser.Id, true);
            var updatedBuilding = await indoorService.UpdateBuilding(museum.Id, buildingId, new BuildingRequest("Tòa nhà Phụ B2"), adminUser.Id, true);
            Check(updatedBuilding.Name == "Tòa nhà Phụ B2", "CD-02: Update building failed.");

            var floorId = await indoorService.AddFloor(museum.Id, new FloorRequest(buildingId, 1, "Tầng 1 B2"), adminUser.Id, true);
            var updatedFloor = await indoorService.UpdateFloor(museum.Id, floorId, new FloorRequest(buildingId, 2, "Tầng 2 B2"), adminUser.Id, true);
            Check(updatedFloor.LevelNo == 2 && updatedFloor.Name == "Tầng 2 B2", "CD-02: Update floor failed.");

            // Chặn xóa Building khi còn Floor
            try
            {
                await indoorService.DeleteBuilding(museum.Id, buildingId, adminUser.Id, true);
                throw new Exception("CD-02: Deleting building with floors must fail.");
            }
            catch (IndoorMapException ex) when (ex.StatusCode == 409) { }

            var galleryId = await indoorService.AddGallery(museum.Id, new GalleryRequest(floorId, "Phòng Chuyên đề 1", "Mô tả 1"), adminUser.Id, true);
            var updatedGallery = await indoorService.UpdateGallery(museum.Id, galleryId, new GalleryRequest(floorId, "Phòng Chuyên đề 1B", "Mô tả mới"), adminUser.Id, true);
            Check(updatedGallery.Name == "Phòng Chuyên đề 1B", "CD-02: Update gallery failed.");

            // Chặn xóa Floor khi còn Gallery
            try
            {
                await indoorService.DeleteFloor(museum.Id, floorId, adminUser.Id, true);
                throw new Exception("CD-02: Deleting floor with galleries must fail.");
            }
            catch (IndoorMapException ex) when (ex.StatusCode == 409) { }

            var structure = await indoorService.Structure(museum.Id, adminUser.Id, true);
            Check(structure.Galleries.Any(g => g.Id == galleryId), "CD-02: Structure must include galleries.");

            // Xóa theo thứ tự Gallery -> Floor -> Building
            await indoorService.DeleteGallery(museum.Id, galleryId, adminUser.Id, true);
            await indoorService.DeleteFloor(museum.Id, floorId, adminUser.Id, true);
            await indoorService.DeleteBuilding(museum.Id, buildingId, adminUser.Id, true);
            Console.WriteLine("PASS [CD-02]: Tạo/sửa/xóa Building, Floor, Gallery & kiểm tra quyền, ràng buộc dữ liệu khi xóa.");

            // =========================================================================
            // 3. TEST CD-03: Chủ đề hiện vật (Quản lý chủ đề, gán/gỡ chủ đề)
            // =========================================================================
            var topicCreated = await topicService.CreateAsync(new CreateTopicRequestDto
            {
                MuseumId = museum.Id,
                Name = "Chủ đề Điêu khắc Cổ",
                Description = "Các tác phẩm điêu khắc cổ đại"
            }, adminUser.Id, true);
            Check(topicCreated.StatusCode == 201 && topicCreated.Data is not null, "CD-03: Create topic failed.");
            var topicId = topicCreated.Data!.Id;

            var topicUpdated = await topicService.UpdateAsync(topicId, new UpdateTopicRequestDto
            {
                Name = "Nghệ thuật Điêu khắc Cổ",
                Description = "Đã cập nhật mô tả",
                IsActive = true
            }, adminUser.Id, true);
            Check(topicUpdated.StatusCode == 200 && topicUpdated.Data?.Name == "Nghệ thuật Điêu khắc Cổ", "CD-03: Update topic failed.");

            var assignedTopics = await topicService.AssignTopicsAsync(seededArtifact.Id, [topicId], staffId, false);
            Check(assignedTopics.StatusCode == 200 && assignedTopics.Data!.Any(t => t.Id == topicId), "CD-03: Assign topic to artifact failed.");

            var filteredByTopic = await artifactService.GetAllAsync(museum.Id, null, null, true, 1, 10, topicId);
            Check(filteredByTopic.Data?.TotalCount == 1 && filteredByTopic.Data.Items[0].Id == seededArtifact.Id, "CD-03: Filter artifacts by topic failed.");

            // Chặn xóa chủ đề khi đang gắn với hiện vật
            var deleteInUse = await topicService.DeleteAsync(topicId, adminUser.Id, true);
            Check(deleteInUse.StatusCode == 409, "CD-03: Deleting topic assigned to artifact must return 409.");

            var removedTopic = await topicService.RemoveTopicFromArtifactAsync(seededArtifact.Id, topicId, staffId, false);
            Check(removedTopic.StatusCode == 200, "CD-03: Remove topic from artifact failed.");

            var deletedTopic = await topicService.DeleteAsync(topicId, adminUser.Id, true);
            Check(deletedTopic.StatusCode == 200, "CD-03: Delete unused topic failed.");
            Console.WriteLine("PASS [CD-03]: Quản lý chủ đề (CRUD), gán/gỡ chủ đề cho hiện vật & lọc hiện vật theo chủ đề.");

            // =========================================================================
            // 4. TEST CD-06A/B & CD-07: Narration, Media, Mô hình 3D & Kết nối Viewer
            // =========================================================================
            // 4a. Upload Media (Audio + Image)
            var mp3Bytes = new byte[] { 0x49, 0x44, 0x33, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var mediaUpload = await assetService.UploadMediaAsync(
                seededArtifact.Id, mp3Bytes, "thuyet-minh-fr.mp3", "Audio tiếng Pháp", "audio", "fr", 1, staffId, false);
            Check(mediaUpload.StatusCode == 201 && mediaUpload.Data is not null, "CD-06B: Upload media failed.");
            var mediaId = mediaUpload.Data!.Id;

            var mediaList = await assetService.GetMediaListAsync(seededArtifact.Id);
            Check(mediaList.StatusCode == 200 && mediaList.Data!.Any(m => m.Id == mediaId), "CD-06B: List media failed.");

            var mediaFile = await assetService.GetMediaFileAsync(seededArtifact.Id, mediaId);
            Check(mediaFile.StatusCode == 200 && mediaFile.Data.Content.CanRead, "CD-06B: Read physical media file failed.");

            await mediaFile.Data.Content.DisposeAsync();
            // 4b. Nhập thủ công Narration đa ngôn ngữ + Duyệt phiên bản
            var contentService = new ArtifactContentService(new ArtifactContentRepository(db), new ArtifactRepository(db));
            var manualFr = await contentService.SubmitAsync(seededArtifact.Id, new SubmitArtifactContentRequestDto
            {
                LanguageCode = "fr",
                Title = "Tambour de bronze de Ngoc Lu",
                Description = "Chef-d'œuvre de la culture de Dong Son.",
                NarrationText = "Bienvenue devant le tambour de bronze de Ngoc Lu.",
                AudioUrl = mediaUpload.Data.FileUrl,
                IsAiGenerated = false
            }, staffId);
            Check(manualFr.StatusCode == 201 && manualFr.Data is { IsAiGenerated: false, Status: ContentStatuses.PendingReview }, "CD-06A: Submit manual narration failed.");

            var approveFr = await contentService.ReviewAsync(manualFr.Data!.Id, new ReviewArtifactContentRequestDto
            {
                Note = "Approved French narration"
            }, curatorUser.Id, true);
            Check(approveFr.StatusCode == 200 && approveFr.Data is { Status: ContentStatuses.Published, IsCurrent: true }, "CD-06A: Approve narration failed.");

            // 4c. Upload & Thay thế Mô hình 3D + Cấu hình Viewer
            var glbBytes = Convert.FromBase64String("Z2xURgIAAAAwAAAAHAAAAEpTT057ImFzc2V0Ijp7InZlcnNpb24iOiIyLjAifX0g");
            var modelUpload = await assetService.UploadOrReplace3DModelAsync(
                seededArtifact.Id, new MemoryStream(glbBytes), glbBytes.Length, "model/gltf-binary", "ngoc-lu-v1.glb", new Update3DModelSettingsRequestDto
                {
                    Name = "Mô hình Trống đồng v1",
                    AutoRotate = true,
                    CameraOrbit = "0deg 75deg 105%",
                    Scale = 1.0m
                }, staffId, false);
            Check(modelUpload.StatusCode == 201 && modelUpload.Data?.FileFormat == "glb", "CD-07: Upload 3D model failed.");

            var gltfBytes = Encoding.UTF8.GetBytes("{\"asset\":{\"version\":\"2.0\"}}");
            var modelReplace = await assetService.UploadOrReplace3DModelAsync(
                seededArtifact.Id, new MemoryStream(glbBytes), glbBytes.Length, "model/gltf-binary", "ngoc-lu-v2.glb", new Update3DModelSettingsRequestDto
                {
                    Name = "Mô hình Trống đồng v2",
                    AutoRotate = false,
                    CameraOrbit = "45deg 60deg 100%",
                    Scale = 1.5m
                }, staffId, false);
            Check(modelReplace.StatusCode == 200 && modelReplace.Data is { FileFormat: "glb", Name: "Mô hình Trống đồng v2", Scale: 1.5m }, "CD-07: Replace 3D model failed.");

            // 4d. Kết nối Viewer với dữ liệu thật & Quét QR
            var viewerData = await assetService.GetViewerDataAsync(seededArtifact.Id);
            Check(viewerData.StatusCode == 200
                && viewerData.Data!.Topics.Count >= 2
                && viewerData.Data.Model3D?.FileFormat == "glb"
                && viewerData.Data.Media.Count == 1
                && viewerData.Data.Contents.Count == 3
                && viewerData.Data.Contents.Any(c => c.LanguageCode == "fr" && c.NarrationText == "Bienvenue devant le tambour de bronze de Ngoc Lu."),
                "CD-07: Viewer data aggregation failed.");

            var scanData = await artifactService.ScanAsync("SMGS-NGOCLU001");
            Check(scanData.StatusCode == 200
                && scanData.Data!.Model3D is not null
                && scanData.Data.Media.Count == 1
                && scanData.Data.Contents.Count == 3,
                "CD-07: QR scan real viewer data failed.");

            // 4e. Xóa Media & Xóa 3D Model
            Check((await assetService.DeleteMediaAsync(seededArtifact.Id, mediaId, staffId, false)).StatusCode == 200, "CD-06B: Delete media failed.");
            Check((await assetService.Delete3DModelAsync(seededArtifact.Id, staffId, false)).StatusCode == 200, "CD-07: Delete 3D model failed.");
            Console.WriteLine("PASS [CD-06A/B & CD-07]: Narration thủ công đa ngôn ngữ, phiên bản & duyệt, upload/list/delete Media, upload/replace/delete 3D & kết nối Viewer dữ liệu thật.");
        }
        finally
        {
            await setup.Database.ExecuteSqlRawAsync(dropSchemaSql);
            if (Directory.Exists(tempAssetsDir)) Directory.Delete(tempAssetsDir, true);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private sealed class FakeHostEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "SMGS.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
