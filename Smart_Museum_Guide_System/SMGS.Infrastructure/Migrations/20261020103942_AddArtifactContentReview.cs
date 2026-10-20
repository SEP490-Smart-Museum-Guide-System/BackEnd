using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SMGS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArtifactContentReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "artifact_contents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    ArtifactId = table.Column<int>(type: "integer", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    Revision = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artifact_contents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_artifact_contents_artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalTable: "artifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "artifact_content_versions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    ArtifactContentId = table.Column<int>(type: "integer", nullable: false),
                    VersionNo = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsAiGenerated = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artifact_content_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_artifact_content_versions_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_artifact_content_versions_Users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_artifact_content_versions_artifact_contents_ArtifactContent~",
                        column: x => x.ArtifactContentId,
                        principalTable: "artifact_contents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[] { new Guid("c1e5f237-c782-4910-9235-5d73ecdd3005"), "seed-curator-v1", "Curator", "CURATOR" });

            migrationBuilder.CreateIndex(
                name: "IX_artifact_content_versions_ArtifactContentId",
                table: "artifact_content_versions",
                column: "ArtifactContentId",
                unique: true,
                filter: "\"IsCurrent\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_artifact_content_versions_ArtifactContentId_VersionNo",
                table: "artifact_content_versions",
                columns: new[] { "ArtifactContentId", "VersionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_artifact_content_versions_CreatedBy",
                table: "artifact_content_versions",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_artifact_content_versions_ReviewedBy",
                table: "artifact_content_versions",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_artifact_contents_ArtifactId_LanguageCode",
                table: "artifact_contents",
                columns: new[] { "ArtifactId", "LanguageCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "artifact_content_versions");

            migrationBuilder.DropTable(
                name: "artifact_contents");

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("c1e5f237-c782-4910-9235-5d73ecdd3005"));
        }
    }
}
