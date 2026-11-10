namespace SMGS.Domain.Entities;

public class Tour
{
    public int Id { get; set; }
    public int MuseumId { get; set; }
    public Guid CreatedBy { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int EstimatedMinutes { get; set; }
    public string Status { get; set; } = "draft";
    public int Revision { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<TourItem> Items { get; set; } = new List<TourItem>();
    public ICollection<TourVersion> Versions { get; set; } = new List<TourVersion>();
}
public class TourItem
{
    public int Id { get; set; }
    public int TourId { get; set; }
    public int ArtifactId { get; set; }
    public int OrderNo { get; set; }
}
public class VisitItem
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public int ArtifactId { get; set; }
    public int? TourItemId { get; set; }
    public int? TourVersionItemId { get; set; }
    public DateTime LastVisitedAt { get; set; }
}
public class RecognitionHistory
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public int? VisitId { get; set; }
    public int? ArtifactId { get; set; }
    public string Method { get; set; } = "qr";
    public bool IsSuccess { get; set; }
    public DateTime RecognizedAt { get; set; }
    public Guid RequestId { get; set; }
}
public class Question
{
    public int Id { get; set; }
    public int ArtifactId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string Content { get; set; } = "";
    public string Explanation { get; set; } = "";
    public string Status { get; set; } = "draft";
    public Guid CreatedBy { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
}
public class QuestionOption
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string Content { get; set; } = "";
    public bool IsCorrect { get; set; }
    public int OrderNo { get; set; }
}
public class Quiz
{
    public int Id { get; set; }
    public int ArtifactId { get; set; }
    public string Title { get; set; } = "";
    public string Status { get; set; } = "draft";
    public Guid CreatedBy { get; set; }
    public int QuestionsPerAttempt { get; set; } = 5;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();
}
public class QuizQuestion
{
    public int QuizId { get; set; }
    public int QuestionId { get; set; }
}
public class DailyQuizEntry
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public int QuizId { get; set; }
    public int ArtifactId { get; set; }
    public long RecognitionId { get; set; }
    public int? VisitId { get; set; }
    public DateOnly EntryDate { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }
}
public class QuizAttempt
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public int QuizId { get; set; }
    public long DailyEntryId { get; set; }
    public string Mode { get; set; } = "later";
    public string Status { get; set; } = "in_progress";
    public int TotalQuestions { get; set; }
    public int CorrectCount { get; set; }
    public int Score { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ICollection<QuizAttemptAnswer> Answers { get; set; } = new List<QuizAttemptAnswer>();
}
public class QuizAttemptAnswer
{
    public long Id { get; set; }
    public long AttemptId { get; set; }
    public int QuestionId { get; set; }
    public int? SelectedOptionId { get; set; }
    public bool IsCorrect { get; set; }
    public int Points { get; set; }
    public DateTime? AnsweredAt { get; set; }
    public int OrderNo { get; set; }
}
public class PointTransaction
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public int Points { get; set; }
    public string Reason { get; set; } = "";
    public string RefType { get; set; } = "";
    public long RefId { get; set; }
    public DateTime CreatedAt { get; set; }
}
public class Badge
{
    public int Id { get; set; }
    public int? MuseumId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string RuleJson { get; set; } = "{}";
}
public class UserBadge
{
    public Guid UserId { get; set; }
    public int BadgeId { get; set; }
    public DateTime AwardedAt { get; set; }
}
