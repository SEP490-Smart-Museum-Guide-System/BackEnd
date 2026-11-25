using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Guide;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Identity;
using SMGS.Infrastructure.Services;

internal static class GuideAndDashboardDbChecks
{
    public static async Task RunAsync()
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("FAIL: " + message);
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("smgs-guide-dashboard-db-" + Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new ApplicationDbContext(options);

        var roles = DataSeeder.CreateRoleSeeds();
        db.Roles.AddRange(roles);

        var adminId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var museumStaff1Id = Guid.NewGuid();
        var museumStaff2Id = Guid.NewGuid();
        var visitorId = Guid.NewGuid();

        db.Users.AddRange(
            new ApplicationUser { Id = adminId, UserName = "admin@smgs.vn", Email = "admin@smgs.vn", FullName = "Admin", IsActive = true },
            new ApplicationUser { Id = staffId, UserName = "staff@smgs.vn", Email = "staff@smgs.vn", FullName = "System Staff", IsActive = true },
            new ApplicationUser { Id = museumStaff1Id, UserName = "ms1@smgs.vn", Email = "ms1@smgs.vn", FullName = "Museum Staff 1", IsActive = true },
            new ApplicationUser { Id = museumStaff2Id, UserName = "ms2@smgs.vn", Email = "ms2@smgs.vn", FullName = "Museum Staff 2", IsActive = true },
            new ApplicationUser { Id = visitorId, UserName = "visitor@smgs.vn", Email = "visitor@smgs.vn", FullName = "Visitor", IsActive = true });

        db.UserRoles.AddRange(
            new IdentityUserRole<Guid> { UserId = adminId, RoleId = roles.Single(r => r.Name == RoleNames.Admin).Id },
            new IdentityUserRole<Guid> { UserId = staffId, RoleId = roles.Single(r => r.Name == RoleNames.Staff).Id },
            new IdentityUserRole<Guid> { UserId = museumStaff1Id, RoleId = roles.Single(r => r.Name == RoleNames.MuseumStaff).Id },
            new IdentityUserRole<Guid> { UserId = museumStaff2Id, RoleId = roles.Single(r => r.Name == RoleNames.MuseumStaff).Id },
            new IdentityUserRole<Guid> { UserId = visitorId, RoleId = roles.Single(r => r.Name == RoleNames.Visitor).Id });

        var museum1 = new Museum { Id = 1, Name = "Bảo tàng Lịch sử", IsActive = true, CreatedAt = DateTime.UtcNow };
        var museum2 = new Museum { Id = 2, Name = "Bảo tàng Mỹ thuật", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Museums.AddRange(museum1, museum2);

        db.StaffAssignments.Add(new StaffAssignment
        {
            Id = 1,
            UserId = museumStaff1Id,
            MuseumId = museum1.Id,
            AssignedAt = DateTime.UtcNow
        });

        var building = new Building { Id = 1, MuseumId = museum1.Id, Name = "Tòa chính" };
        var floor = new Floor { Id = 1, BuildingId = building.Id, LevelNo = 1, Name = "Tầng 1" };
        var gallery = new Gallery { Id = 1, FloorId = floor.Id, Name = "Phòng Đông Sơn" };
        db.Buildings.Add(building);
        db.Floors.Add(floor);
        db.Galleries.Add(gallery);

        var artifact17 = new Artifact { Id = 17, MuseumId = museum1.Id, GalleryId = gallery.Id, Name = "Trống đồng Ngọc Lũ", AccessLevel = "free", IsActive = true, CreatedAt = DateTime.UtcNow };
        var artifact18 = new Artifact { Id = 18, MuseumId = museum1.Id, GalleryId = gallery.Id, Name = "Thạp đồng Đào Thịnh", AccessLevel = "premium", IsActive = true, CreatedAt = DateTime.UtcNow };
        var artifact20 = new Artifact { Id = 20, MuseumId = museum2.Id, GalleryId = gallery.Id, Name = "Tượng Phật Quan Âm", AccessLevel = "free", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Artifacts.AddRange(artifact17, artifact18, artifact20);

        db.Artifact3DModels.Add(new Artifact3DModel
        {
            Id = 1,
            ArtifactId = artifact17.Id,
            Name = "Mô hình 3D Trống đồng",
            FileName = "drum.glb",
            StorageKey = "artifacts/17/drum.glb",
            FileFormat = "glb",
            ContentType = "model/gltf-binary",
            FileSize = 1024,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var historyService = new GuideHistoryService(db);
        var source17 = new GuideSource(91, 2, "Trống đồng Ngọc Lũ", "vi");

        // Seed an expired message (> 24 hours ago) to verify 24h TTL auto-purge
        db.GuideChatMessages.Add(new GuideChatMessage
        {
            UserId = visitorId,
            ArtifactId = 17,
            MuseumId = 1,
            Question = "Câu hỏi từ hôm qua (>24h)",
            Answer = "Câu trả lời đã hết hạn 24h",
            Supported = true,
            LanguageCode = "vi",
            CreatedAt = DateTime.UtcNow.AddHours(-25)
        });
        await db.SaveChangesAsync();

        await historyService.RecordExchangeAsync(visitorId, 17, 1, "Trống đồng có hoa văn gì?", "Hoa văn mặt trời và chim Lạc.", true, source17);
        Check(!await db.GuideChatMessages.AnyAsync(m => m.Question.Contains(">24h")), "Expired >24h chat messages are automatically purged.");
        await historyService.RecordExchangeAsync(visitorId, 17, 1, "Tìm thấy ở đâu?", "Tại Hà Nam.", true, source17);
        await historyService.RecordExchangeAsync(visitorId, 20, 2, "Giá bán bao nhiêu?", "Chưa có thông tin.", false, null);

        var recentTurns = await historyService.GetRecentTurnsAsync(visitorId, 17, 4);
        Check(recentTurns.Count == 4 && recentTurns[0].Role == "user" && recentTurns[1].Role == "assistant", "GetRecentTurns returns chronological pairs.");

        var art17History = await historyService.GetArtifactHistoryAsync(17, visitorId);
        Check(art17History.StatusCode == 200 && art17History.Data?.Count == 2 && art17History.Data[0].ArtifactName == "Trống đồng Ngọc Lũ",
            "GetArtifactHistoryAsync joins artifact and museum names.");

        var convos = await historyService.GetMyConversationsAsync(visitorId);
        Check(convos.StatusCode == 200 && convos.Data?.TotalCount == 2, "GetMyConversationsAsync groups across artifacts.");

        var feed = await historyService.GetMyMessagesFeedAsync(visitorId);
        Check(feed.StatusCode == 200 && feed.Data?.TotalCount == 3, "GetMyMessagesFeedAsync returns full cross-artifact stream.");

        var staffLogs = await historyService.GetLogsAsync(null, null, false, 1, 20, staffId, true);
        Check(staffLogs.StatusCode == 200 && staffLogs.Data?.TotalCount == 1 && staffLogs.Data.Items[0].ArtifactId == 20,
            "System Staff filters unsupported AI answers.");

        var ms1Logs = await historyService.GetLogsAsync(null, null, null, 1, 20, museumStaff1Id, false);
        Check(ms1Logs.StatusCode == 200 && ms1Logs.Data?.TotalCount == 2 && ms1Logs.Data.Items.All(x => x.MuseumId == 1),
            "Assigned Museum Staff sees only their museum chatbot logs.");

        var ms2Logs = await historyService.GetLogsAsync(null, null, null, 1, 20, museumStaff2Id, false);
        Check(ms2Logs.StatusCode == 403, "Unassigned Museum Staff denied chatbot logs.");

        var order1 = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            BuyerId = visitorId,
            MuseumId = museum1.Id,
            ProductId = 1,
            ProductName = "Combo Tham quan & Hướng dẫn số",
            ProductKind = "combo",
            Amount = 150000m,
            Currency = "VND",
            Seats = 2,
            ValidityHours = 24,
            ValidityStartsAt = "visit_date",
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = "paid",
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            PaidAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddHours(22)
        };
        db.PurchaseOrders.Add(order1);
        db.MuseumPasses.Add(new MuseumPass
        {
            Id = Guid.NewGuid(),
            OrderId = order1.Id,
            UserId = visitorId,
            ClaimedAt = DateTime.UtcNow.AddHours(-2)
        });
        db.PaymentAttempts.Add(new PaymentAttempt
        {
            Id = Guid.NewGuid(),
            OrderId = order1.Id,
            Status = "failed",
            FailureReason = "Thẻ hết hạn",
            CreatedAt = DateTime.UtcNow.AddHours(-3),
            CompletedAt = DateTime.UtcNow.AddHours(-3)
        });

        var visit1 = new Visit
        {
            Id = 1,
            VisitorId = visitorId,
            MuseumId = museum1.Id,
            Status = VisitStatus.Completed,
            StartedAt = DateTime.UtcNow.AddHours(-2),
            CompletedAt = DateTime.UtcNow.AddHours(-1)
        };
        db.Visits.Add(visit1);
        db.VisitFeedbacks.Add(new VisitFeedback
        {
            Id = 1,
            VisitId = visit1.Id,
            OverallRating = 5,
            ContentRating = 5,
            TourRating = 4,
            ChatbotRating = 5,
            Comment = "Trải nghiệm rất tuyệt vời!",
            ModerationStatus = FeedbackModerationStatus.Visible,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30)
        });
        db.Set<RecognitionHistory>().AddRange(
            new RecognitionHistory { Id = 1, UserId = visitorId, VisitId = visit1.Id, ArtifactId = 17, Method = "qr", ConfirmedQrScan = true, IsSuccess = true, RecognizedAt = DateTime.UtcNow.AddHours(-2), RequestId = Guid.NewGuid() },
            new RecognitionHistory { Id = 2, UserId = visitorId, VisitId = visit1.Id, ArtifactId = 18, Method = "image", ConfirmedQrScan = false, IsSuccess = true, RecognizedAt = DateTime.UtcNow.AddHours(-1), RequestId = Guid.NewGuid() });
        db.Complaints.Add(new Complaint
        {
            Id = 1,
            VisitorId = visitorId,
            Subject = "Không tải được âm thanh",
            Body = "Audio hiện vật 18 bị chậm",
            Status = ComplaintStatus.New,
            CreatedAt = DateTime.UtcNow.AddMinutes(-20)
        });
        await db.SaveChangesAsync();

        var dashboardService = new DashboardService(db);

        var adminDash = await dashboardService.GetAdminDashboardAsync();
        Check(adminDash.StatusCode == 200
            && adminDash.Data!.Overview.TotalMuseums == 2
            && adminDash.Data.Overview.TotalArtifacts == 3
            && adminDash.Data.Overview.ArtifactsWith3DModel == 1
            && adminDash.Data.Commerce.TotalPaidOrders == 1
            && adminDash.Data.Commerce.ActivePassesCount == 1
            && adminDash.Data.Commerce.RevenueByCurrency.Single().NetRevenue == 150000m
            && adminDash.Data.Engagement.TotalChatbotQuestions == 3
            && adminDash.Data.Engagement.SupportedChatbotAnswers == 2
            && adminDash.Data.Engagement.QrRecognitions == 1
            && adminDash.Data.Engagement.ImageRecognitions == 1
            && adminDash.Data.SupportAndFeedback.TotalComplaints == 1,
            "Admin dashboard aggregates overview, commerce, engagement, AI and support stats accurately.");

        var staffDash = await dashboardService.GetSystemStaffDashboardAsync();
        Check(staffDash.StatusCode == 200
            && staffDash.Data!.Complaints.NewComplaints == 1
            && staffDash.Data.Complaints.UnassignedComplaints == 1
            && staffDash.Data.PaymentReconciliation.FailedPaymentAttemptsCount == 1
            && staffDash.Data.AiAndRecognitionQuality.UnsupportedChatbotAnswers == 1
            && staffDash.Data.AiAndRecognitionQuality.RecentUnsupportedQuestions.Count == 1,
            "System Staff dashboard reports complaint queue, payment failures and unsupported AI questions.");

        var museumDashForbidden = await dashboardService.GetMuseumDashboardAsync(1, museumStaff2Id, false);
        Check(museumDashForbidden.StatusCode == 403, "Unassigned museum staff denied museum dashboard.");

        var museumDashOk = await dashboardService.GetMuseumDashboardAsync(1, museumStaff1Id, false);
        Check(museumDashOk.StatusCode == 200
            && museumDashOk.Data!.MuseumId == 1
            && museumDashOk.Data.Catalog.TotalArtifacts == 2
            && museumDashOk.Data.Catalog.BuildingsCount == 1
            && museumDashOk.Data.VisitsAndEngagement.TotalVisits == 1
            && museumDashOk.Data.VisitsAndEngagement.TopScannedArtifacts.Count == 2
            && museumDashOk.Data.VisitsAndEngagement.TopChattedArtifacts.Single().ArtifactId == 17
            && museumDashOk.Data.Feedback.UnrespondedFeedbacksCount == 1,
            "Assigned museum staff receives complete museum dashboard metrics.");

        var cleared = await historyService.ClearArtifactHistoryAsync(17, visitorId);
        Check(cleared.StatusCode == 200 && cleared.Data == 2, "ClearArtifactHistory removes saved exchanges for artifact.");
        Console.WriteLine("PASS: GuideHistoryService and DashboardService EF InMemory checks.");
    }
}
