using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RequiredBrandIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Parts_PartNumber_Brand_Variant",
                table: "Parts");

            migrationBuilder.InsertData(
                table: "ProductBrands",
                columns: new[] { "Id", "CreatedAtUtc", "CreatedByUserId", "IsActive", "Name", "NormalizedName", "UpdatedAtUtc", "UpdatedByUserId" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2026, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Unknown / not set", "UNKNOWN / NOT SET", null, null });

            migrationBuilder.Sql("""
                UPDATE "Parts"
                SET "BrandId" = '00000000-0000-0000-0000-000000000001'
                WHERE "BrandId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "BrandId",
                table: "Parts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000001"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parts_PartNumber_BrandId_Variant",
                table: "Parts",
                columns: new[] { "PartNumber", "BrandId", "Variant" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Parts_PartNumber_BrandId_Variant",
                table: "Parts");

            migrationBuilder.AlterColumn<Guid>(
                name: "BrandId",
                table: "Parts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.Sql("""
                UPDATE "Parts"
                SET "BrandId" = NULL
                WHERE "BrandId" = '00000000-0000-0000-0000-000000000001';
                """);

            migrationBuilder.DeleteData(
                table: "ProductBrands",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.CreateIndex(
                name: "IX_Parts_PartNumber_Brand_Variant",
                table: "Parts",
                columns: new[] { "PartNumber", "Brand", "Variant" },
                unique: true);
        }
    }
}
