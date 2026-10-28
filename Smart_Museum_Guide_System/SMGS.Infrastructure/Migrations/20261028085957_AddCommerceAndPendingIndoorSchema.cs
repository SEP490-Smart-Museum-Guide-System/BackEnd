using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SMGS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommerceAndPendingIndoorSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IndoorLayout",
                table: "museums",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.CreateTable(
                name: "buildings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MuseumId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buildings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_buildings_museums_MuseumId",
                        column: x => x.MuseumId,
                        principalTable: "museums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "commerce_products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MuseumId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Seats = table.Column<int>(type: "integer", nullable: false),
                    ValidityHours = table.Column<int>(type: "integer", nullable: true),
                    ResourceIdsJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commerce_products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_commerce_products_museums_MuseumId",
                        column: x => x.MuseumId,
                        principalTable: "museums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "commerce_resources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MuseumId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AccessLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ArtifactId = table.Column<int>(type: "integer", nullable: true),
                    ScopeReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commerce_resources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_commerce_resources_artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalTable: "artifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_commerce_resources_museums_MuseumId",
                        column: x => x.MuseumId,
                        principalTable: "museums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "indoor_maps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MuseumId = table.Column<int>(type: "integer", nullable: false),
                    FloorId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    ImageKey = table.Column<string>(type: "text", nullable: true),
                    ImageWidth = table.Column<int>(type: "integer", nullable: false),
                    ImageHeight = table.Column<int>(type: "integer", nullable: false),
                    PublishedJson = table.Column<string>(type: "jsonb", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_indoor_maps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_indoor_maps_floors_FloorId",
                        column: x => x.FloorId,
                        principalTable: "floors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_indoor_maps_museums_MuseumId",
                        column: x => x.MuseumId,
                        principalTable: "museums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_assignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MuseumId = table.Column<int>(type: "integer", nullable: false),
                    GalleryId = table.Column<int>(type: "integer", nullable: true),
                    ArtifactId = table.Column<int>(type: "integer", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_assignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_assignments_artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalTable: "artifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_assignments_galleries_GalleryId",
                        column: x => x.GalleryId,
                        principalTable: "galleries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_assignments_museums_MuseumId",
                        column: x => x.MuseumId,
                        principalTable: "museums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    MuseumId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    ProductKind = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Seats = table.Column<int>(type: "integer", nullable: false),
                    ValidityHours = table.Column<int>(type: "integer", nullable: false),
                    ResourceIdsJson = table.Column<string>(type: "text", nullable: false),
                    BuyerName = table.Column<string>(type: "text", nullable: false),
                    BuyerDetailsJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GroupKeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_orders_Users_BuyerId",
                        column: x => x.BuyerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_commerce_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "commerce_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_museums_MuseumId",
                        column: x => x.MuseumId,
                        principalTable: "museums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "map_nodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IndoorMapId = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    X = table.Column<double>(type: "double precision", nullable: false),
                    Y = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_map_nodes", x => x.Id);
                    table.UniqueConstraint("AK_map_nodes_IndoorMapId_Id", x => new { x.IndoorMapId, x.Id });
                    table.CheckConstraint("CK_map_nodes_xy", "\"X\" BETWEEN 0 AND 1 AND \"Y\" BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_map_nodes_indoor_maps_IndoorMapId",
                        column: x => x.IndoorMapId,
                        principalTable: "indoor_maps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "museum_passes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_museum_passes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_museum_passes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_museum_passes_purchase_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "purchase_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    ConfirmedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_attempts_Users_ConfirmedBy",
                        column: x => x.ConfirmedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_attempts_purchase_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "purchase_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "text", nullable: false),
                    BuyerName = table.Column<string>(type: "text", nullable: false),
                    BuyerDetailsJson = table.Column<string>(type: "text", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    ProductKind = table.Column<string>(type: "text", nullable: false),
                    Seats = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_invoices_purchase_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "purchase_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "map_edges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IndoorMapId = table.Column<int>(type: "integer", nullable: false),
                    FromNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Bidirectional = table.Column<bool>(type: "boolean", nullable: false),
                    IsAccessible = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_map_edges", x => x.Id);
                    table.CheckConstraint("CK_map_edges_distinct", "\"FromNodeId\" <> \"ToNodeId\"");
                    table.ForeignKey(
                        name: "FK_map_edges_indoor_maps_IndoorMapId",
                        column: x => x.IndoorMapId,
                        principalTable: "indoor_maps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_map_edges_map_nodes_IndoorMapId_FromNodeId",
                        columns: x => new { x.IndoorMapId, x.FromNodeId },
                        principalTable: "map_nodes",
                        principalColumns: new[] { "IndoorMapId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_map_edges_map_nodes_IndoorMapId_ToNodeId",
                        columns: x => new { x.IndoorMapId, x.ToNodeId },
                        principalTable: "map_nodes",
                        principalColumns: new[] { "IndoorMapId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "map_markers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IndoorMapId = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    X = table.Column<double>(type: "double precision", nullable: false),
                    Y = table.Column<double>(type: "double precision", nullable: false),
                    ArtifactId = table.Column<int>(type: "integer", nullable: true),
                    GalleryId = table.Column<int>(type: "integer", nullable: true),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDestination = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_map_markers", x => x.Id);
                    table.CheckConstraint("CK_map_markers_xy", "\"X\" BETWEEN 0 AND 1 AND \"Y\" BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_map_markers_artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalTable: "artifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_map_markers_galleries_GalleryId",
                        column: x => x.GalleryId,
                        principalTable: "galleries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_map_markers_indoor_maps_IndoorMapId",
                        column: x => x.IndoorMapId,
                        principalTable: "indoor_maps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_map_markers_map_nodes_IndoorMapId_NodeId",
                        columns: x => new { x.IndoorMapId, x.NodeId },
                        principalTable: "map_nodes",
                        principalColumns: new[] { "IndoorMapId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_floors_BuildingId_LevelNo",
                table: "floors",
                columns: new[] { "BuildingId", "LevelNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_buildings_MuseumId",
                table: "buildings",
                column: "MuseumId");

            migrationBuilder.CreateIndex(
                name: "IX_commerce_products_MuseumId",
                table: "commerce_products",
                column: "MuseumId");

            migrationBuilder.CreateIndex(
                name: "IX_commerce_resources_ArtifactId",
                table: "commerce_resources",
                column: "ArtifactId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_commerce_resources_MuseumId",
                table: "commerce_resources",
                column: "MuseumId");

            migrationBuilder.CreateIndex(
                name: "IX_indoor_maps_FloorId",
                table: "indoor_maps",
                column: "FloorId",
                unique: true,
                filter: "\"FloorId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_indoor_maps_MuseumId",
                table: "indoor_maps",
                column: "MuseumId",
                unique: true,
                filter: "\"FloorId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_map_edges_IndoorMapId_FromNodeId_ToNodeId",
                table: "map_edges",
                columns: new[] { "IndoorMapId", "FromNodeId", "ToNodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_map_edges_IndoorMapId_ToNodeId",
                table: "map_edges",
                columns: new[] { "IndoorMapId", "ToNodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_map_markers_ArtifactId",
                table: "map_markers",
                column: "ArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_map_markers_GalleryId",
                table: "map_markers",
                column: "GalleryId");

            migrationBuilder.CreateIndex(
                name: "IX_map_markers_IndoorMapId_NodeId",
                table: "map_markers",
                columns: new[] { "IndoorMapId", "NodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_museum_passes_OrderId_UserId",
                table: "museum_passes",
                columns: new[] { "OrderId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_museum_passes_UserId",
                table: "museum_passes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_attempts_ConfirmedBy",
                table: "payment_attempts",
                column: "ConfirmedBy");

            migrationBuilder.CreateIndex(
                name: "IX_payment_attempts_pending",
                table: "payment_attempts",
                column: "OrderId",
                unique: true,
                filter: "\"Status\" = 'pending'");

            migrationBuilder.CreateIndex(
                name: "IX_payment_attempts_Reference",
                table: "payment_attempts",
                column: "Reference",
                unique: true,
                filter: "\"Reference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payment_attempts_success",
                table: "payment_attempts",
                column: "OrderId",
                unique: true,
                filter: "\"Status\" = 'succeeded'");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_Number",
                table: "purchase_invoices",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_OrderId",
                table: "purchase_invoices",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_BuyerId_CreatedAt",
                table: "purchase_orders",
                columns: new[] { "BuyerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_GroupKeyHash",
                table: "purchase_orders",
                column: "GroupKeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_MuseumId_PaidAt",
                table: "purchase_orders",
                columns: new[] { "MuseumId", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_ProductId",
                table: "purchase_orders",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_assignments_ArtifactId",
                table: "staff_assignments",
                column: "ArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_assignments_GalleryId",
                table: "staff_assignments",
                column: "GalleryId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_assignments_MuseumId",
                table: "staff_assignments",
                column: "MuseumId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_assignments_UserId_MuseumId",
                table: "staff_assignments",
                columns: new[] { "UserId", "MuseumId" },
                unique: true,
                filter: "\"GalleryId\" IS NULL AND \"ArtifactId\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_floors_buildings_BuildingId",
                table: "floors",
                column: "BuildingId",
                principalTable: "buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_floors_buildings_BuildingId",
                table: "floors");

            migrationBuilder.DropTable(
                name: "buildings");

            migrationBuilder.DropTable(
                name: "commerce_resources");

            migrationBuilder.DropTable(
                name: "map_edges");

            migrationBuilder.DropTable(
                name: "map_markers");

            migrationBuilder.DropTable(
                name: "museum_passes");

            migrationBuilder.DropTable(
                name: "payment_attempts");

            migrationBuilder.DropTable(
                name: "purchase_invoices");

            migrationBuilder.DropTable(
                name: "staff_assignments");

            migrationBuilder.DropTable(
                name: "map_nodes");

            migrationBuilder.DropTable(
                name: "purchase_orders");

            migrationBuilder.DropTable(
                name: "indoor_maps");

            migrationBuilder.DropTable(
                name: "commerce_products");

            migrationBuilder.DropIndex(
                name: "IX_floors_BuildingId_LevelNo",
                table: "floors");

            migrationBuilder.DropColumn(
                name: "IndoorLayout",
                table: "museums");
        }
    }
}
