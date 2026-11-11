using System.Text;
using System.Linq.Expressions;
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
        var seedDemoMedia = services.GetRequiredService<IConfiguration>().GetValue<bool>("CloudflareR2:SeedDemoMedia");
        var assetService = seedDemoMedia ? scope.ServiceProvider.GetService<IArtifactAssetService>() : null;

        var (admin, museumStaff, curator) = await AccountSeeder.SeedAsync(userManager);
        _ = await AccountSeeder.EnsureUserAsync(userManager, "visitor@museum.vn", "Khách tham quan Mẫu", "Visitor123", RoleNames.Visitor);

        // Serialize repeatable local seeds; existing records are never reset.
        await using (var seedTransaction = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(73674753001)");
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
            museum = await GetOrAddAsync(db, db.Museums, x => x.Name == museum.Name, museum);

            // 2. Phân công nhân viên bảo tàng (CD-01)
            StaffAssignment[] assignments = [
                new StaffAssignment
                {
                    UserId = museumStaff.Id,
                    MuseumId = museum.Id,
                    AssignedAt = DateTime.UtcNow
                },
                new StaffAssignment
                {
                    UserId = curator.Id,
                    MuseumId = museum.Id,
                    AssignedAt = DateTime.UtcNow
                }];
            foreach (var assignment in assignments)
                await GetOrAddAsync(db, db.StaffAssignments,
                    x => x.UserId == assignment.UserId && x.MuseumId == museum.Id && x.GalleryId == null && x.ArtifactId == null, assignment);

            // 3. Cấu trúc bảo tàng: Building -> Floor -> Gallery (CD-02)
            var building = new Building { MuseumId = museum.Id, Name = "Tòa nhà Trưng bày Chính" };
            building = await GetOrAddAsync(db, db.Buildings, x => x.MuseumId == museum.Id && x.Name == building.Name, building);

            var floor1 = new Floor { BuildingId = building.Id, LevelNo = 1, Name = "Tầng 1 - Thời kỳ Tiền sử & Sơ sử" };
            var floor2 = new Floor { BuildingId = building.Id, LevelNo = 2, Name = "Tầng 2 - Thời kỳ Phong kiến" };
            floor1 = await GetOrAddAsync(db, db.Floors, x => x.BuildingId == building.Id && x.LevelNo == 1, floor1);
            floor2 = await GetOrAddAsync(db, db.Floors, x => x.BuildingId == building.Id && x.LevelNo == 2, floor2);

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
            gallery1 = await GetOrAddAsync(db, db.Galleries, x => x.FloorId == floor1.Id && x.Name == gallery1.Name, gallery1);
            gallery2 = await GetOrAddAsync(db, db.Galleries, x => x.FloorId == floor2.Id && x.Name == gallery2.Name, gallery2);

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
            topicNational = await GetOrAddAsync(db, db.Topics, x => x.MuseumId == museum.Id && x.Name == topicNational.Name, topicNational);
            topicDongSon = await GetOrAddAsync(db, db.Topics, x => x.MuseumId == museum.Id && x.Name == topicDongSon.Name, topicDongSon);
            topicCeramics = await GetOrAddAsync(db, db.Topics, x => x.MuseumId == museum.Id && x.Name == topicCeramics.Name, topicCeramics);

            // 5. Hiện vật mẫu (Đa dạng hiện vật Free & Premium cho Chatbot và Nhận diện QR/AI Vision)
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
            var artifact3 = new Artifact
            {
                MuseumId = museum.Id,
                GalleryId = gallery1.Id,
                Code = "HV-003",
                Name = "Tượng đồng người cõng nhau thổi khèn",
                QrCode = "SMGS-TUONGDONG003",
                AccessLevel = "free",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var artifact4 = new Artifact
            {
                MuseumId = museum.Id,
                GalleryId = gallery1.Id,
                Code = "HV-004",
                Name = "Đèn đồng hình người quỳ",
                QrCode = "SMGS-DENDONG004",
                AccessLevel = "free",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var artifact5 = new Artifact
            {
                MuseumId = museum.Id,
                GalleryId = gallery2.Id,
                Code = "HV-005",
                Name = "Đầu rồng đất nung thời Lý",
                QrCode = "SMGS-DAURONG005",
                AccessLevel = "free",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var artifact6 = new Artifact
            {
                MuseumId = museum.Id,
                GalleryId = gallery2.Id,
                Code = "HV-006",
                Name = "Thạp gốm hoa nâu thời Trần",
                QrCode = "SMGS-THAPGOM006",
                AccessLevel = "free",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var artifact7 = new Artifact
            {
                MuseumId = museum.Id,
                GalleryId = gallery2.Id,
                Code = "HV-007",
                Name = "Chuông đồng Vân Bản",
                QrCode = "SMGS-CHUONGVANBAN007",
                AccessLevel = "premium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var artifact8 = new Artifact
            {
                MuseumId = museum.Id,
                GalleryId = gallery2.Id,
                Code = "HV-008",
                Name = "Ấn vàng Sắc mệnh chi bảo",
                QrCode = "SMGS-ANVANG008",
                AccessLevel = "premium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            artifact1 = await GetOrAddAsync(db, db.Artifacts, x => x.MuseumId == museum.Id && x.Code == "HV-001", artifact1);
            artifact2 = await GetOrAddAsync(db, db.Artifacts, x => x.MuseumId == museum.Id && x.Code == "HV-002", artifact2);
            artifact3 = await GetOrAddAsync(db, db.Artifacts, x => x.MuseumId == museum.Id && x.Code == "HV-003", artifact3);
            artifact4 = await GetOrAddAsync(db, db.Artifacts, x => x.MuseumId == museum.Id && x.Code == "HV-004", artifact4);
            artifact5 = await GetOrAddAsync(db, db.Artifacts, x => x.MuseumId == museum.Id && x.Code == "HV-005", artifact5);
            artifact6 = await GetOrAddAsync(db, db.Artifacts, x => x.MuseumId == museum.Id && x.Code == "HV-006", artifact6);
            artifact7 = await GetOrAddAsync(db, db.Artifacts, x => x.MuseumId == museum.Id && x.Code == "HV-007", artifact7);
            artifact8 = await GetOrAddAsync(db, db.Artifacts, x => x.MuseumId == museum.Id && x.Code == "HV-008", artifact8);

            // Gán chủ đề cho hiện vật
            ArtifactTopic[] artifactTopics = [
                new ArtifactTopic { ArtifactId = artifact1.Id, TopicId = topicNational.Id },
                new ArtifactTopic { ArtifactId = artifact1.Id, TopicId = topicDongSon.Id },
                new ArtifactTopic { ArtifactId = artifact2.Id, TopicId = topicNational.Id },
                new ArtifactTopic { ArtifactId = artifact2.Id, TopicId = topicDongSon.Id },
                new ArtifactTopic { ArtifactId = artifact3.Id, TopicId = topicDongSon.Id },
                new ArtifactTopic { ArtifactId = artifact4.Id, TopicId = topicNational.Id },
                new ArtifactTopic { ArtifactId = artifact4.Id, TopicId = topicDongSon.Id },
                new ArtifactTopic { ArtifactId = artifact5.Id, TopicId = topicCeramics.Id },
                new ArtifactTopic { ArtifactId = artifact6.Id, TopicId = topicCeramics.Id },
                new ArtifactTopic { ArtifactId = artifact7.Id, TopicId = topicNational.Id },
                new ArtifactTopic { ArtifactId = artifact8.Id, TopicId = topicNational.Id }
            ];
            foreach (var link in artifactTopics)
                await GetOrAddAsync(db, db.ArtifactTopics, x => x.ArtifactId == link.ArtifactId && x.TopicId == link.TopicId, link);

            // 6. Nội dung đa ngôn ngữ & Narration mẫu giàu chi tiết phục vụ Chatbot hỏi đáp (CD-06A)
            var contentVi1 = new ArtifactContent { ArtifactId = artifact1.Id, LanguageCode = "vi" };
            var contentEn1 = new ArtifactContent { ArtifactId = artifact1.Id, LanguageCode = "en" };
            var contentVi2 = new ArtifactContent { ArtifactId = artifact2.Id, LanguageCode = "vi" };
            var contentVi3 = new ArtifactContent { ArtifactId = artifact3.Id, LanguageCode = "vi" };
            var contentVi4 = new ArtifactContent { ArtifactId = artifact4.Id, LanguageCode = "vi" };
            var contentVi5 = new ArtifactContent { ArtifactId = artifact5.Id, LanguageCode = "vi" };
            var contentVi6 = new ArtifactContent { ArtifactId = artifact6.Id, LanguageCode = "vi" };
            var contentVi7 = new ArtifactContent { ArtifactId = artifact7.Id, LanguageCode = "vi" };
            var contentVi8 = new ArtifactContent { ArtifactId = artifact8.Id, LanguageCode = "vi" };
            contentVi1 = await GetOrAddAsync(db, db.ArtifactContents, x => x.ArtifactId == contentVi1.ArtifactId && x.LanguageCode == contentVi1.LanguageCode, contentVi1);
            contentEn1 = await GetOrAddAsync(db, db.ArtifactContents, x => x.ArtifactId == contentEn1.ArtifactId && x.LanguageCode == contentEn1.LanguageCode, contentEn1);
            contentVi2 = await GetOrAddAsync(db, db.ArtifactContents, x => x.ArtifactId == contentVi2.ArtifactId && x.LanguageCode == contentVi2.LanguageCode, contentVi2);
            contentVi3 = await GetOrAddAsync(db, db.ArtifactContents, x => x.ArtifactId == contentVi3.ArtifactId && x.LanguageCode == contentVi3.LanguageCode, contentVi3);
            contentVi4 = await GetOrAddAsync(db, db.ArtifactContents, x => x.ArtifactId == contentVi4.ArtifactId && x.LanguageCode == contentVi4.LanguageCode, contentVi4);
            contentVi5 = await GetOrAddAsync(db, db.ArtifactContents, x => x.ArtifactId == contentVi5.ArtifactId && x.LanguageCode == contentVi5.LanguageCode, contentVi5);
            contentVi6 = await GetOrAddAsync(db, db.ArtifactContents, x => x.ArtifactId == contentVi6.ArtifactId && x.LanguageCode == contentVi6.LanguageCode, contentVi6);
            contentVi7 = await GetOrAddAsync(db, db.ArtifactContents, x => x.ArtifactId == contentVi7.ArtifactId && x.LanguageCode == contentVi7.LanguageCode, contentVi7);
            contentVi8 = await GetOrAddAsync(db, db.ArtifactContents, x => x.ArtifactId == contentVi8.ArtifactId && x.LanguageCode == contentVi8.LanguageCode, contentVi8);

            var now = DateTime.UtcNow;
            ArtifactContentVersion[] contentVersions = [
                new ArtifactContentVersion
                {
                    ArtifactContentId = contentVi1.Id,
                    VersionNo = 1,
                    Title = "Trống đồng Ngọc Lũ - Bảo vật Quốc gia thời kỳ Đông Sơn",
                    Description = "Trống đồng Ngọc Lũ thuộc nền Văn hóa Đông Sơn, có niên đại khoảng 2.500 năm cách ngày nay (thế kỷ VII - VI trước Công nguyên). Hiện vật được chế tác bằng chất liệu đồng thau, có đường kính mặt trống 79 cm, chiều cao 63 cm và nặng 86 kg. Trống được phát hiện vào năm 1893 tại xã Như Trác, huyện Nam Xang (nay thuộc huyện Bình Lục, tỉnh Hà Nam) và được công nhận là Bảo vật Quốc gia đợt 1 năm 2012. Trống gồm ba phần rõ rệt: tang trống phình rộng, thân trống hình trụ thẳng và chân trống loe hình nón cụt, có bốn đôi quai kép trang trí văn bện thừng.",
                    NarrationText = "Xin chào quý khách. Trước mắt quý khách là Trống đồng Ngọc Lũ, biểu tượng đỉnh cao của nghệ thuật đúc đồng thời kỳ Văn hóa Đông Sơn cách đây khoảng 2.500 năm. Chính giữa mặt trống là hình ngôi sao nổi 14 cánh tượng trưng cho mặt trời. Xung quanh ngôi sao là 16 vòng hoa văn đồng tâm, nổi bật với cảnh sinh hoạt của cư dân Việt cổ như giã gạo, múa hóa trang lông chim, đánh trống đồng, đua thuyền, cùng hình đàn hươu và chim Lạc bay ngược chiều kim đồng hồ. Trống đồng Ngọc Lũ được dùng làm nhạc khí trong các nghi lễ cầu mùa, lễ hội cộng đồng và là biểu tượng quyền lực của thủ lĩnh bộ lạc thời Hùng Vương dựng nước.",
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
                    ArtifactContentId = contentEn1.Id,
                    VersionNo = 1,
                    Title = "Ngoc Lu Bronze Drum",
                    Description = "The Ngoc Lu bronze drum is one of the most important and intricately decorated artifacts of the Dong Son culture in Vietnam, dating back approximately 2,500 years (7th - 6th century BCE). Discovered in 1893 in Ha Nam province, it measures 79 cm in diameter, 63 cm in height, and weighs 86 kg.",
                    NarrationText = "Welcome to the exhibit. Before you stands the Ngoc Lu Bronze Drum, a masterpiece of bronze casting from the Dong Son civilization over 2,500 years ago. The tympanum features a 14-pointed central star surrounded by concentric bands depicting ancient Viet rituals, rice pounding, feathered dancers, deer, and Lac birds.",
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
                    ArtifactContentId = contentVi2.Id,
                    VersionNo = 1,
                    Title = "Thạp đồng Đào Thịnh - Bảo vật Quốc gia Văn hóa Đông Sơn",
                    Description = "Thạp đồng Đào Thịnh thuộc nền Văn hóa Đông Sơn, có niên đại khoảng 2.500 - 2.000 năm cách ngày nay (thế kỷ V - III trước Công nguyên). Hiện vật làm bằng đồng thau, có kích thước lớn nhất trong các thạp đồng Đông Sơn từng được tìm thấy với chiều cao 98 cm, đường kính miệng 61 cm, đường kính đáy 60 cm và nặng 74 kg. Thạp được người dân phát hiện vào ngày 14 tháng 9 năm 1961 bên bờ sông Hồng thuộc xã Đào Thịnh, huyện Trấn Yên, tỉnh Yên Bái. Hiện vật được công nhận là Bảo vật Quốc gia đợt 1 năm 2012.",
                    NarrationText = "Xin mời quý khách chiêm ngưỡng Thạp đồng Đào Thịnh. Điểm độc đáo nhất của chiếc thạp nằm ở phần nắp: chính giữa là ngôi sao 12 cánh, xung quanh rìa nắp đúc nổi bốn cặp tượng nam nữ trong tư thế giao phối, thể hiện tín ngưỡng phồn thực cầu mong sự sinh sôi nảy nở của cư dân nông nghiệp trồng lúa nước. Trên thân thạp khắc họa 6 chiếc thuyền nối đuôi nhau cùng hình chim Lạc và vũ công hóa trang lông chim. Thạp ban đầu dùng để đựng lương thực, thóc giống hoặc rượu trong nghi lễ, và khi phát hiện có chứa di cốt cùng một thạp nhỏ bên trong, cho thấy nó còn được dùng làm quan tài táng người quyền quý.",
                    IsAiGenerated = false,
                    Status = ContentStatuses.Published,
                    IsCurrent = true,
                    CreatedBy = museumStaff.Id,
                    ReviewedBy = curator.Id,
                    ReviewedAt = now,
                    PublishedAt = now,
                    ReviewNote = "Đã duyệt nội dung Thạp đồng Đào Thịnh.",
                    CreatedAt = now
                },
                new ArtifactContentVersion
                {
                    ArtifactContentId = contentVi2.Id,
                    VersionNo = 2,
                    Title = "Thạp đồng Đào Thịnh (Bản chờ duyệt bổ sung)",
                    Description = "Bản cập nhật bổ sung thông tin bảo quản Thạp đồng Đào Thịnh.",
                    NarrationText = "Bản nháp thuyết minh bổ sung đang chờ Curator phê duyệt.",
                    IsAiGenerated = false,
                    Status = ContentStatuses.PendingReview,
                    IsCurrent = false,
                    CreatedBy = museumStaff.Id,
                    CreatedAt = now
                },
                new ArtifactContentVersion
                {
                    ArtifactContentId = contentVi3.Id,
                    VersionNo = 1,
                    Title = "Tượng đồng người cõng nhau thổi khèn",
                    Description = "Tượng đồng người cõng nhau thổi khèn là hiện vật điêu khắc tròn thuộc nền Văn hóa Đông Sơn, có niên đại khoảng 2.500 - 2.000 năm cách ngày nay. Tượng được chế tác bằng đồng thau với chiều cao khoảng 9,5 cm, được tìm thấy tại di chỉ khảo cổ Đông Sơn (tỉnh Thanh Hóa). Hiện vật mô tả sinh động hình ảnh hai người đàn ông mặc khố, đầu vấn khăn: người dưới khỏe mạnh cõng người trên lưng và bước đi nhịp nhàng theo điệu múa, trong khi người ngồi trên ngả lưng ra sau và hai tay nâng cây khèn bầu lên miệng thổi.",
                    NarrationText = "Tượng đồng người cõng nhau thổi khèn là minh chứng sống động cho đời sống tinh thần và nghệ thuật âm nhạc phong phú của người Việt cổ thời Đông Sơn. Cây khèn bầu trên tay nhân vật có cấu tạo giống với khèn của nhiều dân tộc vùng núi phía Bắc và Tây Nguyên ngày nay. Tác phẩm thường được gắn làm phần trang trí trên cán dao găm hoặc đồ dùng nghi lễ của tầng lớp quý tộc, thể hiện không khí lễ hội vui tươi sau mùa thu hoạch.",
                    IsAiGenerated = false,
                    Status = ContentStatuses.Published,
                    IsCurrent = true,
                    CreatedBy = museumStaff.Id,
                    ReviewedBy = curator.Id,
                    ReviewedAt = now,
                    PublishedAt = now,
                    ReviewNote = "Đã duyệt.",
                    CreatedAt = now
                },
                new ArtifactContentVersion
                {
                    ArtifactContentId = contentVi4.Id,
                    VersionNo = 1,
                    Title = "Đèn đồng hình người quỳ - Bảo vật Quốc gia Lạch Trường",
                    Description = "Đèn đồng hình người quỳ (còn gọi là cây đèn hình người quỳ Lạch Trường) có niên đại khoảng 2.000 - 1.700 năm cách ngày nay (thế kỷ I - III sau Công nguyên), thuộc giai đoạn muộn của Văn hóa Đông Sơn tiếp xúc văn hóa Hán. Đèn được làm bằng chất liệu đồng thau, cao 40 cm, rộng 27 cm, nặng khoảng 1,8 kg. Hiện vật được nhà khảo cổ Olov Janse phát hiện năm 1935 trong ngôi mộ gạch số 3 tại Lạch Trường, huyện Hoằng Hóa, tỉnh Thanh Hóa và được công nhận là Bảo vật Quốc gia năm 2012.",
                    NarrationText = "Trước mắt quý khách là cây Đèn đồng hình người quỳ nổi tiếng. Phần thân đèn là tượng một người đàn ông cởi trần, đóng khố, quỳ hai gối trên đế vuông, trên lưng và hai vai có gắn ba nhánh đèn hình chữ S đỡ các đĩa đựng dầu thắp sáng. Trên các nhánh đèn có đúc nổi hình 10 nhạc công đang thổi sáo và 4 con linh thú có cánh. Hiện vật dùng để thắp sáng trong không gian nghi lễ và tùy táng trong mộ quý tộc, thể hiện kỹ thuật đúc đồng ghép khối vô cùng tinh xảo.",
                    IsAiGenerated = false,
                    Status = ContentStatuses.Published,
                    IsCurrent = true,
                    CreatedBy = museumStaff.Id,
                    ReviewedBy = curator.Id,
                    ReviewedAt = now,
                    PublishedAt = now,
                    ReviewNote = "Đã duyệt.",
                    CreatedAt = now
                },
                new ArtifactContentVersion
                {
                    ArtifactContentId = contentVi5.Id,
                    VersionNo = 1,
                    Title = "Đầu rồng đất nung thời Lý - Kiến trúc Hoàng thành Thăng Long",
                    Description = "Đầu rồng đất nung thời Lý có niên đại thế kỷ XI - XII (triều đại nhà Lý, từ năm 1009 đến năm 1225). Hiện vật được chế tác bằng chất liệu đất nung màu đỏ gạch độ bền cao, chiều cao khoảng 55 cm, chiều rộng 38 cm, được tìm thấy tại khu di tích Hoàng thành Thăng Long (Hà Nội). Đầu rồng là cấu kiện trang trí kiến trúc gắn trên bờ nóc và góc mái của các cung điện, chùa tháp thời Lý.",
                    NarrationText = "Đầu rồng đất nung thời Lý mang đầy đủ những nét đặc trưng nhất của hình tượng rồng thời Lý: thân uốn lượn mềm mại, đầu ngẩng cao, bờm bay ngược ra sau như những làn sóng, miệng há rộng ngậm viên ngọc quý (minh châu), mào lửa vươn cao trên mũi và trán có khắc chữ Vương. Khác với vẻ dữ tợn của rồng phong kiến phương Bắc, rồng thời Lý có nét thanh thoát, hiền hòa, gắn liền với tín ngưỡng cầu mưa của cư dân nông nghiệp và tinh thần từ bi của Phật giáo thời Lý.",
                    IsAiGenerated = false,
                    Status = ContentStatuses.Published,
                    IsCurrent = true,
                    CreatedBy = museumStaff.Id,
                    ReviewedBy = curator.Id,
                    ReviewedAt = now,
                    PublishedAt = now,
                    ReviewNote = "Đã duyệt.",
                    CreatedAt = now
                },
                new ArtifactContentVersion
                {
                    ArtifactContentId = contentVi6.Id,
                    VersionNo = 1,
                    Title = "Thạp gốm hoa nâu thời Trần",
                    Description = "Thạp gốm hoa nâu thời Trần có niên đại thế kỷ XIII - XIV (triều đại nhà Trần, từ năm 1225 đến năm 1400). Hiện vật được chế tác bằng gốm sành phủ men trắng ngà và vẽ hoa văn màu nâu sắt (oxit sắt), chiều cao 64 cm, đường kính miệng 34 cm, được phát hiện tại khu vực Nam Định - quê hương của vương triều Trần. Gốm hoa nâu là dòng gốm độc đáo chỉ có ở Việt Nam vào thời Lý - Trần.",
                    NarrationText = "Thạp gốm hoa nâu thời Trần có dáng hình trụ khỏe khoắn, vai phình nhẹ với 6 tai gắn ngang trên vai. Nghệ nhân thời Trần đã dùng kỹ thuật khắc chìm đường viền rồi tô màu nâu sắt lên nền men trắng trước khi nung ở nhiệt độ khoảng 1.200 độ C. Hoa văn trên thân thạp thể hiện hình các chiến binh cầm giáo và khiên tập võ nghệ xen kẽ họa tiết hoa sen dây cách điệu, phản ánh hào khí Đông A thượng võ của dân tộc Đại Việt trong ba lần kháng chiến chống quân Nguyên Mông. Thạp được dùng để đựng nước sạch, rượu quý hoặc vật phẩm trong cung đình và chùa chiền.",
                    IsAiGenerated = false,
                    Status = ContentStatuses.Published,
                    IsCurrent = true,
                    CreatedBy = museumStaff.Id,
                    ReviewedBy = curator.Id,
                    ReviewedAt = now,
                    PublishedAt = now,
                    ReviewNote = "Đã duyệt.",
                    CreatedAt = now
                },
                new ArtifactContentVersion
                {
                    ArtifactContentId = contentVi7.Id,
                    VersionNo = 1,
                    Title = "Chuông đồng Vân Bản - Bảo vật Quốc gia thời Trần",
                    Description = "Chuông đồng Vân Bản (còn gọi là chuông chùa Vân Bản) có niên đại từ thời nhà Trần (thế kỷ XIII - XIV). Chuông được đúc bằng đồng thau, cao 125 cm, đường kính miệng 80 cm và nặng khoảng 300 kg. Hiện vật được ngư dân phát hiện năm 1958 dưới bãi biển Đồ Sơn (thành phố Hải Phòng), nơi xưa kia từng có tháp Tường Long và chùa Vân Bản, và được công nhận là Bảo vật Quốc gia năm 2013.",
                    NarrationText = "Đây là nội dung thuyết minh chuyên sâu (Premium) về Chuông đồng Vân Bản. Quai chuông được tạo hình đôi rồng đấu lưng vào nhau (lưỡng long) với thân mập khỏe, chân có bốn móng sắc nhọn đặc trưng mỹ thuật thời Trần. Trên thân chuông chia làm bốn ô lớn, khắc bài minh văn chữ Hán và các băng hoa văn cánh sen kép tinh xảo. Chuông Vân Bản không chỉ là pháp khí Phật giáo quan trọng của vùng biển Đông Bắc thời Trần mà còn là tư liệu quý giá về nghệ thuật đúc đồng và lịch sử Phật giáo Trúc Lâm Yên Tử.",
                    IsAiGenerated = false,
                    Status = ContentStatuses.Published,
                    IsCurrent = true,
                    CreatedBy = museumStaff.Id,
                    ReviewedBy = curator.Id,
                    ReviewedAt = now,
                    PublishedAt = now,
                    ReviewNote = "Đã duyệt nội dung Premium.",
                    CreatedAt = now
                },
                new ArtifactContentVersion
                {
                    ArtifactContentId = contentVi8.Id,
                    VersionNo = 1,
                    Title = "Ấn vàng Sắc mệnh chi bảo - Bảo vật Quốc gia triều Nguyễn",
                    Description = "Ấn vàng Sắc mệnh chi bảo được đúc vào tháng 10 năm Minh Mạng thứ 8 (năm 1827) dưới triều đại nhà Nguyễn. Ấn được chế tác bằng vàng ròng nguyên chất (vàng 10 tuổi), có chiều cao 11 cm, mặt ấn hình vuông kích thước 14 cm x 14 cm, dày 2,5 cm và nặng 8,3 kg (tương đương khoảng 221 lượng vàng), trở thành chiếc kim bảo lớn nhất và nặng nhất của vương triều Nguyễn. Hiện vật được công nhận là Bảo vật Quốc gia năm 2015.",
                    NarrationText = "Chào mừng quý khách đến với nội dung thuyết minh chuyên sâu (Premium) về Ấn vàng Sắc mệnh chi bảo. Quai ấn được đúc thành hình rồng uốn khúc năm móng đầu ngẩng cao uy nghiêm, đuôi xòe 9 dải lửa. Mặt dưới của ấn khắc nổi bốn chữ Hán 'Sắc mệnh chi bảo', còn mặt lưng khắc năm đúc 1827 cùng trọng lượng của ấn. Chiếc bảo ấn này được các hoàng đế triều Nguyễn sử dụng để đóng trên các đạo sắc phong thần, phong tước cho các bậc công thần, tướng lĩnh văn võ và ban cấp sắc mệnh trọng đại của quốc gia.",
                    IsAiGenerated = false,
                    Status = ContentStatuses.Published,
                    IsCurrent = true,
                    CreatedBy = museumStaff.Id,
                    ReviewedBy = curator.Id,
                    ReviewedAt = now,
                    PublishedAt = now,
                    ReviewNote = "Đã duyệt nội dung Premium.",
                    CreatedAt = now
                }
            ];
            foreach (var version in contentVersions)
            {
                if (await db.ArtifactContentVersions.AnyAsync(x => x.ArtifactContentId == version.ArtifactContentId && x.VersionNo == version.VersionNo)) continue;
                if (version.IsCurrent && await db.ArtifactContentVersions.AnyAsync(x => x.ArtifactContentId == version.ArtifactContentId && x.IsCurrent))
                    version.IsCurrent = false;
                db.ArtifactContentVersions.Add(version);
                await db.SaveChangesAsync();
            }

            // 6b. Seed sẵn các Tour & TourVersion mẫu cho WF-02 (F04, F05, F06) phục vụ Demo DS-02a / DS-02b
            var tourDongSon = new Tour
            {
                MuseumId = museum.Id,
                CreatedBy = museumStaff.Id,
                Title = "Tinh hoa Văn hóa Đông Sơn",
                Description = "Khám phá các Bảo vật Quốc gia thời đại đồ đồng Đông Sơn.",
                EstimatedMinutes = 45,
                Status = "published",
                CreatedAt = now,
                Items =
                [
                    new TourItem { ArtifactId = artifact1.Id, OrderNo = 1 },
                    new TourItem { ArtifactId = artifact2.Id, OrderNo = 2 },
                    new TourItem { ArtifactId = artifact3.Id, OrderNo = 3 }
                ],
                Versions =
                [
                    new TourVersion
                    {
                        VersionNo = 1,
                        Title = "Tinh hoa Văn hóa Đông Sơn (v1)",
                        Description = "Phiên bản v1 gồm 3 hiện vật tiêu biểu thời Đông Sơn.",
                        EstimatedMinutes = 45,
                        Status = TourVersionStatuses.Approved,
                        PublicAt = now.AddHours(-1),
                        CreatedBy = museumStaff.Id,
                        CreatedAt = now.AddHours(-2),
                        SubmittedBy = museumStaff.Id,
                        SubmittedAt = now.AddHours(-1).AddMinutes(-30),
                        ReviewedBy = curator.Id,
                        ReviewedAt = now.AddHours(-1),
                        ReviewNote = "Đã duyệt v1 công bố cho khách tham quan.",
                        Items =
                        [
                            new TourVersionItem { ArtifactId = artifact1.Id, OrderNo = 1 },
                            new TourVersionItem { ArtifactId = artifact2.Id, OrderNo = 2 },
                            new TourVersionItem { ArtifactId = artifact3.Id, OrderNo = 3 }
                        ]
                    },
                    new TourVersion
                    {
                        VersionNo = 2,
                        Title = "Tinh hoa Văn hóa Đông Sơn (v2 - Chờ duyệt)",
                        Description = "Phiên bản v2 mở rộng thêm Đèn đồng hình người quỳ (4 điểm dừng).",
                        EstimatedMinutes = 60,
                        Status = TourVersionStatuses.Submitted,
                        CreatedBy = museumStaff.Id,
                        CreatedAt = now,
                        SubmittedBy = museumStaff.Id,
                        SubmittedAt = now,
                        Items =
                        [
                            new TourVersionItem { ArtifactId = artifact1.Id, OrderNo = 1 },
                            new TourVersionItem { ArtifactId = artifact2.Id, OrderNo = 2 },
                            new TourVersionItem { ArtifactId = artifact3.Id, OrderNo = 3 },
                            new TourVersionItem { ArtifactId = artifact4.Id, OrderNo = 4 }
                        ]
                    }
                ]
            };

            var tourCeramics = new Tour
            {
                MuseumId = museum.Id,
                CreatedBy = museumStaff.Id,
                Title = "Gốm cổ & Kiến trúc Lý - Trần",
                Description = "Hành trình ngắn thưởng lãm gốm hoa nâu và kiến trúc Hoàng thành.",
                EstimatedMinutes = 30,
                Status = "published",
                CreatedAt = now,
                Items =
                [
                    new TourItem { ArtifactId = artifact5.Id, OrderNo = 1 },
                    new TourItem { ArtifactId = artifact6.Id, OrderNo = 2 }
                ],
                Versions =
                [
                    new TourVersion
                    {
                        VersionNo = 1,
                        Title = "Gốm cổ & Kiến trúc Lý - Trần (v1)",
                        Description = "Hành trình 30 phút khám phá nghệ thuật đất nung và gốm hoa nâu.",
                        EstimatedMinutes = 30,
                        Status = TourVersionStatuses.Approved,
                        PublicAt = now.AddHours(-1),
                        CreatedBy = museumStaff.Id,
                        CreatedAt = now.AddHours(-2),
                        SubmittedBy = museumStaff.Id,
                        SubmittedAt = now.AddHours(-1).AddMinutes(-15),
                        ReviewedBy = curator.Id,
                        ReviewedAt = now.AddHours(-1),
                        ReviewNote = "Đã duyệt tour chuyên đề Gốm sứ.",
                        Items =
                        [
                            new TourVersionItem { ArtifactId = artifact5.Id, OrderNo = 1 },
                            new TourVersionItem { ArtifactId = artifact6.Id, OrderNo = 2 }
                        ]
                    }
                ]
            };

            await GetOrAddAsync(db, db.Tours, x => x.MuseumId == museum.Id && x.Title == tourDongSon.Title, tourDongSon);
            await GetOrAddAsync(db, db.Tours, x => x.MuseumId == museum.Id && x.Title == tourCeramics.Title, tourCeramics);
            await seedTransaction.CommitAsync();
        }

        // 7. Seed sẵn mô hình 3D thật (.glb) và file âm thanh thật (.wav) cho Hiện vật 1 nếu chưa có
        var firstArtifact = await db.Artifacts.OrderBy(a => a.Id).FirstOrDefaultAsync();
        if (firstArtifact is not null && assetService is not null && seedDemoMedia)
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

    private static async Task<T> GetOrAddAsync<T>(ApplicationDbContext db, DbSet<T> table, Expression<Func<T, bool>> match, T seed)
        where T : class
    {
        var existing = await table.FirstOrDefaultAsync(match);
        if (existing is not null) return existing;
        table.Add(seed);
        await db.SaveChangesAsync();
        return seed;
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
