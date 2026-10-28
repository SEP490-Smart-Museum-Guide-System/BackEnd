using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Identity;
using SMGS.Domain.Constants;

namespace SMGS.Infrastructure.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Museum> Museums => Set<Museum>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<CommerceResource> CommerceResources => Set<CommerceResource>();
    public DbSet<CommerceProduct> CommerceProducts => Set<CommerceProduct>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
    public DbSet<MuseumPass> MuseumPasses => Set<MuseumPass>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<ArtifactContent> ArtifactContents => Set<ArtifactContent>();
    public DbSet<ArtifactContentVersion> ArtifactContentVersions => Set<ArtifactContentVersion>();
    public DbSet<Artifact> Artifacts => Set<Artifact>();
    public DbSet<Gallery> Galleries => Set<Gallery>();
    public DbSet<Floor> Floors => Set<Floor>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<VisitFeedback> VisitFeedbacks => Set<VisitFeedback>();
    public DbSet<StaffResponse> StaffResponses => Set<StaffResponse>();
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<StaffAssignment> StaffAssignments => Set<StaffAssignment>();
    public DbSet<IndoorMap> IndoorMaps => Set<IndoorMap>();
    public DbSet<MapNode> MapNodes => Set<MapNode>();
    public DbSet<MapMarker> MapMarkers => Set<MapMarker>();
    public DbSet<MapEdge> MapEdges => Set<MapEdge>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        IndoorMapConfiguration.Configure(builder);
        CommerceConfiguration.Configure(builder);
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityRole<Guid>>().HasData(DataSeeder.CreateRoleSeeds());
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.Entity<ApplicationUser>().Property(user => user.FullName).HasMaxLength(200);
        builder.Entity<ApplicationUser>().HasIndex(user => user.NormalizedEmail).IsUnique();

        // Museum
        builder.Entity<City>(entity =>
        {
            entity.ToTable("cities");
            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code).HasMaxLength(2);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasData(CityCatalog.Items);
        });
        builder.Entity<Museum>(entity =>
        {
            entity.ToTable("museums");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasColumnType("text");
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.CityCode).HasMaxLength(2);
            entity.HasOne<City>().WithMany().HasForeignKey(e => e.CityCode).OnDelete(DeleteBehavior.Restrict);
            entity.Property(e => e.Latitude).HasColumnType("decimal(9,6)");
            entity.Property(e => e.Longitude).HasColumnType("decimal(9,6)");
            entity.Property(e => e.OpeningHours).HasMaxLength(500);
            entity.Property(e => e.CoverImageUrl).HasMaxLength(1000);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnType("timestamptz");
        });

        // Floor
        builder.Entity<Floor>(entity =>
        {
            entity.ToTable("floors");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.LevelNo).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.MapImageUrl).HasMaxLength(1000);
        });

        // Gallery
        builder.Entity<Gallery>(entity =>
        {
            entity.ToTable("galleries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(300);
            entity.Property(e => e.Description).HasColumnType("text");
            entity.HasOne(e => e.Floor)
                .WithMany(f => f.Galleries)
                .HasForeignKey(e => e.FloorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ArtifactContent>(entity =>
        {
            entity.ToTable("artifact_contents");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.LanguageCode).IsRequired().HasMaxLength(35);
            entity.Property(e => e.Revision).IsConcurrencyToken();
            entity.HasIndex(e => new { e.ArtifactId, e.LanguageCode }).IsUnique();
            entity.HasOne(e => e.Artifact).WithMany().HasForeignKey(e => e.ArtifactId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ArtifactContentVersion>(entity =>
        {
            entity.ToTable("artifact_content_versions");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Title).IsRequired().HasMaxLength(300);
            entity.Property(e => e.Description).IsRequired().HasColumnType("text");
            entity.Property(e => e.Status).IsRequired().HasMaxLength(30).IsConcurrencyToken();
            entity.Property(e => e.ReviewNote).HasMaxLength(2000);
            entity.HasIndex(e => new { e.ArtifactContentId, e.VersionNo }).IsUnique();
            entity.HasIndex(e => e.ArtifactContentId).IsUnique().HasFilter("\"IsCurrent\" = TRUE");
            entity.HasOne(e => e.Content).WithMany(c => c.Versions).HasForeignKey(e => e.ArtifactContentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
        });

        // Artifact
        builder.Entity<Artifact>(entity =>
        {
            entity.ToTable("artifacts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(300);
            entity.Property(e => e.Code).HasMaxLength(100);
            entity.Property(e => e.QrCode).HasMaxLength(200);
            entity.HasIndex(e => e.QrCode).IsUnique().HasFilter("\"QrCode\" IS NOT NULL");
            entity.Property(e => e.MapIcon).HasMaxLength(500);
            entity.Property(e => e.AccessLevel).HasMaxLength(20).HasDefaultValue("free");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnType("timestamptz");
            entity.HasOne(e => e.Museum)
                .WithMany()
                .HasForeignKey(e => e.MuseumId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Gallery)
                .WithMany(g => g.Artifacts)
                .HasForeignKey(e => e.GalleryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Visit
        builder.Entity<Visit>(entity =>
        {
            entity.ToTable("visits");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.StartedAt).HasColumnType("timestamptz");
            entity.Property(e => e.CompletedAt).HasColumnType("timestamptz");
            entity.HasIndex(e => new { e.VisitorId, e.MuseumId, e.Status });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.VisitorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Museum).WithMany().HasForeignKey(e => e.MuseumId).OnDelete(DeleteBehavior.Restrict);
        });

        // VisitFeedback
        builder.Entity<VisitFeedback>(entity =>
        {
            entity.ToTable("visit_feedbacks", t =>
            {
                t.HasCheckConstraint("CK_visit_feedbacks_overall", "\"OverallRating\" BETWEEN 1 AND 5");
                t.HasCheckConstraint("CK_visit_feedbacks_content", "\"ContentRating\" IS NULL OR \"ContentRating\" BETWEEN 1 AND 5");
                t.HasCheckConstraint("CK_visit_feedbacks_tour", "\"TourRating\" IS NULL OR \"TourRating\" BETWEEN 1 AND 5");
                t.HasCheckConstraint("CK_visit_feedbacks_chatbot", "\"ChatbotRating\" IS NULL OR \"ChatbotRating\" BETWEEN 1 AND 5");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Comment).HasMaxLength(2000);
            entity.Property(e => e.ModerationStatus).IsRequired().HasMaxLength(20).HasDefaultValue(FeedbackModerationStatus.Visible);
            entity.Property(e => e.ModerationNote).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasColumnType("timestamptz");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamptz");
            // Mỗi lượt tham quan chỉ có một đánh giá
            entity.HasIndex(e => e.VisitId).IsUnique();
            entity.HasOne(e => e.Visit).WithOne(v => v.Feedback).HasForeignKey<VisitFeedback>(e => e.VisitId).OnDelete(DeleteBehavior.Cascade);
        });

        // StaffResponse
        builder.Entity<StaffResponse>(entity =>
        {
            entity.ToTable("staff_responses");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Content).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasColumnType("timestamptz");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamptz");
            entity.HasIndex(e => e.FeedbackId).IsUnique();
            entity.HasOne(e => e.Feedback).WithOne(f => f.StaffResponse).HasForeignKey<StaffResponse>(e => e.FeedbackId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.RespondedBy).OnDelete(DeleteBehavior.Restrict);
        });

        // Complaint
        builder.Entity<Complaint>(entity =>
        {
            entity.ToTable("complaints");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Subject).IsRequired().HasMaxLength(300);
            entity.Property(e => e.Body).IsRequired().HasMaxLength(5000);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue(ComplaintStatus.New);
            entity.Property(e => e.ResolutionNote).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasColumnType("timestamptz");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamptz");
            entity.Property(e => e.ResolvedAt).HasColumnType("timestamptz");
            entity.HasIndex(e => e.Status);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.VisitorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
