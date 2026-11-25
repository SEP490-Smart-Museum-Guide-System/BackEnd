using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using SMGS.Infrastructure.Data;

#nullable disable

namespace SMGS.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261125023000_AddGuideChatHistory")]
public partial class AddGuideChatHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "guide_chat_messages",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ArtifactId = table.Column<int>(type: "integer", nullable: false),
                MuseumId = table.Column<int>(type: "integer", nullable: false),
                Question = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                Answer = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                Supported = table.Column<bool>(type: "boolean", nullable: false),
                ContentVersionId = table.Column<int>(type: "integer", nullable: true),
                ContentVersionNo = table.Column<int>(type: "integer", nullable: true),
                ContentTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                LanguageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "vi"),
                CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_guide_chat_messages", x => x.Id);
                table.ForeignKey(
                    name: "FK_guide_chat_messages_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_guide_chat_messages_artifacts_ArtifactId",
                    column: x => x.ArtifactId,
                    principalTable: "artifacts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_guide_chat_messages_museums_MuseumId",
                    column: x => x.MuseumId,
                    principalTable: "museums",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_guide_chat_messages_ArtifactId",
            table: "guide_chat_messages",
            column: "ArtifactId");

        migrationBuilder.CreateIndex(
            name: "IX_guide_chat_messages_MuseumId_Supported_CreatedAt",
            table: "guide_chat_messages",
            columns: new[] { "MuseumId", "Supported", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_guide_chat_messages_UserId_ArtifactId_CreatedAt",
            table: "guide_chat_messages",
            columns: new[] { "UserId", "ArtifactId", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "guide_chat_messages");
    }
}
