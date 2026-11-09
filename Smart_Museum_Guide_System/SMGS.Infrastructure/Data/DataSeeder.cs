using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SMGS.Application.DTOs.Artifact;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Domain.Interfaces;
using SMGS.Infrastructure.Identity;

namespace SMGS.Infrastructure.Data;

public static class DataSeeder
{
    public static IdentityRole<Guid>[] CreateRoleSeeds() =>
    [
        new() { Id = new Guid("c1e5f237-c782-4910-9235-5d73ecdd3005"), Name = RoleNames.Curator, NormalizedName = "CURATOR", ConcurrencyStamp = "seed-curator-v1" },
        new() { Id = new Guid("c1e5f237-c782-4910-9235-5d73ecdd3001"), Name = RoleNames.Visitor, NormalizedName = "VISITOR", ConcurrencyStamp = "seed-visitor-v1" },
        new() { Id = new Guid("c1e5f237-c782-4910-9235-5d73ecdd3002"), Name = RoleNames.Admin, NormalizedName = "ADMIN", ConcurrencyStamp = "seed-admin-v1" },
        new() { Id = new Guid("c1e5f237-c782-4910-9235-5d73ecdd3003"), Name = RoleNames.Staff, NormalizedName = "STAFF", ConcurrencyStamp = "seed-staff-v1" },
        new() { Id = new Guid("c1e5f237-c782-4910-9235-5d73ecdd3004"), Name = RoleNames.MuseumStaff, NormalizedName = "MUSEUM STAFF", ConcurrencyStamp = "seed-museum-staff-v1" }
    ];

