using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InternalSaleReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "SaleReceiptNumbers",
                startValue: 100000L);

            migrationBuilder.AddColumn<long>(
                name: "ReceiptNumber",
                table: "Sales",
                type: "bigint",
                nullable: false,
                defaultValueSql: "nextval('\"SaleReceiptNumbers\"')");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_ReceiptNumber",
                table: "Sales",
                column: "ReceiptNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_ReceiptNumber",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ReceiptNumber",
                table: "Sales");

            migrationBuilder.DropSequence(
                name: "SaleReceiptNumbers");
        }
    }
}
