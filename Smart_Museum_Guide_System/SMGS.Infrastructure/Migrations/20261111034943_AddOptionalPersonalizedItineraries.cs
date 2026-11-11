using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMGS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOptionalPersonalizedItineraries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PersonalizedTourId",
                table: "visits",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "personalized_tours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    MuseumId = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personalized_tours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_personalized_tours_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_personalized_tours_museums_MuseumId",
                        column: x => x.MuseumId,
                        principalTable: "museums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_visits_PersonalizedTourId",
                table: "visits",
                column: "PersonalizedTourId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_visits_one_itinerary",
                table: "visits",
                sql: "\"TourId\" IS NULL OR \"PersonalizedTourId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_personalized_tours_MuseumId",
                table: "personalized_tours",
                column: "MuseumId");

            migrationBuilder.CreateIndex(
                name: "IX_personalized_tours_OwnerId_CreatedAt",
                table: "personalized_tours",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_visits_personalized_tours_PersonalizedTourId",
                table: "visits",
                column: "PersonalizedTourId",
                principalTable: "personalized_tours",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_visits_personalized_tours_PersonalizedTourId",
                table: "visits");

            migrationBuilder.DropTable(
                name: "personalized_tours");

            migrationBuilder.DropIndex(
                name: "IX_visits_PersonalizedTourId",
                table: "visits");

            migrationBuilder.DropCheckConstraint(
                name: "CK_visits_one_itinerary",
                table: "visits");

            migrationBuilder.DropColumn(
                name: "PersonalizedTourId",
                table: "visits");
        }
    }
}
