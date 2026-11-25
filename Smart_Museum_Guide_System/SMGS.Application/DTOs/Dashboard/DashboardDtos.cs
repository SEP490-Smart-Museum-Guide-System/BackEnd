using SMGS.Application.DTOs.Commerce;
using SMGS.Application.DTOs.Guide;

namespace SMGS.Application.DTOs.Dashboard;

public record RoleUserCountDto(string Role, int UserCount);

public record StatusCountDto(string Status, int Count);

public record CurrencyAmountDto(string Currency, decimal Amount, int Count);

public record CurrencyRevenueSummaryDto(
    string Currency,
    int PaidOrders,
    decimal GrossRevenue,
    decimal RefundedAmount,
    decimal NetRevenue,
    int FailedAttempts);

public record ProductKindSalesDto(
    string ProductKind,
    string Currency,
    int PaidOrders,
    int TotalSeats,
    decimal GrossRevenue);

public record MuseumRevenueRankDto(
    int MuseumId,
    string MuseumName,
    string Currency,
    int PaidOrders,
    decimal GrossRevenue,
    decimal RefundedAmount,
    decimal NetRevenue);

public record SystemOverviewStatsDto(
    int TotalMuseums,
    int ActiveMuseums,
    int TotalArtifacts,
    int ActiveArtifacts,
    int ArtifactsWith3DModel,
    int PremiumArtifacts,
    int TotalUsers,
    int ActiveUsers,
    IReadOnlyList<RoleUserCountDto> UsersByRole);

public record AdminCommerceStatsDto(
    int TotalPaidOrders,
    int TotalPendingOrders,
    int TotalCancelledOrders,
    int ActivePassesCount,
    IReadOnlyList<CurrencyRevenueSummaryDto> RevenueByCurrency,
    IReadOnlyList<ProductKindSalesDto> SalesByProductKind,
    IReadOnlyList<MuseumRevenueRankDto> TopMuseumsByRevenue);

public record AdminEngagementStatsDto(
    int TotalVisits,
    int CompletedVisits,
    int InProgressVisits,
    int GuidedVisits,
    int TotalRecognitions,
    int QrRecognitions,
    int ImageRecognitions,
    int TotalQuizAttempts,
    int CompletedQuizAttempts,
    double AverageQuizScore,
    int TotalChatbotQuestions,
    int SupportedChatbotAnswers,
    int UnsupportedChatbotAnswers,
    double ChatbotSupportRatePercent);

public record FeedbackAndComplaintsSummaryDto(
    int TotalFeedbacks,
    double AverageOverallRating,
    double? AverageContentRating,
    double? AverageTourRating,
    double? AverageChatbotRating,
    int TotalComplaints,
    IReadOnlyList<StatusCountDto> ComplaintsByStatus);

public record AdminDashboardDto(
    SystemOverviewStatsDto Overview,
    AdminCommerceStatsDto Commerce,
    AdminEngagementStatsDto Engagement,
    FeedbackAndComplaintsSummaryDto SupportAndFeedback);

public record PendingComplaintSummaryDto(
    int Id,
    Guid VisitorId,
    string Subject,
    string Status,
    Guid? AssignedTo,
    DateTime CreatedAt);

public record FailedPaymentSummaryDto(
    Guid AttemptId,
    Guid OrderId,
    int MuseumId,
    string ProductName,
    decimal Amount,
    string Currency,
    string? FailureReason,
    DateTime CreatedAt);

public record StaffComplaintQueueDto(
    int TotalComplaints,
    int NewComplaints,
    int InProgressComplaints,
    int ResolvedComplaints,
    int UnassignedComplaints,
    IReadOnlyList<PendingComplaintSummaryDto> RecentPendingComplaints);

public record StaffFeedbackModerationDto(
    int TotalFeedbacks,
    int VisibleFeedbacks,
    int HiddenFeedbacks,
    int LowRatedFeedbacksCount,
    double AverageOverallRating);

public record StaffPaymentReconciliationDto(
    int PaidOrdersCount,
    int CancelledOrRefundedOrdersCount,
    int FailedPaymentAttemptsCount,
    IReadOnlyList<CurrencyAmountDto> RefundsByCurrency,
    IReadOnlyList<FailedPaymentSummaryDto> RecentFailedPayments);

public record StaffAiAndRecognitionQualityDto(
    int TotalChatbotQuestions,
    int SupportedChatbotAnswers,
    int UnsupportedChatbotAnswers,
    double ChatbotSupportRatePercent,
    IReadOnlyList<GuideAdminLogDto> RecentUnsupportedQuestions,
    int TotalRecognitions,
    int QrRecognitions,
    int ImageRecognitions,
    int SuccessfulRecognitions,
    int FailedRecognitions,
    double RecognitionSuccessRatePercent);

public record SystemStaffDashboardDto(
    StaffComplaintQueueDto Complaints,
    StaffFeedbackModerationDto FeedbackModeration,
    StaffPaymentReconciliationDto PaymentReconciliation,
    StaffAiAndRecognitionQualityDto AiAndRecognitionQuality);

public record MuseumCatalogStatsDto(
    int BuildingsCount,
    int FloorsCount,
    int GalleriesCount,
    int PublishedIndoorMapsCount,
    int TotalArtifacts,
    int ActiveArtifacts,
    int ArtifactsWith3DModel,
    int FreeArtifacts,
    int PremiumArtifacts);

public record MuseumCurationPipelineDto(
    int PendingArtifactContentsCount,
    int PublishedArtifactContentsCount,
    int RejectedArtifactContentsCount,
    int DraftTourVersionsCount,
    int SubmittedTourVersionsCount,
    int ApprovedTourVersionsCount,
    int RejectedTourVersionsCount,
    int DraftQuestionsCount,
    int ApprovedQuestionsCount,
    int RejectedQuestionsCount,
    int PublishedQuizzesCount);

public record MuseumVisitsAndEngagementDto(
    int TotalVisits,
    int CompletedVisits,
    int InProgressVisits,
    int GuidedVisits,
    int UniqueVisitors,
    double CompletionRatePercent,
    int TotalRecognitions,
    int TotalChatbotQuestions,
    int SupportedChatbotAnswers,
    int UnsupportedChatbotAnswers,
    IReadOnlyList<PopularArtifactResponse> TopScannedArtifacts,
    IReadOnlyList<PopularArtifactResponse> TopChattedArtifacts);

public record MuseumCommerceStatsDto(
    int TotalPaidOrders,
    int ActivePassesCount,
    IReadOnlyList<CurrencyRevenueSummaryDto> RevenueByCurrency,
    IReadOnlyList<ProductKindSalesDto> SalesByProductKind);

public record MuseumFeedbackStatsDto(
    int TotalFeedbacks,
    int UnrespondedFeedbacksCount,
    double AverageOverallRating,
    double? AverageContentRating,
    double? AverageTourRating,
    double? AverageChatbotRating);

public record MuseumDashboardDto(
    int MuseumId,
    string MuseumName,
    bool IsActive,
    MuseumCatalogStatsDto Catalog,
    MuseumCurationPipelineDto CurationPipeline,
    MuseumVisitsAndEngagementDto VisitsAndEngagement,
    MuseumCommerceStatsDto Commerce,
    MuseumFeedbackStatsDto Feedback);
