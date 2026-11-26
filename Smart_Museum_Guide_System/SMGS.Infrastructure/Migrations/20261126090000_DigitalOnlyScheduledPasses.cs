using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMGS.Infrastructure.Migrations;

[Migration("20261126090000_DigitalOnlyScheduledPasses")]
public partial class DigitalOnlyScheduledPasses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE commerce_resources SET \"Kind\" = 'digital' WHERE \"Kind\" <> 'digital';");
        migrationBuilder.Sql("UPDATE commerce_products SET \"Kind\" = 'digital' WHERE \"Kind\" <> 'digital';");
        migrationBuilder.Sql("UPDATE purchase_orders SET \"ProductKind\" = 'digital' WHERE \"ProductKind\" <> 'digital';");
        migrationBuilder.Sql("UPDATE museum_passes p SET \"ClaimedAt\" = (o.\"VisitDate\"::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh'), \"RevokedAt\" = (o.\"VisitDate\"::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh') + (o.\"ValidityHours\" * INTERVAL '1 hour') FROM purchase_orders o WHERE p.\"OrderId\" = o.\"Id\";");
        migrationBuilder.DropColumn(name: "ValidityStartsAt", table: "commerce_products");
        migrationBuilder.DropColumn(name: "ValidityStartsAt", table: "purchase_orders");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "ValidityStartsAt", table: "commerce_products", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ValidityStartsAt", table: "purchase_orders", type: "text", nullable: false, defaultValue: "visit_date");
    }
}
