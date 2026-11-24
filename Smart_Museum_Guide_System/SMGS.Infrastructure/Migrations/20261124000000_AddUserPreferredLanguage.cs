using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SMGS.Infrastructure.Data;

#nullable disable

namespace SMGS.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261124000000_AddUserPreferredLanguage")]
public partial class AddUserPreferredLanguage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<string>(
            name: "PreferredLanguage",
            table: "Users",
            type: "character varying(2)",
            maxLength: 2,
            nullable: false,
            defaultValue: "vi");

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(name: "PreferredLanguage", table: "Users");
}
