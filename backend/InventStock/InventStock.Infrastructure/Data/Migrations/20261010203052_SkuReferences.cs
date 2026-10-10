using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SkuReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Parts_PartNumber",
                table: "Parts");

            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "Parts",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Variant",
                table: "Parts",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CatalogueReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Issuer = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Value = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SearchValue = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MatchValue = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogueReferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SkuReferences",
                columns: table => new
                {
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LinkedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkuReferences", x => new { x.PartId, x.ReferenceId });
                    table.ForeignKey(
                        name: "FK_SkuReferences_CatalogueReferences_ReferenceId",
                        column: x => x.ReferenceId,
                        principalTable: "CatalogueReferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SkuReferences_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Parts_PartNumber_Brand_Variant",
                table: "Parts",
                columns: new[] { "PartNumber", "Brand", "Variant" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogueReferences_Kind_Issuer_SearchValue",
                table: "CatalogueReferences",
                columns: new[] { "Kind", "Issuer", "SearchValue" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogueReferences_MatchValue_Exact",
                table: "CatalogueReferences",
                column: "MatchValue");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogueReferences_MatchValue_Trigram",
                table: "CatalogueReferences",
                column: "MatchValue")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogueReferences_SearchValue",
                table: "CatalogueReferences",
                column: "SearchValue")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_SkuReferences_ReferenceId_PartId",
                table: "SkuReferences",
                columns: new[] { "ReferenceId", "PartId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SkuReferences");

            migrationBuilder.DropTable(
                name: "CatalogueReferences");

            migrationBuilder.DropIndex(
                name: "IX_Parts_PartNumber_Brand_Variant",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "Brand",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "Variant",
                table: "Parts");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_PartNumber",
                table: "Parts",
                column: "PartNumber",
                unique: true);
        }
    }
}
