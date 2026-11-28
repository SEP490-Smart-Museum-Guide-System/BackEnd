using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Commerce;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Dashboard;
using SMGS.Application.DTOs.Guide;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

public class DashboardService(
    ApplicationDbContext db,
    IAccountAdminService? accountAdmin = null,
    TimeProvider? clock = null) : IDashboardService
{
    private DateTime UtcNow => (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    private static bool ValidateRange(DateTime? fromUtc, DateTime? toUtc, out string? error)
    {
        if (fromUtc.HasValue && toUtc.HasValue && toUtc.Value <= fromUtc.Value)
        {
            error = "Khoảng thời gian thống kê không hợp lệ (to phải lớn hơn from).";
            return false;
        }
        error = null;
        return true;
    }

    private static DateTime? NormalizeUtc(DateTime? value) =>
        value.HasValue
            ? (value.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
                : value.Value.ToUniversalTime())
            : null;

    public async Task<ResponseDto<AdminDashboardDto>> GetAdminDashboardAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var from = NormalizeUtc(fromUtc);
        var to = NormalizeUtc(toUtc);
        if (!ValidateRange(from, to, out var error))
            return ResponseDto<AdminDashboardDto>.Fail(error!, 400);

        var now = UtcNow;

        // 1. Overview
        var totalMuseums = await db.Museums.AsNoTracking().CountAsync(cancellationToken);
        var activeMuseums = await db.Museums.AsNoTracking().CountAsync(m => m.IsActive, cancellationToken);
        var totalArtifacts = await db.Artifacts.AsNoTracking().CountAsync(cancellationToken);
        var activeArtifacts = await db.Artifacts.AsNoTracking().CountAsync(a => a.IsActive, cancellationToken);
        var artifactsWith3D = await db.Artifact3DModels.AsNoTracking().CountAsync(cancellationToken);
        var premiumArtifacts = await db.Artifacts.AsNoTracking().CountAsync(a => a.IsActive && a.AccessLevel == "premium", cancellationToken);
        var totalUsers = await db.Users.AsNoTracking().CountAsync(cancellationToken);
        var activeUsers = await db.Users.AsNoTracking().CountAsync(u => u.IsActive, cancellationToken);

        var roleRows = await (from r in db.Roles.AsNoTracking()
                              join ur in db.UserRoles.AsNoTracking() on r.Id equals ur.RoleId into urj
                              from ur in urj.DefaultIfEmpty()
                              group ur by r.Name into g
                              select new { Role = g.Key ?? "", Count = g.Count(x => x != null) })
                             .OrderBy(x => x.Role)
                             .ToListAsync(cancellationToken);

        var usersByRole = roleRows.Select(r => new RoleUserCountDto(r.Role, r.Count)).ToList();

        var overview = new SystemOverviewStatsDto(
            totalMuseums,
            activeMuseums,
            totalArtifacts,
            activeArtifacts,
            artifactsWith3D,
            premiumArtifacts,
            totalUsers,
            activeUsers,
            usersByRole);

        // 2. Commerce
        var ordersQuery = db.PurchaseOrders.AsNoTracking().AsQueryable();
        if (from.HasValue) ordersQuery = ordersQuery.Where(o => o.CreatedAt >= from.Value);
        if (to.HasValue) ordersQuery = ordersQuery.Where(o => o.CreatedAt < to.Value);

        var paidOrdersList = await ordersQuery.Where(o => o.Status == "paid").ToListAsync(cancellationToken);
        var totalPaidOrders = paidOrdersList.Count;
        var totalPendingOrders = await ordersQuery.CountAsync(o => o.Status == "pending", cancellationToken);
        var totalCancelledOrders = await ordersQuery.CountAsync(o => o.Status == "cancelled", cancellationToken);

        var activePassesCount = await (from p in db.MuseumPasses.AsNoTracking()
                                       join o in db.PurchaseOrders.AsNoTracking() on p.OrderId equals o.Id
                                       where p.RevokedAt > now && o.Status == "paid" && o.CancelledAt == null && o.DigitalCancelledAt == null && o.ExpiresAt > now
                                       select p.Id).CountAsync(cancellationToken);

        var attemptsQuery = from a in db.PaymentAttempts.AsNoTracking()
                            join o in db.PurchaseOrders.AsNoTracking() on a.OrderId equals o.Id
                            where a.Status == "failed"
                            select new { a.CreatedAt, a.CompletedAt, o.Currency, o.MuseumId };
        if (from.HasValue) attemptsQuery = attemptsQuery.Where(a => (a.CompletedAt ?? a.CreatedAt) >= from.Value);
        if (to.HasValue) attemptsQuery = attemptsQuery.Where(a => (a.CompletedAt ?? a.CreatedAt) < to.Value);
        var failedAttemptsList = await attemptsQuery.ToListAsync(cancellationToken);

        var allOrdersForRefunds = await ordersQuery.Where(o => o.RefundedAmount > 0).ToListAsync(cancellationToken);

        var currencies = paidOrdersList.Select(o => o.Currency)
            .Union(allOrdersForRefunds.Select(o => o.Currency))
            .Union(failedAttemptsList.Select(a => a.Currency))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        var revenueByCurrency = currencies.Select(currency =>
        {
            var paidForCurr = paidOrdersList.Where(o => o.Currency == currency).ToList();
            var gross = paidForCurr.Sum(o => o.Amount);
            var refunded = paidForCurr.Sum(o => o.RefundedAmount)
                + allOrdersForRefunds.Where(o => o.Currency == currency && o.Status != "paid").Sum(o => o.RefundedAmount);
            var failed = failedAttemptsList.Count(a => a.Currency == currency);
            return new CurrencyRevenueSummaryDto(currency, paidForCurr.Count, gross, refunded, gross - refunded, failed);
        }).ToList();

        var salesByProductKind = paidOrdersList
            .GroupBy(o => new { o.ProductKind, o.Currency })
            .OrderBy(g => g.Key.ProductKind)
            .ThenBy(g => g.Key.Currency)
            .Select(g => new ProductKindSalesDto(
                g.Key.ProductKind,
                g.Key.Currency,
                g.Count(),
                g.Sum(o => o.Seats),
                g.Sum(o => o.Amount)))
            .ToList();

        var museumNames = await db.Museums.AsNoTracking()
            .ToDictionaryAsync(m => m.Id, m => m.Name, cancellationToken);

        var topMuseumsByRevenue = paidOrdersList
            .GroupBy(o => new { o.MuseumId, o.Currency })
            .Select(g =>
            {
                var gross = g.Sum(o => o.Amount);
                var refunded = g.Sum(o => o.RefundedAmount)
                    + allOrdersForRefunds.Where(o => o.MuseumId == g.Key.MuseumId && o.Currency == g.Key.Currency && o.Status != "paid").Sum(o => o.RefundedAmount);
                return new MuseumRevenueRankDto(
                    g.Key.MuseumId,
                    museumNames.GetValueOrDefault(g.Key.MuseumId, ""),
                    g.Key.Currency,
                    g.Count(),
                    gross,
                    refunded,
                    gross - refunded);
            })
            .OrderByDescending(m => m.NetRevenue)
            .ThenBy(m => m.MuseumId)
            .Take(10)
            .ToList();

        var commerce = new AdminCommerceStatsDto(
            totalPaidOrders,
            totalPendingOrders,
            totalCancelledOrders,
            activePassesCount,
            revenueByCurrency,
            salesByProductKind,
            topMuseumsByRevenue);

        // 3. Engagement
        var visitsQuery = db.Visits.AsNoTracking().AsQueryable();
        if (from.HasValue) visitsQuery = visitsQuery.Where(v => v.StartedAt >= from.Value);
        if (to.HasValue) visitsQuery = visitsQuery.Where(v => v.StartedAt < to.Value);

        var totalVisits = await visitsQuery.CountAsync(cancellationToken);
        var completedVisits = await visitsQuery.CountAsync(v => v.Status == VisitStatus.Completed, cancellationToken);
        var inProgressVisits = await visitsQuery.CountAsync(v => v.Status == VisitStatus.InProgress, cancellationToken);
        var guidedVisits = await visitsQuery.CountAsync(v => v.TourId != null || v.TourVersionId != null || v.PersonalizedTourId != null, cancellationToken);

        var recognitionsQuery = db.Set<RecognitionHistory>().AsNoTracking().AsQueryable();
        if (from.HasValue) recognitionsQuery = recognitionsQuery.Where(r => r.RecognizedAt >= from.Value);
        if (to.HasValue) recognitionsQuery = recognitionsQuery.Where(r => r.RecognizedAt < to.Value);

        var totalRecognitions = await recognitionsQuery.CountAsync(cancellationToken);
        var qrRecognitions = await recognitionsQuery.CountAsync(r => r.Method == "qr", cancellationToken);
        var imageRecognitions = await recognitionsQuery.CountAsync(r => r.Method == "image", cancellationToken);

        var quizAttemptsQuery = db.Set<QuizAttempt>().AsNoTracking().AsQueryable();
        if (from.HasValue) quizAttemptsQuery = quizAttemptsQuery.Where(q => q.StartedAt >= from.Value);
        if (to.HasValue) quizAttemptsQuery = quizAttemptsQuery.Where(q => q.StartedAt < to.Value);

        var totalQuizAttempts = await quizAttemptsQuery.CountAsync(cancellationToken);
        var completedQuizScores = await quizAttemptsQuery
            .Where(q => q.Status == "completed")
            .Select(q => q.Score)
            .ToListAsync(cancellationToken);
        var completedQuizAttempts = completedQuizScores.Count;
        var avgQuizScore = completedQuizAttempts > 0 ? Math.Round(completedQuizScores.Average(), 2) : 0d;

        var chatQuery = db.GuideChatMessages.AsNoTracking().AsQueryable();
        if (from.HasValue) chatQuery = chatQuery.Where(m => m.CreatedAt >= from.Value);
        if (to.HasValue) chatQuery = chatQuery.Where(m => m.CreatedAt < to.Value);

        var totalChat = await chatQuery.CountAsync(cancellationToken);
        var supportedChat = await chatQuery.CountAsync(m => m.Supported, cancellationToken);
        var unsupportedChat = totalChat - supportedChat;
        var supportRate = totalChat > 0 ? Math.Round(100d * supportedChat / totalChat, 2) : 0d;

        var engagement = new AdminEngagementStatsDto(
            totalVisits,
            completedVisits,
            inProgressVisits,
            guidedVisits,
            totalRecognitions,
            qrRecognitions,
            imageRecognitions,
            totalQuizAttempts,
            completedQuizAttempts,
            avgQuizScore,
            totalChat,
            supportedChat,
            unsupportedChat,
            supportRate);

        // 4. Support & Feedback
        var feedbacksQuery = db.VisitFeedbacks.AsNoTracking().AsQueryable();
        if (from.HasValue) feedbacksQuery = feedbacksQuery.Where(f => f.CreatedAt >= from.Value);
        if (to.HasValue) feedbacksQuery = feedbacksQuery.Where(f => f.CreatedAt < to.Value);

        var feedbackRows = await feedbacksQuery
            .Select(f => new { f.OverallRating, f.ContentRating, f.TourRating, f.ChatbotRating })
            .ToListAsync(cancellationToken);

        var totalFeedbacks = feedbackRows.Count;
        var avgOverall = totalFeedbacks > 0 ? Math.Round(feedbackRows.Average(f => f.OverallRating), 2) : 0d;
        var contentRatings = feedbackRows.Where(f => f.ContentRating.HasValue).Select(f => f.ContentRating!.Value).ToList();
        var tourRatings = feedbackRows.Where(f => f.TourRating.HasValue).Select(f => f.TourRating!.Value).ToList();
        var chatbotRatings = feedbackRows.Where(f => f.ChatbotRating.HasValue).Select(f => f.ChatbotRating!.Value).ToList();

        var complaintsQuery = db.Complaints.AsNoTracking().AsQueryable();
        if (from.HasValue) complaintsQuery = complaintsQuery.Where(c => c.CreatedAt >= from.Value);
        if (to.HasValue) complaintsQuery = complaintsQuery.Where(c => c.CreatedAt < to.Value);

        var totalComplaints = await complaintsQuery.CountAsync(cancellationToken);
        var complaintsByStatus = (await complaintsQuery
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .OrderBy(g => g.Status)
            .ToListAsync(cancellationToken))
            .Select(g => new StatusCountDto(g.Status, g.Count))
            .ToList();

        var supportAndFeedback = new FeedbackAndComplaintsSummaryDto(
            totalFeedbacks,
            avgOverall,
            contentRatings.Count > 0 ? Math.Round(contentRatings.Average(), 2) : null,
            tourRatings.Count > 0 ? Math.Round(tourRatings.Average(), 2) : null,
            chatbotRatings.Count > 0 ? Math.Round(chatbotRatings.Average(), 2) : null,
            totalComplaints,
            complaintsByStatus);

        return ResponseDto<AdminDashboardDto>.Success(new AdminDashboardDto(
            overview,
            commerce,
            engagement,
            supportAndFeedback));
    }

    public async Task<ResponseDto<SystemStaffDashboardDto>> GetSystemStaffDashboardAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var from = NormalizeUtc(fromUtc);
        var to = NormalizeUtc(toUtc);
        if (!ValidateRange(from, to, out var error))
            return ResponseDto<SystemStaffDashboardDto>.Fail(error!, 400);

        // 1. Complaints Queue
        var complaintsQuery = db.Complaints.AsNoTracking().AsQueryable();
        if (from.HasValue) complaintsQuery = complaintsQuery.Where(c => c.CreatedAt >= from.Value);
        if (to.HasValue) complaintsQuery = complaintsQuery.Where(c => c.CreatedAt < to.Value);

        var totalComplaints = await complaintsQuery.CountAsync(cancellationToken);
        var newComplaints = await complaintsQuery.CountAsync(c => c.Status == ComplaintStatus.New, cancellationToken);
        var inProgressComplaints = await complaintsQuery.CountAsync(c => c.Status == ComplaintStatus.InProgress, cancellationToken);
        var resolvedComplaints = await complaintsQuery.CountAsync(c => c.Status == ComplaintStatus.Resolved || c.Status == ComplaintStatus.Closed, cancellationToken);
        var unassignedComplaints = await complaintsQuery.CountAsync(c => c.AssignedTo == null && c.Status != ComplaintStatus.Resolved && c.Status != ComplaintStatus.Closed, cancellationToken);

        var recentPendingComplaints = (await complaintsQuery
            .Where(c => c.Status == ComplaintStatus.New || c.Status == ComplaintStatus.InProgress)
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Take(10)
            .ToListAsync(cancellationToken))
            .Select(c => new PendingComplaintSummaryDto(c.Id, c.VisitorId, c.Subject, c.Status, c.AssignedTo, c.CreatedAt))
            .ToList();

        var complaintQueue = new StaffComplaintQueueDto(
            totalComplaints,
            newComplaints,
            inProgressComplaints,
            resolvedComplaints,
            unassignedComplaints,
            recentPendingComplaints);

        // 2. Feedback Moderation
        var feedbacksQuery = db.VisitFeedbacks.AsNoTracking().AsQueryable();
        if (from.HasValue) feedbacksQuery = feedbacksQuery.Where(f => f.CreatedAt >= from.Value);
        if (to.HasValue) feedbacksQuery = feedbacksQuery.Where(f => f.CreatedAt < to.Value);

        var feedbackList = await feedbacksQuery
            .Select(f => new { f.OverallRating, f.ModerationStatus })
            .ToListAsync(cancellationToken);

        var totalFeedbacks = feedbackList.Count;
        var visibleFeedbacks = feedbackList.Count(f => f.ModerationStatus == FeedbackModerationStatus.Visible);
        var hiddenFeedbacks = feedbackList.Count(f => f.ModerationStatus == FeedbackModerationStatus.Hidden);
        var lowRatedFeedbacks = feedbackList.Count(f => f.OverallRating <= 2);
        var avgOverallRating = totalFeedbacks > 0 ? Math.Round(feedbackList.Average(f => f.OverallRating), 2) : 0d;

        var feedbackModeration = new StaffFeedbackModerationDto(
            totalFeedbacks,
            visibleFeedbacks,
            hiddenFeedbacks,
            lowRatedFeedbacks,
            avgOverallRating);

        // 3. Payment Reconciliation
        var ordersQuery = db.PurchaseOrders.AsNoTracking().AsQueryable();
        if (from.HasValue) ordersQuery = ordersQuery.Where(o => o.CreatedAt >= from.Value);
        if (to.HasValue) ordersQuery = ordersQuery.Where(o => o.CreatedAt < to.Value);

        var paidOrdersCount = await ordersQuery.CountAsync(o => o.Status == "paid", cancellationToken);
        var refundedOrdersList = await ordersQuery.Where(o => o.Status == "cancelled" || o.RefundedAmount > 0).ToListAsync(cancellationToken);
        var cancelledOrRefundedCount = refundedOrdersList.Count;

        var refundsByCurrency = refundedOrdersList
            .Where(o => o.RefundedAmount > 0)
            .GroupBy(o => o.Currency)
            .OrderBy(g => g.Key)
            .Select(g => new CurrencyAmountDto(g.Key, g.Sum(o => o.RefundedAmount), g.Count()))
            .ToList();

        var failedAttemptsQuery = from a in db.PaymentAttempts.AsNoTracking()
                                  join o in db.PurchaseOrders.AsNoTracking() on a.OrderId equals o.Id
                                  where a.Status == "failed"
                                  select new { Attempt = a, Order = o };
        if (from.HasValue) failedAttemptsQuery = failedAttemptsQuery.Where(x => (x.Attempt.CompletedAt ?? x.Attempt.CreatedAt) >= from.Value);
        if (to.HasValue) failedAttemptsQuery = failedAttemptsQuery.Where(x => (x.Attempt.CompletedAt ?? x.Attempt.CreatedAt) < to.Value);

        var failedPaymentAttemptsCount = await failedAttemptsQuery.CountAsync(cancellationToken);
        var recentFailedRows = await failedAttemptsQuery
            .OrderByDescending(x => x.Attempt.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        var recentFailedPayments = recentFailedRows.Select(x => new FailedPaymentSummaryDto(
            x.Attempt.Id,
            x.Order.Id,
            x.Order.MuseumId,
            x.Order.ProductName,
            x.Order.Amount,
            x.Order.Currency,
            x.Attempt.FailureReason,
            x.Attempt.CreatedAt)).ToList();

        var paymentReconciliation = new StaffPaymentReconciliationDto(
            paidOrdersCount,
            cancelledOrRefundedCount,
            failedPaymentAttemptsCount,
            refundsByCurrency,
            recentFailedPayments);

        // 4. AI & Recognition Quality
        var chatQuery = db.GuideChatMessages.AsNoTracking().AsQueryable();
        if (from.HasValue) chatQuery = chatQuery.Where(m => m.CreatedAt >= from.Value);
        if (to.HasValue) chatQuery = chatQuery.Where(m => m.CreatedAt < to.Value);

        var totalChat = await chatQuery.CountAsync(cancellationToken);
        var supportedChat = await chatQuery.CountAsync(m => m.Supported, cancellationToken);
        var unsupportedChat = totalChat - supportedChat;
        var chatSupportRate = totalChat > 0 ? Math.Round(100d * supportedChat / totalChat, 2) : 0d;

        var recentUnsupportedQuestions = await (from m in chatQuery
                                                where !m.Supported
                                                join a in db.Artifacts.AsNoTracking() on m.ArtifactId equals a.Id into aj
                                                from a in aj.DefaultIfEmpty()
                                                join mu in db.Museums.AsNoTracking() on m.MuseumId equals mu.Id into muj
                                                from mu in muj.DefaultIfEmpty()
                                                orderby m.CreatedAt descending, m.Id descending
                                                select new GuideAdminLogDto(
                                                    m.Id,
                                                    m.UserId,
                                                    m.ArtifactId,
                                                    a != null ? a.Name : "",
                                                    m.MuseumId,
                                                    mu != null ? mu.Name : "",
                                                    m.Question,
                                                    m.Answer,
                                                    m.Supported,
                                                    m.LanguageCode,
                                                    m.ContentVersionNo,
                                                    m.CreatedAt))
                                               .Take(10)
                                               .ToListAsync(cancellationToken);

        var recognitionsQuery = db.Set<RecognitionHistory>().AsNoTracking().AsQueryable();
        if (from.HasValue) recognitionsQuery = recognitionsQuery.Where(r => r.RecognizedAt >= from.Value);
        if (to.HasValue) recognitionsQuery = recognitionsQuery.Where(r => r.RecognizedAt < to.Value);

        var totalRecognitions = await recognitionsQuery.CountAsync(cancellationToken);
        var qrRecognitions = await recognitionsQuery.CountAsync(r => r.Method == "qr", cancellationToken);
        var imageRecognitions = await recognitionsQuery.CountAsync(r => r.Method == "image", cancellationToken);
        var successfulRecognitions = await recognitionsQuery.CountAsync(r => r.IsSuccess, cancellationToken);
        var failedRecognitions = totalRecognitions - successfulRecognitions;
        var recognitionSuccessRate = totalRecognitions > 0 ? Math.Round(100d * successfulRecognitions / totalRecognitions, 2) : 0d;

        var aiQuality = new StaffAiAndRecognitionQualityDto(
            totalChat,
            supportedChat,
            unsupportedChat,
            chatSupportRate,
            recentUnsupportedQuestions,
            totalRecognitions,
            qrRecognitions,
            imageRecognitions,
            successfulRecognitions,
            failedRecognitions,
            recognitionSuccessRate);

        return ResponseDto<SystemStaffDashboardDto>.Success(new SystemStaffDashboardDto(
            complaintQueue,
            feedbackModeration,
            paymentReconciliation,
            aiQuality));
    }

    public async Task<ResponseDto<MuseumDashboardDto>> GetMuseumDashboardAsync(
        int museumId,
        Guid callerId,
        bool isAdmin,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (callerId == Guid.Empty)
            return ResponseDto<MuseumDashboardDto>.Fail("Vui lòng đăng nhập.", 401);

        var from = NormalizeUtc(fromUtc);
        var to = NormalizeUtc(toUtc);
        if (!ValidateRange(from, to, out var error))
            return ResponseDto<MuseumDashboardDto>.Fail(error!, 400);

        var museum = await db.Museums.AsNoTracking()
            .SingleOrDefaultAsync(m => m.Id == museumId, cancellationToken);
        if (museum is null)
            return ResponseDto<MuseumDashboardDto>.Fail("Không tìm thấy bảo tàng.", 404);

        if (!isAdmin)
        {
            var canManage = accountAdmin is not null
                ? await accountAdmin.CanManageMuseumAsync(callerId, false, museumId)
                : await db.StaffAssignments.AsNoTracking().AnyAsync(s => s.UserId == callerId && s.MuseumId == museumId, cancellationToken);

            if (!canManage && !await db.StaffAssignments.AsNoTracking().AnyAsync(s => s.UserId == callerId && s.MuseumId == museumId, cancellationToken))
                return ResponseDto<MuseumDashboardDto>.Fail("Bạn không được phân công quản lý bảo tàng này.", 403);
        }

        var now = UtcNow;

        // 1. Catalog
        var buildingsCount = await db.Buildings.AsNoTracking().CountAsync(b => b.MuseumId == museumId, cancellationToken);
        var floorsCount = await (from f in db.Floors.AsNoTracking()
                                 join b in db.Buildings.AsNoTracking() on f.BuildingId equals b.Id
                                 where b.MuseumId == museumId
                                 select f.Id).CountAsync(cancellationToken);
        var galleriesCount = await (from g in db.Galleries.AsNoTracking()
                                    join f in db.Floors.AsNoTracking() on g.FloorId equals f.Id
                                    join b in db.Buildings.AsNoTracking() on f.BuildingId equals b.Id
                                    where b.MuseumId == museumId
                                    select g.Id).CountAsync(cancellationToken);
        var publishedMapsCount = await db.IndoorMaps.AsNoTracking()
            .CountAsync(m => m.MuseumId == museumId && m.PublishedJson != null, cancellationToken);

        var artifactsInMuseum = db.Artifacts.AsNoTracking().Where(a => a.MuseumId == museumId);
        var totalArtifacts = await artifactsInMuseum.CountAsync(cancellationToken);
        var activeArtifacts = await artifactsInMuseum.CountAsync(a => a.IsActive, cancellationToken);
        var freeArtifacts = await artifactsInMuseum.CountAsync(a => a.IsActive && a.AccessLevel == "free", cancellationToken);
        var premiumArtifacts = await artifactsInMuseum.CountAsync(a => a.IsActive && a.AccessLevel == "premium", cancellationToken);
        var artifactsWith3D = await (from m in db.Artifact3DModels.AsNoTracking()
                                     join a in artifactsInMuseum on m.ArtifactId equals a.Id
                                     select m.Id).CountAsync(cancellationToken);

        var catalog = new MuseumCatalogStatsDto(
            buildingsCount,
            floorsCount,
            galleriesCount,
            publishedMapsCount,
            totalArtifacts,
            activeArtifacts,
            artifactsWith3D,
            freeArtifacts,
            premiumArtifacts);

        // 2. Curation Pipeline
        var contentStatuses = await (from v in db.ArtifactContentVersions.AsNoTracking()
                                     join c in db.ArtifactContents.AsNoTracking() on v.ArtifactContentId equals c.Id
                                     join a in artifactsInMuseum on c.ArtifactId equals a.Id
                                     group v by v.Status into g
                                     select new { Status = g.Key, Count = g.Count() })
                                    .ToListAsync(cancellationToken);

        int ContentCount(string status) => contentStatuses.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        var tourVersionStatuses = await (from tv in db.TourVersions.AsNoTracking()
                                         join t in db.Tours.AsNoTracking() on tv.TourId equals t.Id
                                         where t.MuseumId == museumId
                                         group tv by tv.Status into g
                                         select new { Status = g.Key, Count = g.Count() })
                                        .ToListAsync(cancellationToken);

        int TourVersionCount(string status) => tourVersionStatuses.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        var questionStatuses = await (from q in db.Set<Question>().AsNoTracking()
                                      join a in artifactsInMuseum on q.ArtifactId equals a.Id
                                      group q by q.Status into g
                                      select new { Status = g.Key, Count = g.Count() })
                                     .ToListAsync(cancellationToken);

        int QuestionCount(string status) => questionStatuses.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        var publishedQuizzesCount = await (from qz in db.Set<Quiz>().AsNoTracking()
                                           join a in artifactsInMuseum on qz.ArtifactId equals a.Id
                                           where qz.Status == "published"
                                           select qz.Id).CountAsync(cancellationToken);

        var curation = new MuseumCurationPipelineDto(
            ContentCount(ContentStatuses.PendingReview),
            ContentCount(ContentStatuses.Published),
            ContentCount(ContentStatuses.Rejected),
            TourVersionCount(TourVersionStatuses.Draft),
            TourVersionCount(TourVersionStatuses.Submitted),
            TourVersionCount(TourVersionStatuses.Approved),
            TourVersionCount(TourVersionStatuses.Rejected),
            QuestionCount("draft"),
            QuestionCount("approved"),
            QuestionCount("rejected"),
            publishedQuizzesCount);

        // 3. Visits & Engagement
        var visitsQuery = db.Visits.AsNoTracking().Where(v => v.MuseumId == museumId);
        if (from.HasValue) visitsQuery = visitsQuery.Where(v => v.StartedAt >= from.Value);
        if (to.HasValue) visitsQuery = visitsQuery.Where(v => v.StartedAt < to.Value);

        var totalVisits = await visitsQuery.CountAsync(cancellationToken);
        var completedVisits = await visitsQuery.CountAsync(v => v.Status == VisitStatus.Completed, cancellationToken);
        var inProgressVisits = await visitsQuery.CountAsync(v => v.Status == VisitStatus.InProgress, cancellationToken);
        var guidedVisits = await visitsQuery.CountAsync(v => v.TourId != null || v.TourVersionId != null || v.PersonalizedTourId != null, cancellationToken);
        var uniqueVisitors = await visitsQuery.Select(v => v.VisitorId).Distinct().CountAsync(cancellationToken);
        var completionRate = totalVisits > 0 ? Math.Round(100d * completedVisits / totalVisits, 2) : 0d;

        var recognitionsInMuseum = from r in db.Set<RecognitionHistory>().AsNoTracking()
                                   join a in artifactsInMuseum on r.ArtifactId equals a.Id
                                   where r.IsSuccess
                                   select new { r.Id, r.RecognizedAt, a.Name, ArtifactId = a.Id };
        if (from.HasValue) recognitionsInMuseum = recognitionsInMuseum.Where(r => r.RecognizedAt >= from.Value);
        if (to.HasValue) recognitionsInMuseum = recognitionsInMuseum.Where(r => r.RecognizedAt < to.Value);

        var totalRecognitions = await recognitionsInMuseum.CountAsync(cancellationToken);
        var topScannedArtifacts = (await recognitionsInMuseum
            .GroupBy(r => new { r.ArtifactId, r.Name })
            .Select(g => new { g.Key.ArtifactId, g.Key.Name, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.ArtifactId)
            .Take(10)
            .ToListAsync(cancellationToken))
            .Select(g => new PopularArtifactResponse(g.ArtifactId, g.Name, g.Count))
            .ToList();

        var chatInMuseum = db.GuideChatMessages.AsNoTracking().Where(m => m.MuseumId == museumId);
        if (from.HasValue) chatInMuseum = chatInMuseum.Where(m => m.CreatedAt >= from.Value);
        if (to.HasValue) chatInMuseum = chatInMuseum.Where(m => m.CreatedAt < to.Value);

        var totalChat = await chatInMuseum.CountAsync(cancellationToken);
        var supportedChat = await chatInMuseum.CountAsync(m => m.Supported, cancellationToken);
        var unsupportedChat = totalChat - supportedChat;

        var topChattedRows = await (from m in chatInMuseum
                                    join a in artifactsInMuseum on m.ArtifactId equals a.Id
                                    group m by new { a.Id, a.Name } into g
                                    orderby g.Count() descending, g.Key.Id
                                    select new { g.Key.Id, g.Key.Name, Count = g.Count() })
                                   .Take(5)
                                   .ToListAsync(cancellationToken);

        var topChattedArtifacts = topChattedRows
            .Select(g => new PopularArtifactResponse(g.Id, g.Name, g.Count))
            .ToList();

        var visitsAndEngagement = new MuseumVisitsAndEngagementDto(
            totalVisits,
            completedVisits,
            inProgressVisits,
            guidedVisits,
            uniqueVisitors,
            completionRate,
            totalRecognitions,
            totalChat,
            supportedChat,
            unsupportedChat,
            topScannedArtifacts,
            topChattedArtifacts);

        // 4. Commerce
        var ordersQuery = db.PurchaseOrders.AsNoTracking().Where(o => o.MuseumId == museumId);
        if (from.HasValue) ordersQuery = ordersQuery.Where(o => o.CreatedAt >= from.Value);
        if (to.HasValue) ordersQuery = ordersQuery.Where(o => o.CreatedAt < to.Value);

        var paidOrders = await ordersQuery.Where(o => o.Status == "paid").ToListAsync(cancellationToken);
        var refundedNonPaid = await ordersQuery.Where(o => o.Status != "paid" && o.RefundedAmount > 0).ToListAsync(cancellationToken);

        var failedAttemptsInMuseum = await (from a in db.PaymentAttempts.AsNoTracking()
                                            join o in ordersQuery on a.OrderId equals o.Id
                                            where a.Status == "failed"
                                            select new { o.Currency }).ToListAsync(cancellationToken);

        var currencies = paidOrders.Select(o => o.Currency)
            .Union(refundedNonPaid.Select(o => o.Currency))
            .Union(failedAttemptsInMuseum.Select(a => a.Currency))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        var revenueByCurrency = currencies.Select(currency =>
        {
            var paidForCurr = paidOrders.Where(o => o.Currency == currency).ToList();
            var gross = paidForCurr.Sum(o => o.Amount);
            var refunded = paidForCurr.Sum(o => o.RefundedAmount) + refundedNonPaid.Where(o => o.Currency == currency).Sum(o => o.RefundedAmount);
            var failed = failedAttemptsInMuseum.Count(a => a.Currency == currency);
            return new CurrencyRevenueSummaryDto(currency, paidForCurr.Count, gross, refunded, gross - refunded, failed);
        }).ToList();

        var salesByKind = paidOrders
            .GroupBy(o => new { o.ProductKind, o.Currency })
            .OrderBy(g => g.Key.ProductKind)
            .ThenBy(g => g.Key.Currency)
            .Select(g => new ProductKindSalesDto(
                g.Key.ProductKind,
                g.Key.Currency,
                g.Count(),
                g.Sum(o => o.Seats),
                g.Sum(o => o.Amount)))
            .ToList();

        var activePassesCount = await (from p in db.MuseumPasses.AsNoTracking()
                                       join o in db.PurchaseOrders.AsNoTracking() on p.OrderId equals o.Id
                                       where o.MuseumId == museumId && p.RevokedAt > now && o.Status == "paid" && o.CancelledAt == null && o.DigitalCancelledAt == null && o.ExpiresAt > now
                                       select p.Id).CountAsync(cancellationToken);

        var commerce = new MuseumCommerceStatsDto(
            paidOrders.Count,
            activePassesCount,
            revenueByCurrency,
            salesByKind);

        // 5. Feedback
        var feedbacksQuery = from f in db.VisitFeedbacks.AsNoTracking()
                             join v in db.Visits.AsNoTracking() on f.VisitId equals v.Id
                             where v.MuseumId == museumId && f.ModerationStatus == FeedbackModerationStatus.Visible
                             select new
                             {
                                 f.Id,
                                 f.CreatedAt,
                                 f.OverallRating,
                                 f.ContentRating,
                                 f.TourRating,
                                 f.ChatbotRating,
                                 HasResponse = f.StaffResponse != null
                             };
        if (from.HasValue) feedbacksQuery = feedbacksQuery.Where(f => f.CreatedAt >= from.Value);
        if (to.HasValue) feedbacksQuery = feedbacksQuery.Where(f => f.CreatedAt < to.Value);

        var feedbackRows = await feedbacksQuery.ToListAsync(cancellationToken);
        var totalFeedbacks = feedbackRows.Count;
        var unrespondedCount = feedbackRows.Count(f => !f.HasResponse);
        var avgOverall = totalFeedbacks > 0 ? Math.Round(feedbackRows.Average(f => f.OverallRating), 2) : 0d;
        var contentRatings = feedbackRows.Where(f => f.ContentRating.HasValue).Select(f => f.ContentRating!.Value).ToList();
        var tourRatings = feedbackRows.Where(f => f.TourRating.HasValue).Select(f => f.TourRating!.Value).ToList();
        var chatbotRatings = feedbackRows.Where(f => f.ChatbotRating.HasValue).Select(f => f.ChatbotRating!.Value).ToList();

        var feedback = new MuseumFeedbackStatsDto(
            totalFeedbacks,
            unrespondedCount,
            avgOverall,
            contentRatings.Count > 0 ? Math.Round(contentRatings.Average(), 2) : null,
            tourRatings.Count > 0 ? Math.Round(tourRatings.Average(), 2) : null,
            chatbotRatings.Count > 0 ? Math.Round(chatbotRatings.Average(), 2) : null);

        return ResponseDto<MuseumDashboardDto>.Success(new MuseumDashboardDto(
            museum.Id,
            museum.Name,
            museum.IsActive,
            catalog,
            curation,
            visitsAndEngagement,
            commerce,
            feedback));
    }
}
