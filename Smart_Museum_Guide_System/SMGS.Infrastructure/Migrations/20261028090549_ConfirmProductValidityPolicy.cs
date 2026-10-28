using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMGS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConfirmProductValidityPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ValidityStartsAt",
                table: "purchase_orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ValidityStartsAt",
                table: "commerce_products",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ValidityStartsAt",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "ValidityStartsAt",
                table: "commerce_products");
        }
    }
}
