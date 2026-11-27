using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261127120000_AddVisitorHome")]
public partial class AddVisitorHome : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "visitor_home",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false),
                Revision = table.Column<Guid>(nullable: false),
                Title = table.Column<string>(nullable: false),
                SlidesJson = table.Column<string>(nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_visitor_home", x => x.Id));
        migrationBuilder.Sql("""INSERT INTO visitor_home ("Id", "Revision", "Title", "SlidesJson") VALUES (1, '00000000-0000-0000-0000-000000000000', '', '[]');""");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable("visitor_home");
}