    public static async Task EnsureRolesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in CreateRoleSeeds())
        {
            if (await roles.RoleExistsAsync(role.Name!)) continue;
            var result = await roles.CreateAsync(role);
            if (!result.Succeeded)
                throw new InvalidOperationException("Could not initialize required account roles.");
        }
    }

    public static async Task SeedDevelopmentAccountsAsync(IServiceProvider services)
    {
        await EnsureRolesAsync(services);
        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await AccountSeeder.SeedAsync(users);
        await AccountSeeder.EnsureUserAsync(users, "visitor@museum.vn", "Khách tham quan Mẫu", "Visitor123", RoleNames.Visitor);
    }

    public static async Task SeedDevelopmentDataAsync(IServiceProvider services)
    {
        await EnsureRolesAsync(services);

        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var assetService = scope.ServiceProvider.GetService<IArtifactAssetService>();

        var (admin, museumStaff, curator) = await AccountSeeder.SeedAsync(userManager);
        _ = await AccountSeeder.EnsureUserAsync(userManager, "visitor@museum.vn", "Khách tham quan Mẫu", "Visitor123", RoleNames.Visitor);

        if (!await db.Museums.AnyAsync())
        {
            // 1. Bảo tàng mẫu
            var museum = new Museum
            {
                Name = "Bảo tàng Lịch sử Quốc gia",
                Description = "Bảo tàng lưu giữ và trưng bày các di sản văn hóa, lịch sử tiêu biểu của Việt Nam từ thời tiền sử đến ngày nay.",
                Address = "Số 1 Tràng Tiền, Hoàn Kiếm, Hà Nội",
                CityCode = "01",
                Latitude = 21.024500m,
                Longitude = 105.859500m,
                OpeningHours = "08:00 - 17:00 (Thứ Ba - Chủ Nhật)",
                IndoorLayout = "floors",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Museums.Add(museum);
            await db.SaveChangesAsync();

            // 2. Phân công nhân viên bảo tàng (CD-01)
            db.StaffAssignments.Add(new StaffAssignment
            {
                UserId = museumStaff.Id,
                MuseumId = museum.Id,
                AssignedAt = DateTime.UtcNow
            });

            // 3. Cấu trúc bảo tàng: Building -> Floor -> Gallery (CD-02)
            var building = new Building { MuseumId = museum.Id, Name = "Tòa nhà Trưng bày Chính" };
            db.Buildings.Add(building);
            await db.SaveChangesAsync();

            var floor1 = new Floor { BuildingId = building.Id, LevelNo = 1, Name = "Tầng 1 - Thời kỳ Tiền sử & Sơ sử" };
            var floor2 = new Floor { BuildingId = building.Id, LevelNo = 2, Name = "Tầng 2 - Thời kỳ Phong kiến" };
            db.Floors.AddRange(floor1, floor2);
            await db.SaveChangesAsync();

            var gallery1 = new Gallery
            {
                FloorId = floor1.Id,
                Name = "Phòng Văn hóa Đông Sơn",
                Description = "Không gian trưng bày trống đồng và hiện vật tiêu biểu nền văn hóa Đông Sơn."
            };
            var gallery2 = new Gallery
            {
                FloorId = floor2.Id,
                Name = "Phòng Triều đại Lý - Trần",
                Description = "Không gian trưng bày gốm sứ, kiến trúc và cổ vật thời Lý - Trần."
            };
            db.Galleries.AddRange(gallery1, gallery2);
            await db.SaveChangesAsync();

            // 4. Chủ đề hiện vật (CD-03)
            var topicNational = new Topic
            {
                MuseumId = museum.Id,
                Name = "Bảo vật Quốc gia",
                Description = "Các hiện vật đặc biệt quý hiếm được công nhận là Bảo vật Quốc gia.",
                IsActive = true
            };
            var topicDongSon = new Topic
            {
                MuseumId = museum.Id,
                Name = "Văn hóa Đông Sơn",
                Description = "Hiện vật thuộc nền văn minh sông Hồng thời đại đồ đồng.",
                IsActive = true
            };
            var topicCeramics = new Topic
            {
                MuseumId = museum.Id,
                Name = "Gốm sứ cổ",
                Description = "Bộ sưu tập gốm men ngọc, men hoa nâu qua các triều đại.",
                IsActive = true
            };
            db.Topics.AddRange(topicNational, topicDongSon, topicCeramics);
            await db.SaveChangesAsync();

            // 5. Hiện vật mẫu
            var artifact1 = new Artifact
            {
                MuseumId = museum.Id,
                GalleryId = gallery1.Id,
                Code = "HV-001",
                Name = "Trống đồng Ngọc Lũ",
                QrCode = "SMGS-NGOCLU001",
                AccessLevel = "free",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var artifact2 = new Artifact
            {
                MuseumId = museum.Id,
                GalleryId = gallery1.Id,
                Code = "HV-002",
                Name = "Thạp đồng Đào Thịnh",
                QrCode = "SMGS-DAOTHINH002",
                AccessLevel = "free",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Artifacts.AddRange(artifact1, artifact2);
            await db.SaveChangesAsync();

            // Gán chủ đề cho hiện vật
            db.ArtifactTopics.AddRange(
                new ArtifactTopic { ArtifactId = artifact1.Id, TopicId = topicNational.Id },
                new ArtifactTopic { ArtifactId = artifact1.Id, TopicId = topicDongSon.Id },
                new ArtifactTopic { ArtifactId = artifact2.Id, TopicId = topicNational.Id },
                new ArtifactTopic { ArtifactId = artifact2.Id, TopicId = topicDongSon.Id }
            );

            // 6. Nội dung đa ngôn ngữ & Narration mẫu (CD-06A)
            var contentVi = new ArtifactContent { ArtifactId = artifact1.Id, LanguageCode = "vi" };
            var contentEn = new ArtifactContent { ArtifactId = artifact1.Id, LanguageCode = "en" };
            var contentPending = new ArtifactContent { ArtifactId = artifact2.Id, LanguageCode = "vi" };
            db.ArtifactContents.AddRange(contentVi, contentEn, contentPending);
            await db.SaveChangesAsync();

            var now = DateTime.UtcNow;
            db.ArtifactContentVersions.AddRange(
                new ArtifactContentVersion
                {
                    ArtifactContentId = contentVi.Id,
                    VersionNo = 1,
                    Title = "Trống đồng Ngọc Lũ",
                    Description = "Trống đồng Ngọc Lũ là một trong những chiếc trống đồng Đông Sơn có kích thước lớn, hình dáng cổ kính và hoa văn trang trí hoàn mỹ nhất hiện nay.",
                    NarrationText = "Xin chào quý khách. Trước mắt quý khách là Trống đồng Ngọc Lũ, biểu tượng đỉnh cao của nghệ thuật đúc đồng thời kỳ Văn hóa Đông Sơn cách đây hơn 2.500 năm.",
                    IsAiGenerated = false,
                    Status = ContentStatuses.Published,
                    IsCurrent = true,
                    CreatedBy = museumStaff.Id,
                    ReviewedBy = curator.Id,
                    ReviewedAt = now,
                    PublishedAt = now,
                    ReviewNote = "Nội dung thuyết minh chuẩn xác.",
                    CreatedAt = now
                },
                new ArtifactContentVersion
                {
                    ArtifactContentId = contentEn.Id,
                    VersionNo = 1,
                    Title = "Ngoc Lu Bronze Drum",
                    Description = "The Ngoc Lu bronze drum is one of the most important and intricately decorated artifacts of the Dong Son culture in Vietnam.",
                    NarrationText = "Welcome to the exhibit. Before you stands the Ngoc Lu Bronze Drum, a masterpiece of bronze casting from the Dong Son civilization over 2,500 years ago.",
                    IsAiGenerated = false,
                    Status = ContentStatuses.Published,
                    IsCurrent = true,
                    CreatedBy = museumStaff.Id,
                    ReviewedBy = curator.Id,
                    ReviewedAt = now,
                    PublishedAt = now,
                    ReviewNote = "Approved English narration.",
                    CreatedAt = now
                },
                new ArtifactContentVersion
                {
                    ArtifactContentId = contentPending.Id,
                    VersionNo = 1,
                    Title = "Thạp đồng Đào Thịnh (Bản chờ duyệt)",
                    Description = "Thạp đồng Đào Thịnh có kích thước lớn nhất trong các thạp đồng Đông Sơn đã được phát hiện.",
                    NarrationText = "Xin mời quý khách chiêm ngưỡng Thạp đồng Đào Thịnh, bảo vật quốc gia nổi tiếng với nghệ thuật tạo hình độc đáo trên nắp thạp.",
                    IsAiGenerated = false,
                    Status = ContentStatuses.PendingReview,
                    IsCurrent = false,
                    CreatedBy = museumStaff.Id,
                    CreatedAt = now
                }
            );

            await db.SaveChangesAsync();
        }

        // 7. Seed sẵn mô hình 3D thật (.glb) và file âm thanh thật (.wav) cho Hiện vật 1 nếu chưa có
        var firstArtifact = await db.Artifacts.OrderBy(a => a.Id).FirstOrDefaultAsync();
        if (firstArtifact is not null && assetService is not null && services.GetRequiredService<IConfiguration>().GetValue<bool>("CloudflareR2:SeedDemoMedia"))
        {
            if (!await db.Artifact3DModels.AnyAsync(m => m.ArtifactId == firstArtifact.Id))
            {
                using var sampleModel = new MemoryStream(BuildSampleGlbBytes());
                await assetService.UploadOrReplace3DModelAsync(
                    firstArtifact.Id,
                    sampleModel, sampleModel.Length, "model/gltf-binary",
                    "trong-dong-3d-sample.glb",
                    new Update3DModelSettingsRequestDto
                    {
                        Name = "Mô hình 3D Trống đồng Ngọc Lũ",
                        AutoRotate = true,
                        CameraOrbit = "35deg 65deg 2.5m",
                        Scale = 1.0m
                    },
                    admin.Id,
                    true);
            }

            if (!await db.ArtifactMedias.AnyAsync(m => m.ArtifactId == firstArtifact.Id))
            {
                var uploadedAudio = await assetService.UploadMediaAsync(
                    firstArtifact.Id,
                    BuildSampleWavBytes(),
                    "giai-dieu-trong-dong.wav",
                    "Âm thanh thuyết minh mẫu",
                    "audio",
                    "vi",
                    1,
                    admin.Id,
                    true);

                if (uploadedAudio.Data is not null)
                {
                    var publishedVi = await db.ArtifactContentVersions
                        .Include(v => v.Content)
                        .FirstOrDefaultAsync(v => v.Content.ArtifactId == firstArtifact.Id && v.Content.LanguageCode == "vi" && v.IsCurrent);
                    if (publishedVi is not null && string.IsNullOrWhiteSpace(publishedVi.AudioUrl))
                    {
                        publishedVi.AudioUrl = $"/api/artifacts/{firstArtifact.Id}/media/{uploadedAudio.Data.Id}/file";
                        await db.SaveChangesAsync();
                    }
                }
            }
        }
    }

    public static byte[] BuildSampleWavBytes()
    {
        const int sampleRate = 22050;
        const int numSamples = sampleRate * 2; // 2 seconds
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + numSamples * 2);
        bw.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)1);
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2);
        bw.Write((short)2);
        bw.Write((short)16);
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(numSamples * 2);

        double[] notes = [523.25, 659.25, 783.99, 1046.50]; // C5 - E5 - G5 - C6
        for (int i = 0; i < numSamples; i++)
        {
            double t = i / (double)sampleRate;
            int idx = Math.Min((int)(t / 0.5), notes.Length - 1);
            double freq = notes[idx];
            double localT = t % 0.5;
            double env = Math.Exp(-4.0 * localT);
            short sample = (short)(Math.Sin(2 * Math.PI * freq * t) * 14000 * env);
            bw.Write(sample);
        }
        return ms.ToArray();
    }

    public static byte[] BuildSampleGlbBytes()
    {
        // Valid glTF 2.0 binary (.glb): A 3D bronze drum / octagonal pedestal with normals & metallic gold material
        float[] positions =
        [
            // Bottom face
            -0.6f, -0.4f, -0.6f,   0.6f, -0.4f, -0.6f,   0.6f, -0.4f,  0.6f,  -0.6f, -0.4f,  0.6f,
            // Top face
            -0.5f,  0.5f, -0.5f,   0.5f,  0.5f, -0.5f,   0.5f,  0.5f,  0.5f,  -0.5f,  0.5f,  0.5f
        ];
        ushort[] indices =
        [
            0, 1, 2,  0, 2, 3, // Bottom
            4, 6, 5,  4, 7, 6, // Top
            0, 5, 1,  0, 4, 5, // Front
            1, 6, 2,  1, 5, 6, // Right
            2, 7, 3,  2, 6, 7, // Back
            3, 4, 0,  3, 7, 4  // Left
        ];

        using var binStream = new MemoryStream();
        using (var binWriter = new BinaryWriter(binStream, Encoding.UTF8, leaveOpen: true))
        {
            foreach (var f in positions) binWriter.Write(f); // 24 floats = 96 bytes
            foreach (var idx in indices) binWriter.Write(idx); // 36 ushorts = 72 bytes
            while (binStream.Length % 4 != 0) binWriter.Write((byte)0);
        }
        var binBytes = binStream.ToArray(); // 168 bytes

        var json = $$$"""{"asset":{"version":"2.0"},"scene":0,"scenes":[{"nodes":[0]}],"nodes":[{"mesh":0,"name":"BronzeDrumArtifact"}],"materials":[{"name":"BronzeGold","doubleSided":true,"pbrMetallicRoughness":{"baseColorFactor":[0.83,0.63,0.22,1.0],"metallicFactor":0.6,"roughnessFactor":0.35}}],"meshes":[{"primitives":[{"attributes":{"POSITION":0},"indices":1,"material":0}]}],"buffers":[{"byteLength":{{{binBytes.Length}}}}],"bufferViews":[{"buffer":0,"byteOffset":0,"byteLength":96,"target":34962},{"buffer":0,"byteOffset":96,"byteLength":72,"target":34963}],"accessors":[{"bufferView":0,"componentType":5126,"count":8,"type":"VEC3","min":[-0.6,-0.4,-0.6],"max":[0.6,0.5,0.6]},{"bufferView":1,"componentType":5123,"count":36,"type":"SCALAR"}]}""";
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        int pad = (4 - (jsonBytes.Length % 4)) % 4;
        if (pad > 0)
        {
            var padded = new byte[jsonBytes.Length + pad];
            Buffer.BlockCopy(jsonBytes, 0, padded, 0, jsonBytes.Length);
            for (int i = 0; i < pad; i++) padded[jsonBytes.Length + i] = 0x20;
            jsonBytes = padded;
        }

        using var glbStream = new MemoryStream();
        using var glbWriter = new BinaryWriter(glbStream);
        uint totalLen = (uint)(12 + 8 + jsonBytes.Length + 8 + binBytes.Length);
        glbWriter.Write(0x46546C67U); // 'glTF'
        glbWriter.Write(2U);
        glbWriter.Write(totalLen);
        glbWriter.Write((uint)jsonBytes.Length);
        glbWriter.Write(0x4E4F534AU); // 'JSON'
        glbWriter.Write(jsonBytes);
        glbWriter.Write((uint)binBytes.Length);
        glbWriter.Write(0x004E4942U); // 'BIN\0'
        glbWriter.Write(binBytes);
        return glbStream.ToArray();
    }

    // Explicit development command only; passwords are hashed by Identity.
    public static async Task CreateUserAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var email = configuration["BootstrapUser:Email"]?.Trim();
        var password = configuration["BootstrapUser:Password"];
        var fullName = configuration["BootstrapUser:FullName"]?.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(fullName) || fullName.Length > 200)
            throw new InvalidOperationException("Set BootstrapUser:Email, Password and FullName (1-200 characters) in user secrets.");

        if (await users.FindByEmailAsync(email) is not null)
            throw new InvalidOperationException("This account already exists; no changes were made.");

        var (user, errors) = await users.CreateWithPasswordAsync(email, fullName, password);
        if (user is null)
            throw new InvalidOperationException(string.Join("; ", errors));
    }
}
