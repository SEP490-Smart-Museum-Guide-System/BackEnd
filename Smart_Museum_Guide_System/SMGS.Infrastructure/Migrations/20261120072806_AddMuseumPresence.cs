using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SMGS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMuseumPresence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "museum_presence",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MuseumId = table.Column<int>(type: "integer", nullable: false),
                    VisitId = table.Column<int>(type: "integer", nullable: true),
                    MapId = table.Column<int>(type: "integer", nullable: false),
                    MapRevision = table.Column<int>(type: "integer", nullable: false),
                    MarkerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_museum_presence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_museum_presence_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_museum_presence_indoor_maps_MapId",
                        column: x => x.MapId,
                        principalTable: "indoor_maps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_museum_presence_museums_MuseumId",
                        column: x => x.MuseumId,
                        principalTable: "museums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_museum_presence_visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_museum_presence_MapId",
                table: "museum_presence",
                column: "MapId");

            migrationBuilder.CreateIndex(
                name: "IX_museum_presence_MuseumId",
                table: "museum_presence",
                column: "MuseumId");

            migrationBuilder.CreateIndex(
                name: "IX_museum_presence_UserId_MuseumId_ObservedAt",
                table: "museum_presence",
                columns: new[] { "UserId", "MuseumId", "ObservedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_museum_presence_UserId_RequestId",
                table: "museum_presence",
                columns: new[] { "UserId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_museum_presence_VisitId",
                table: "museum_presence",
                column: "VisitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "museum_presence");
        }
    }
}
