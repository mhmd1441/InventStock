using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SalePriceGuidanceSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PriceOverridden",
                table: "SaleLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SuggestedUnitPriceUsd",
                table: "SaleLines",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SaleLines_PriceOverride",
                table: "SaleLines",
                sql: "(\"SuggestedUnitPriceUsd\" IS NULL AND \"PriceOverridden\" = FALSE) OR (\"SuggestedUnitPriceUsd\" > 0 AND \"PriceOverridden\" = (\"UnitSellingPriceUsd\" <> \"SuggestedUnitPriceUsd\"))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SaleLines_PriceOverride",
                table: "SaleLines");

            migrationBuilder.DropColumn(
                name: "PriceOverridden",
                table: "SaleLines");

            migrationBuilder.DropColumn(
                name: "SuggestedUnitPriceUsd",
                table: "SaleLines");
        }
    }
}
