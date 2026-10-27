using Microsoft.EntityFrameworkCore;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Identity;

namespace SMGS.Infrastructure.Data;

internal static class IndoorMapConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<Museum>().Property(m => m.IndoorLayout).HasMaxLength(20).HasDefaultValue("unknown");
        builder.Entity<Building>(e =>
        {
            e.ToTable("buildings"); e.Property(x => x.Name).HasMaxLength(200);
            e.HasOne<Museum>().WithMany().HasForeignKey(x => x.MuseumId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Floor>().HasOne<Building>().WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Floor>().HasIndex(x => new { x.BuildingId, x.LevelNo }).IsUnique();
        builder.Entity<StaffAssignment>(e =>
        {
            e.ToTable("staff_assignments");
            e.HasIndex(x => new { x.UserId, x.MuseumId }).IsUnique().HasFilter("\"GalleryId\" IS NULL AND \"ArtifactId\" IS NULL");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Museum>().WithMany().HasForeignKey(x => x.MuseumId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Gallery>().WithMany().HasForeignKey(x => x.GalleryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Artifact>().WithMany().HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<IndoorMap>(e =>
        {
            e.ToTable("indoor_maps"); e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.Property(x => x.PublishedJson).HasColumnType("jsonb");
            e.HasIndex(x => x.FloorId).IsUnique().HasFilter("\"FloorId\" IS NOT NULL");
            e.HasIndex(x => x.MuseumId).IsUnique().HasFilter("\"FloorId\" IS NULL");
            e.HasOne<Museum>().WithMany().HasForeignKey(x => x.MuseumId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Floor>().WithMany().HasForeignKey(x => x.FloorId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Nodes).WithOne().HasForeignKey(x => x.IndoorMapId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Markers).WithOne().HasForeignKey(x => x.IndoorMapId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Edges).WithOne().HasForeignKey(x => x.IndoorMapId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<MapNode>(e =>
        {
            e.ToTable("map_nodes", t => t.HasCheckConstraint("CK_map_nodes_xy", "\"X\" BETWEEN 0 AND 1 AND \"Y\" BETWEEN 0 AND 1"));
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.IndoorMapId, x.Id });
            e.Property(x => x.Label).HasMaxLength(200); e.Property(x => x.Type).HasMaxLength(30);
        });
        builder.Entity<MapMarker>(e =>
        {
            e.ToTable("map_markers", t => t.HasCheckConstraint("CK_map_markers_xy", "\"X\" BETWEEN 0 AND 1 AND \"Y\" BETWEEN 0 AND 1"));
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Label).HasMaxLength(200); e.Property(x => x.Type).HasMaxLength(30);
            e.HasOne<MapNode>().WithMany().HasForeignKey(x => new { x.IndoorMapId, x.NodeId })
                .HasPrincipalKey(x => new { x.IndoorMapId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Artifact>().WithMany().HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Gallery>().WithMany().HasForeignKey(x => x.GalleryId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<MapEdge>(e =>
        {
            e.ToTable("map_edges", t => t.HasCheckConstraint("CK_map_edges_distinct", "\"FromNodeId\" <> \"ToNodeId\""));
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.IndoorMapId, x.FromNodeId, x.ToNodeId }).IsUnique();
            e.HasOne<MapNode>().WithMany().HasForeignKey(x => new { x.IndoorMapId, x.FromNodeId })
                .HasPrincipalKey(x => new { x.IndoorMapId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<MapNode>().WithMany().HasForeignKey(x => new { x.IndoorMapId, x.ToNodeId })
                .HasPrincipalKey(x => new { x.IndoorMapId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
