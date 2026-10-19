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
    public DbSet<Artifact> Artifacts => Set<Artifact>();
    public DbSet<Gallery> Galleries => Set<Gallery>();
    public DbSet<Floor> Floors => Set<Floor>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityRole<Guid>>().HasData(
            new { Id = new Guid("c1e5f237-c782-4910-9235-5d73ecdd3001"), Name = RoleNames.Visitor, NormalizedName = "VISITOR", ConcurrencyStamp = "seed-visitor-v1" },
            new { Id = new Guid("c1e5f237-c782-4910-9235-5d73ecdd3002"), Name = RoleNames.Admin, NormalizedName = "ADMIN", ConcurrencyStamp = "seed-admin-v1" },
            new { Id = new Guid("c1e5f237-c782-4910-9235-5d73ecdd3003"), Name = RoleNames.Staff, NormalizedName = "STAFF", ConcurrencyStamp = "seed-staff-v1" },
            new { Id = new Guid("c1e5f237-c782-4910-9235-5d73ecdd3004"), Name = RoleNames.MuseumStaff, NormalizedName = "MUSEUM STAFF", ConcurrencyStamp = "seed-museum-staff-v1" });
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.Entity<ApplicationUser>().Property(user => user.FullName).HasMaxLength(200);
        builder.Entity<ApplicationUser>().HasIndex(user => user.NormalizedEmail).IsUnique();

        // Museum
        builder.Entity<Museum>(entity =>
        {
            entity.ToTable("museums");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasColumnType("text");
            entity.Property(e => e.Address).HasMaxLength(500);
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

        // Artifact
        builder.Entity<Artifact>(entity =>
        {
            entity.ToTable("artifacts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(300);
            entity.Property(e => e.Code).HasMaxLength(100);
            entity.Property(e => e.QrCode).HasMaxLength(200);
            entity.HasIndex(e => e.QrCode).IsUnique().HasFilter("qr_code IS NOT NULL");
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
    }
}