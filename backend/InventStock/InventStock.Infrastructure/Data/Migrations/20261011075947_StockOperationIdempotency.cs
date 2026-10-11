using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class StockOperationIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OperationId",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayloadFingerprint",
                table: "StockMovements",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_OperationId",
                table: "StockMovements",
                column: "OperationId",
                unique: true,
                filter: "\"OperationId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMovements_OperationId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "OperationId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "PayloadFingerprint",
                table: "StockMovements");
        }
    }
}
