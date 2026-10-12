using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class StockUnitsAndConfidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovements_QuantityChange_NotZero",
                table: "StockMovements");

            migrationBuilder.AddColumn<int>(
                name: "BookQuantityBeforeCount",
                table: "StockMovements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CountedQuantity",
                table: "StockMovements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StockUnit",
                table: "Parts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Piece");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovements_QuantityChange_NotZero",
                table: "StockMovements",
                sql: "\"QuantityChange\" <> 0 OR \"MovementType\" = 'PhysicalCount'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovements_QuantityChange_NotZero",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "BookQuantityBeforeCount",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "CountedQuantity",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "StockUnit",
                table: "Parts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovements_QuantityChange_NotZero",
                table: "StockMovements",
                sql: "\"QuantityChange\" <> 0");
        }
    }
}
