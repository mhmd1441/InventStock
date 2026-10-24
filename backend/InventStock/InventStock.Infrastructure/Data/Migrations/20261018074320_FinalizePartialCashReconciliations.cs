using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinalizePartialCashReconciliations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CashReconciliations_CashAccountId_BusinessDate",
                table: "CashReconciliations");

            migrationBuilder.AddColumn<Guid>(
                name: "SupersedesCashReconciliationId",
                table: "CashReconciliations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashReconciliations_CashAccountId_BusinessDate",
                table: "CashReconciliations",
                columns: new[] { "CashAccountId", "BusinessDate" },
                unique: true,
                filter: "\"SupersedesCashReconciliationId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashReconciliations_SupersedesCashReconciliationId",
                table: "CashReconciliations",
                column: "SupersedesCashReconciliationId",
                unique: true,
                filter: "\"SupersedesCashReconciliationId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_CashReconciliations_CashReconciliations_SupersedesCashRecon~",
                table: "CashReconciliations",
                column: "SupersedesCashReconciliationId",
                principalTable: "CashReconciliations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashReconciliations_CashReconciliations_SupersedesCashRecon~",
                table: "CashReconciliations");

            migrationBuilder.DropIndex(
                name: "IX_CashReconciliations_CashAccountId_BusinessDate",
                table: "CashReconciliations");

            migrationBuilder.DropIndex(
                name: "IX_CashReconciliations_SupersedesCashReconciliationId",
                table: "CashReconciliations");

            migrationBuilder.DropColumn(
                name: "SupersedesCashReconciliationId",
                table: "CashReconciliations");

            migrationBuilder.CreateIndex(
                name: "IX_CashReconciliations_CashAccountId_BusinessDate",
                table: "CashReconciliations",
                columns: new[] { "CashAccountId", "BusinessDate" },
                unique: true);
        }
    }
}
