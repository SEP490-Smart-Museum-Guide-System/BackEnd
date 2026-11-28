using Microsoft.EntityFrameworkCore;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Identity;

namespace SMGS.Infrastructure.Data;

public static class CommerceConfiguration
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<AdmissionTicket>(e =>
        {
            e.ToTable("admission_tickets");
            e.HasKey(x => x.OrderId);
            e.Property(x => x.Code).HasMaxLength(64);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.RefundedAmount).HasPrecision(18, 2);
            e.HasOne<PurchaseOrder>().WithOne().HasForeignKey<AdmissionTicket>(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AdmissionCheckIn>(e =>
        {
            e.ToTable("admission_check_ins");
            e.HasKey(x => new { x.OrderId, x.OperationId });
            e.HasOne<AdmissionTicket>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CommerceResource>(e =>
        {
            e.ToTable("commerce_resources");
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Kind).HasMaxLength(20);
            e.Property(x => x.AccessLevel).HasMaxLength(20);
            e.Property(x => x.ScopeReference).HasMaxLength(200);
            e.HasIndex(x => x.ArtifactId).IsUnique();
            e.HasOne<Museum>().WithMany().HasForeignKey(x => x.MuseumId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Artifact>().WithMany().HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CommerceProduct>(e =>
        {
            e.ToTable("commerce_products");
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Kind).HasMaxLength(20);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.HasOne<Museum>().WithMany().HasForeignKey(x => x.MuseumId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<PurchaseOrder>(e =>
        {
            e.ToTable("purchase_orders");
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.GroupKeyHash).HasMaxLength(64);
            e.Property(x => x.RefundedAmount).HasPrecision(18, 2);
            e.HasIndex(x => x.GroupKeyHash).IsUnique();
            e.HasIndex(x => new { x.BuyerId, x.CreatedAt });
            e.HasIndex(x => new { x.MuseumId, x.PaidAt });
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.BuyerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Museum>().WithMany().HasForeignKey(x => x.MuseumId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CommerceProduct>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<WalletTransaction>(e =>
        {
            e.ToTable("wallet_transactions");
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Type).HasMaxLength(20);
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasIndex(x => x.OrderId).HasFilter("\"OrderId\" IS NOT NULL");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<PaymentAttempt>(e =>
        {
            e.ToTable("payment_attempts");
            e.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ConfirmedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.OrderId, "IX_payment_attempts_pending").IsUnique().HasFilter("\"Status\" = 'pending'");
            e.HasIndex(x => x.OrderId, "IX_payment_attempts_success").IsUnique().HasFilter("\"Status\" = 'succeeded'");
            e.HasIndex(x => x.Reference).IsUnique().HasFilter("\"Reference\" IS NOT NULL");
        });
        b.Entity<MuseumPass>(e =>
        {
            e.ToTable("museum_passes");
            e.HasIndex(x => new { x.OrderId, x.UserId }).IsUnique();
            e.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<PurchaseInvoice>(e =>
        {
            e.ToTable("purchase_invoices");
            e.HasIndex(x => x.OrderId).IsUnique();
            e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
