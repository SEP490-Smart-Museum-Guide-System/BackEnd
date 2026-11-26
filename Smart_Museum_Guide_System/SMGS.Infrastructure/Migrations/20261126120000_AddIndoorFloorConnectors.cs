using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261126120000_AddIndoorFloorConnectors")]
public partial class AddIndoorFloorConnectors : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(
            name: "ConnectorCode",
            table: "map_nodes",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "ConnectorCode", table: "map_nodes");
}
