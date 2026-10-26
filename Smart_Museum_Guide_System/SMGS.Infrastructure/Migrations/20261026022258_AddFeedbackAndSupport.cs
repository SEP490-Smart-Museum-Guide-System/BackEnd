using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SMGS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackAndSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CityCode",
                table: "museums",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cities",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cities", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "complaints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    VisitorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "New"),
                    AssignedTo = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_complaints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_complaints_Users_AssignedTo",
                        column: x => x.AssignedTo,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_complaints_Users_VisitorId",
                        column: x => x.VisitorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "visits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    VisitorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TourId = table.Column<int>(type: "integer", nullable: true),
                    MuseumId = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_visits_Users_VisitorId",
                        column: x => x.VisitorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_visits_museums_MuseumId",
                        column: x => x.MuseumId,
                        principalTable: "museums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "visit_feedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    VisitId = table.Column<int>(type: "integer", nullable: false),
                    OverallRating = table.Column<int>(type: "integer", nullable: false),
                    ContentRating = table.Column<int>(type: "integer", nullable: true),
                    TourRating = table.Column<int>(type: "integer", nullable: true),
                    ChatbotRating = table.Column<int>(type: "integer", nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ModerationStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Visible"),
                    ModerationNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visit_feedbacks", x => x.Id);
                    table.CheckConstraint("CK_visit_feedbacks_chatbot", "\"ChatbotRating\" IS NULL OR \"ChatbotRating\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_visit_feedbacks_content", "\"ContentRating\" IS NULL OR \"ContentRating\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_visit_feedbacks_overall", "\"OverallRating\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_visit_feedbacks_tour", "\"TourRating\" IS NULL OR \"TourRating\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_visit_feedbacks_visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staff_responses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    FeedbackId = table.Column<int>(type: "integer", nullable: false),
                    RespondedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_responses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_responses_Users_RespondedBy",
                        column: x => x.RespondedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_responses_visit_feedbacks_FeedbackId",
                        column: x => x.FeedbackId,
                        principalTable: "visit_feedbacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "cities",
                columns: new[] { "Code", "Name" },
                values: new object[,]
                {
                    { "01", "Hà Nội" },
                    { "04", "Cao Bằng" },
                    { "08", "Tuyên Quang" },
                    { "11", "Điện Biên" },
                    { "12", "Lai Châu" },
                    { "14", "Sơn La" },
                    { "15", "Lào Cai" },
                    { "19", "Thái Nguyên" },
                    { "20", "Lạng Sơn" },
                    { "22", "Quảng Ninh" },
                    { "24", "Bắc Ninh" },
                    { "25", "Phú Thọ" },
                    { "31", "Hải Phòng" },
                    { "33", "Hưng Yên" },
                    { "37", "Ninh Bình" },
                    { "38", "Thanh Hóa" },
                    { "40", "Nghệ An" },
                    { "42", "Hà Tĩnh" },
                    { "44", "Quảng Trị" },
                    { "46", "Huế" },
                    { "48", "Đà Nẵng" },
                    { "51", "Quảng Ngãi" },
                    { "52", "Gia Lai" },
                    { "56", "Khánh Hòa" },
                    { "66", "Đắk Lắk" },
                    { "68", "Lâm Đồng" },
                    { "75", "Đồng Nai" },
                    { "79", "Hồ Chí Minh" },
                    { "80", "Tây Ninh" },
                    { "82", "Đồng Tháp" },
                    { "86", "Vĩnh Long" },
                    { "91", "An Giang" },
                    { "92", "Cần Thơ" },
                    { "96", "Cà Mau" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_museums_CityCode",
                table: "museums",
                column: "CityCode");

            migrationBuilder.CreateIndex(
                name: "IX_complaints_AssignedTo",
                table: "complaints",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_complaints_Status",
                table: "complaints",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_complaints_VisitorId",
                table: "complaints",
                column: "VisitorId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_responses_FeedbackId",
                table: "staff_responses",
                column: "FeedbackId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_responses_RespondedBy",
                table: "staff_responses",
                column: "RespondedBy");

            migrationBuilder.CreateIndex(
                name: "IX_visit_feedbacks_VisitId",
                table: "visit_feedbacks",
                column: "VisitId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_visits_MuseumId",
                table: "visits",
                column: "MuseumId");

            migrationBuilder.CreateIndex(
                name: "IX_visits_VisitorId_MuseumId_Status",
                table: "visits",
                columns: new[] { "VisitorId", "MuseumId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_museums_cities_CityCode",
                table: "museums",
                column: "CityCode",
                principalTable: "cities",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_museums_cities_CityCode",
                table: "museums");

            migrationBuilder.DropTable(
                name: "cities");

            migrationBuilder.DropTable(
                name: "complaints");

            migrationBuilder.DropTable(
                name: "staff_responses");

            migrationBuilder.DropTable(
                name: "visit_feedbacks");

            migrationBuilder.DropTable(
                name: "visits");

            migrationBuilder.DropIndex(
                name: "IX_museums_CityCode",
                table: "museums");

            migrationBuilder.DropColumn(
                name: "CityCode",
                table: "museums");
        }
    }
}
