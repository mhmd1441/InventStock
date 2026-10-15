using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SaleStockDiscrepancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SaleLines_PartId",
                table: "SaleLines");

            migrationBuilder.AddColumn<bool>(
                name: "StockShortage",
                table: "SaleLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsRecount",
                table: "Parts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_SaleLines_PartId",
                table: "SaleLines",
                column: "PartId",
                filter: "\"StockShortage\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SaleLines_PartId",
                table: "SaleLines");

            migrationBuilder.DropColumn(
                name: "StockShortage",
                table: "SaleLines");

            migrationBuilder.DropColumn(
                name: "NeedsRecount",
                table: "Parts");

            migrationBuilder.CreateIndex(
                name: "IX_SaleLines_PartId",
                table: "SaleLines",
                column: "PartId");
        }
    }
}
