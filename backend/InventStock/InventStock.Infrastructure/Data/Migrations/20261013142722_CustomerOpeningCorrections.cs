using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomerOpeningCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerEntries_Kind",
                table: "CustomerAccountEntries");

            migrationBuilder.AddColumn<decimal>(
                name: "BalanceAfterUsd",
                table: "CustomerAccountEntries",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CorrectedOpeningBalanceUsd",
                table: "CustomerAccountEntries",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalOpeningEntryId",
                table: "CustomerAccountEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccountEntries_OriginalOpeningEntryId",
                table: "CustomerAccountEntries",
                column: "OriginalOpeningEntryId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomerEntries_Kind",
                table: "CustomerAccountEntries",
                sql: "(\"Kind\" = 'Opening' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NULL AND \"CustomerPaymentCorrectionId\" IS NULL) OR (\"Kind\" = 'OpeningCorrection' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NULL AND \"CustomerPaymentCorrectionId\" IS NULL AND \"OriginalOpeningEntryId\" IS NOT NULL AND \"CorrectedOpeningBalanceUsd\" IS NOT NULL AND \"BalanceAfterUsd\" IS NOT NULL AND \"AmountUsd\" <> 0) OR (\"Kind\" = 'SaleCharge' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" > 0) OR (\"Kind\" = 'SalePayment' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'AccountPayment' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'PaymentCorrection' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NOT NULL AND \"AmountUsd\" > 0)");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerAccountEntries_CustomerAccountEntries_OriginalOpeni~",
                table: "CustomerAccountEntries",
                column: "OriginalOpeningEntryId",
                principalTable: "CustomerAccountEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerAccountEntries_CustomerAccountEntries_OriginalOpeni~",
                table: "CustomerAccountEntries");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAccountEntries_OriginalOpeningEntryId",
                table: "CustomerAccountEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerEntries_Kind",
                table: "CustomerAccountEntries");

            migrationBuilder.DropColumn(
                name: "BalanceAfterUsd",
                table: "CustomerAccountEntries");

            migrationBuilder.DropColumn(
                name: "CorrectedOpeningBalanceUsd",
                table: "CustomerAccountEntries");

            migrationBuilder.DropColumn(
                name: "OriginalOpeningEntryId",
                table: "CustomerAccountEntries");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomerEntries_Kind",
                table: "CustomerAccountEntries",
                sql: "(\"Kind\" = 'Opening' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NULL AND \"CustomerPaymentCorrectionId\" IS NULL) OR (\"Kind\" = 'SaleCharge' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" > 0) OR (\"Kind\" = 'SalePayment' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'AccountPayment' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'PaymentCorrection' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NOT NULL AND \"AmountUsd\" > 0)");
        }
    }
}
