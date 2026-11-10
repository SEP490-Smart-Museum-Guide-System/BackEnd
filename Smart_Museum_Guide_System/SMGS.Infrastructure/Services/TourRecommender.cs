using SMGS.Application.DTOs.Experience;

namespace SMGS.Infrastructure.Services;

/// <summary>
/// Bộ chấm điểm và xếp hạng tour theo sở thích + thời gian rảnh (WF-02: F05 - Mức 1).
/// Đây là class thuần (pure logic, không phụ thuộc DB) để dễ hiểu, dễ kiểm thử tất định (deterministic),
/// và dễ giải thích khi bảo vệ đồ án.
/// </summary>
public static class TourRecommender
{
    public const double InterestWeight = 0.7;
    public const double TimeFitWeight = 0.2;
    public const double NoveltyWeight = 0.1;

    public const string FallbackNoPublicTour = "NO_PUBLIC_TOUR";
    public const string FallbackTimeTooShort = "TIME_TOO_SHORT";
    public const string FallbackNoInterestMatch = "NO_INTEREST_MATCH";

    public sealed record CandidateTour(
        int TourId,
        int TourVersionId,
        int VersionNo,
        string Title,
        string Description,
        int EstimatedMinutes,
        IReadOnlyList<int> ArtifactIds,
        IReadOnlyList<int> TopicIds,
        IReadOnlyDictionary<int, string> TopicNames,
        bool AlreadyCompletedByVisitor,
        int CompletedVisitCount);

    public static TourRecommendationResultDto Recommend(
        IReadOnlyList<CandidateTour> candidates,
        IReadOnlyCollection<int> requestedTopicIds,
        int availableMinutes)
    {
        if (availableMinutes is < 1 or > 1440)
        {
            throw new ExperienceException("Thời lượng có sẵn phải nằm trong khoảng từ 1 đến 1440 phút.");
        }

        if (candidates.Count == 0)
        {
            return new TourRecommendationResultDto(
                Recommendations: [],
                FallbackCode: FallbackNoPublicTour,
                FallbackExplanation: "Bảo tàng hiện chưa có phiên bản tour công khai nào.",
                FallbackSuggestion: null);
        }

        var distinctInterests = requestedTopicIds
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

        // Bước 1: Lọc các tour vừa với quỹ thời gian của khách.
        var fitting = candidates
            .Where(c => c.EstimatedMinutes <= availableMinutes)
            .ToList();

        if (fitting.Count == 0)
        {
            var shortest = candidates
                .OrderBy(c => c.EstimatedMinutes)
                .ThenByDescending(c => c.CompletedVisitCount)
                .ThenBy(c => c.TourId)
                .First();

            var extraMinutes = shortest.EstimatedMinutes - availableMinutes;
            var suggestion = ScoreCandidate(shortest, distinctInterests, shortest.EstimatedMinutes);

            return new TourRecommendationResultDto(
                Recommendations: [],
                FallbackCode: FallbackTimeTooShort,
                FallbackExplanation: $"Không có tour nào vừa với {availableMinutes} phút. Tour ngắn nhất là \"{shortest.Title}\" ({shortest.EstimatedMinutes} phút, cần thêm {extraMinutes} phút).",
                FallbackSuggestion: suggestion);
        }

        // Bước 2: Chấm điểm từng tour vừa thời gian.
        var scored = fitting
            .Select(c => (Candidate: c, Dto: ScoreCandidate(c, distinctInterests, availableMinutes)))
            .ToList();

        // Nếu khách có chọn sở thích nhưng không tour nào khớp chủ đề nào -> trả về Fallback NO_INTEREST_MATCH kèm gợi ý thay thế.
        if (distinctInterests.Length > 0 && scored.All(x => x.Dto.MatchedTopicIds.Count == 0))
        {
            var bestAlternative = scored
                .OrderByDescending(x => x.Candidate.CompletedVisitCount)
                .ThenByDescending(x => x.Dto.TotalScore)
                .ThenBy(x => x.Dto.TourId)
                .First()
                .Dto;

            return new TourRecommendationResultDto(
                Recommendations: [],
                FallbackCode: FallbackNoInterestMatch,
                FallbackExplanation: $"Chưa có tour nào trong quỹ thời gian {availableMinutes} phút khớp với chủ đề bạn chọn. Gợi ý tour tiêu biểu: \"{bestAlternative.Title}\" ({bestAlternative.EstimatedMinutes} phút).",
                FallbackSuggestion: bestAlternative);
        }

        // Bước 3: Sắp xếp tất định (deterministic): nếu khách có chọn sở thích thì chỉ giữ các tour có ít nhất 1 chủ đề khớp,
        // xếp theo TotalScore giảm dần -> InterestScore giảm dần -> phổ biến giảm dần -> TourId tăng dần.
        var filtered = distinctInterests.Length > 0
            ? scored.Where(x => x.Dto.MatchedTopicIds.Count > 0)
            : scored;

        var ranked = filtered
            .OrderByDescending(x => x.Dto.TotalScore)
            .ThenByDescending(x => x.Dto.InterestScore)
            .ThenByDescending(x => x.Candidate.CompletedVisitCount)
            .ThenBy(x => x.Dto.TourId)
            .Select(x => x.Dto)
            .ToList();

        return new TourRecommendationResultDto(
            Recommendations: ranked,
            FallbackCode: null,
            FallbackExplanation: null,
            FallbackSuggestion: null);
    }

