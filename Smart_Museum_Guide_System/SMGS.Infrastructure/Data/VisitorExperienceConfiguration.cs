using Microsoft.EntityFrameworkCore;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Identity;

namespace SMGS.Infrastructure.Data;

public static class VisitorExperienceConfiguration
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<Tour>(e => {
            e.ToTable("tours"); e.Property(x => x.Title).HasMaxLength(200); e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Status).HasMaxLength(20); e.Property(x => x.Revision).IsConcurrencyToken();
            e.HasOne<Museum>().WithMany().HasForeignKey(x => x.MuseumId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.TourId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Versions).WithOne().HasForeignKey(x => x.TourId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<TourItem>(e => {
            e.ToTable("tour_items"); e.HasIndex(x => new { x.TourId, x.OrderNo }).IsUnique();
            e.HasIndex(x => new { x.TourId, x.ArtifactId }).IsUnique();
            e.HasOne<Artifact>().WithMany().HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<TourVersion>(e => {
            e.ToTable("tour_versions");
            e.Property(x => x.Title).IsRequired().HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Status).IsRequired().HasMaxLength(20);
            e.Property(x => x.ReviewNote).HasMaxLength(1000);
            e.HasIndex(x => new { x.TourId, x.VersionNo }).IsUnique();
            e.HasIndex(x => new { x.TourId, x.Status, x.PublicAt });
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.SubmittedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.TourVersionId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<TourVersionItem>(e => {
            e.ToTable("tour_version_items");
            e.HasIndex(x => new { x.TourVersionId, x.OrderNo }).IsUnique();
            e.HasIndex(x => new { x.TourVersionId, x.ArtifactId }).IsUnique();
            e.HasOne<Artifact>().WithMany().HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<SavedRoute>(e => {
            e.ToTable("saved_routes");
            e.Property(x => x.TopicIdsJson).IsRequired().HasMaxLength(1000);
            e.Property(x => x.Explanation).HasMaxLength(1000);
            e.HasIndex(x => new { x.VisitorId, x.MuseumId, x.SavedAt });
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.VisitorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Museum>().WithMany().HasForeignKey(x => x.MuseumId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Tour>().WithMany().HasForeignKey(x => x.TourId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TourVersion>().WithMany().HasForeignKey(x => x.TourVersionId).OnDelete(DeleteBehavior.Restrict);
        });
        // Legacy Visit.TourId values have never been enforced; a migration must reconcile these before adding the FK.
        b.Entity<Visit>().HasOne<Tour>().WithMany().HasForeignKey(x => x.TourId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Visit>().HasOne<TourVersion>().WithMany().HasForeignKey(x => x.TourVersionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<VisitItem>(e => {
            e.ToTable("visit_items"); e.HasIndex(x => new { x.VisitId, x.ArtifactId }).IsUnique();
            e.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Artifact>().WithMany().HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TourItem>().WithMany().HasForeignKey(x => x.TourItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TourVersionItem>().WithMany().HasForeignKey(x => x.TourVersionItemId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<RecognitionHistory>(e => {
            e.ToTable("recognition_history"); e.Property(x => x.Method).HasMaxLength(20);
            e.HasIndex(x => new { x.UserId, x.RequestId }).IsUnique();
            e.HasIndex(x => new { x.UserId, x.RecognizedAt });
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Artifact>().WithMany().HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Question>(e => {
            e.ToTable("questions"); e.Property(x => x.Content).HasMaxLength(4000); e.Property(x => x.Explanation).HasMaxLength(4000);
            e.Property(x => x.LanguageCode).HasMaxLength(35); e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.ReviewNote).HasMaxLength(1000);
            e.HasOne<Artifact>().WithMany().HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Options).WithOne().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<QuestionOption>(e => { e.ToTable("question_options"); e.Property(x => x.Content).HasMaxLength(1000); });
        b.Entity<Quiz>(e => {
            e.ToTable("quizzes"); e.Property(x => x.Title).HasMaxLength(200); e.Property(x => x.Status).HasMaxLength(20);
            e.HasOne<Artifact>().WithMany().HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Questions).WithOne().HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<QuizQuestion>(e => {
            e.ToTable("quiz_questions"); e.HasKey(x => new { x.QuizId, x.QuestionId });
            e.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<DailyQuizEntry>(e => {
            e.ToTable("daily_quiz_entries"); e.Property(x => x.Status).HasMaxLength(20);
            e.HasIndex(x => new { x.UserId, x.QuizId, x.EntryDate }).IsUnique();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Quiz>().WithMany().HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Artifact>().WithMany().HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<RecognitionHistory>().WithMany().HasForeignKey(x => x.RecognitionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<QuizAttempt>(e => {
            e.ToTable("quiz_attempts"); e.Property(x => x.Status).HasMaxLength(20); e.Property(x => x.Mode).HasMaxLength(20);
            e.HasIndex(x => x.DailyEntryId).IsUnique().HasFilter("\"Status\" = 'in_progress'");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Quiz>().WithMany().HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<DailyQuizEntry>().WithMany().HasForeignKey(x => x.DailyEntryId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Answers).WithOne().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<QuizAttemptAnswer>(e => {
            e.ToTable("quiz_attempt_answers"); e.HasIndex(x => new { x.AttemptId, x.QuestionId }).IsUnique();
            e.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<QuestionOption>().WithMany().HasForeignKey(x => x.SelectedOptionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<PointTransaction>(e => {
            e.ToTable("point_transactions"); e.Property(x => x.Reason).HasMaxLength(50); e.Property(x => x.RefType).HasMaxLength(50);
            e.HasIndex(x => new { x.UserId, x.Reason, x.RefType, x.RefId }).IsUnique();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Badge>(e => {
            e.ToTable("badges"); e.Property(x => x.Code).HasMaxLength(50); e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.RuleJson).HasColumnType("jsonb"); e.HasIndex(x => x.Code).IsUnique();
            e.HasOne<Museum>().WithMany().HasForeignKey(x => x.MuseumId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<UserBadge>(e => {
            e.ToTable("user_badges"); e.HasKey(x => new { x.UserId, x.BadgeId });
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Badge>().WithMany().HasForeignKey(x => x.BadgeId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
