using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ControlledBrands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BrandId",
                table: "Parts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductBrands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBrands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BrandAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductBrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedValue = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrandAliases_ProductBrands_ProductBrandId",
                        column: x => x.ProductBrandId,
                        principalTable: "ProductBrands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Preserve every existing SKU and stock link. Existing non-empty brand snapshots are
            // promoted to canonical records, then parts are linked without changing their IDs.
            migrationBuilder.Sql("""
                INSERT INTO "ProductBrands" ("Id", "Name", "NormalizedName", "IsActive", "CreatedAtUtc")
                SELECT (md5('inventstock-brand:' || normalized))::uuid, normalized, normalized, TRUE, NOW()
                FROM (
                    SELECT DISTINCT upper(regexp_replace(btrim("Brand"), '\s+', ' ', 'g')) AS normalized
                    FROM "Parts"
                    WHERE btrim("Brand") <> ''
                ) existing;

                UPDATE "Parts" p
                SET "BrandId" = b."Id", "Brand" = b."NormalizedName"
                FROM "ProductBrands" b
                WHERE b."NormalizedName" = upper(regexp_replace(btrim(p."Brand"), '\s+', ' ', 'g'))
                  AND btrim(p."Brand") <> '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Parts_BrandId",
                table: "Parts",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_BrandAliases_NormalizedValue",
                table: "BrandAliases",
                column: "NormalizedValue",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BrandAliases_ProductBrandId",
                table: "BrandAliases",
                column: "ProductBrandId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBrands_NormalizedName",
                table: "ProductBrands",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Parts_ProductBrands_BrandId",
                table: "Parts",
                column: "BrandId",
                principalTable: "ProductBrands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Parts_ProductBrands_BrandId",
                table: "Parts");

            migrationBuilder.DropTable(
                name: "BrandAliases");

            migrationBuilder.DropTable(
                name: "ProductBrands");

            migrationBuilder.DropIndex(
                name: "IX_Parts_BrandId",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "BrandId",
                table: "Parts");
        }
    }
}