    public static RecommendedTourDto ScoreCandidate(
        CandidateTour candidate,
        IReadOnlyCollection<int> distinctInterests,
        int availableMinutes)
    {
        var candidateTopics = candidate.TopicIds.Distinct().OrderBy(id => id).ToArray();
        var matched = distinctInterests.Intersect(candidateTopics).OrderBy(id => id).ToArray();

        // Điểm khớp sở thích theo hệ số Jaccard: |A ∩ B| / |A ∪ B|.
        // Nếu khách không chọn chủ đề nào thì coi mọi tour có điểm sở thích trung lập (1.0).
        double interestScore;
        if (distinctInterests.Count == 0)
        {
            interestScore = 1.0;
        }
        else
        {
            var unionCount = distinctInterests.Union(candidateTopics).Count();
            interestScore = unionCount == 0 ? 0.0 : (double)matched.Length / unionCount;
        }

        var safeAvailable = Math.Max(availableMinutes, candidate.EstimatedMinutes);
        var timeFitScore = safeAvailable <= 0
            ? 0.0
            : Math.Clamp((double)candidate.EstimatedMinutes / safeAvailable, 0.0, 1.0);

        var noveltyScore = candidate.AlreadyCompletedByVisitor ? 0.0 : 1.0;

        var totalScore = Math.Round(
            (InterestWeight * interestScore) +
            (TimeFitWeight * timeFitScore) +
            (NoveltyWeight * noveltyScore),
            4);

        var explanation = BuildExplanation(candidate, distinctInterests.Count, matched, availableMinutes);

        return new RecommendedTourDto(
            TourId: candidate.TourId,
            TourVersionId: candidate.TourVersionId,
            VersionNo: candidate.VersionNo,
            Title: candidate.Title,
            Description: candidate.Description,
            EstimatedMinutes: candidate.EstimatedMinutes,
            ArtifactIds: candidate.ArtifactIds,
            MatchedTopicIds: matched,
            InterestScore: Math.Round(interestScore, 4),
            TimeFitScore: Math.Round(timeFitScore, 4),
            NoveltyScore: noveltyScore,
            TotalScore: totalScore,
            Explanation: explanation);
    }

    private static string BuildExplanation(
        CandidateTour candidate,
        int requestedTopicCount,
        IReadOnlyList<int> matchedTopicIds,
        int availableMinutes)
    {
        var topicPart = requestedTopicCount == 0
            ? "Phù hợp cho khách chưa chọn chủ đề cụ thể"
            : matchedTopicIds.Count > 0
                ? $"Khớp {matchedTopicIds.Count}/{requestedTopicCount} chủ đề bạn quan tâm ({ string.Join(", ", matchedTopicIds.Select(id => candidate.TopicNames.TryGetValue(id, out var name) ? name : $"#{id}")) })"
                : "Không khớp chủ đề đã chọn";

        var timePart = $"Thời lượng ước tính {candidate.EstimatedMinutes}/{availableMinutes} phút ({candidate.ArtifactIds.Count} điểm dừng)";
        return $"{topicPart}. {timePart}.";
    }
}

