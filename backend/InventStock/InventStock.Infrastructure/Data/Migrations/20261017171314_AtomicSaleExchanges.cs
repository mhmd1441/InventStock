using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AtomicSaleExchanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Sales_Settlement",
                table: "Sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SaleReturns_Settlement",
                table: "SaleReturns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerEntries_Kind",
                table: "CustomerAccountEntries");

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeCreditUsd",
                table: "Sales",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeAppliedUsd",
                table: "SaleReturns",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "SaleExchangeId",
                table: "CustomerAccountEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SaleExchanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalSaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReplacementSaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReturnTotalUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReturnAppliedUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AccountReductionUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReplacementTotalUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleExchanges", x => x.Id);
                    table.CheckConstraint("CK_SaleExchanges_Customer", "\"CustomerId\" IS NOT NULL OR \"AccountReductionUsd\" = 0");
                    table.CheckConstraint("CK_SaleExchanges_Replacement", "\"ReplacementTotalUsd\" > 0");
                    table.CheckConstraint("CK_SaleExchanges_ReturnSettlement", "\"ReturnTotalUsd\" > 0 AND \"ReturnAppliedUsd\" >= 0 AND \"RefundUsd\" >= 0 AND \"AccountReductionUsd\" >= 0 AND \"ReturnTotalUsd\" = \"ReturnAppliedUsd\" + \"RefundUsd\" + \"AccountReductionUsd\"");
                    table.ForeignKey(
                        name: "FK_SaleExchanges_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleExchanges_SaleReturns_SaleReturnId",
                        column: x => x.SaleReturnId,
                        principalTable: "SaleReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleExchanges_Sales_OriginalSaleId",
                        column: x => x.OriginalSaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleExchanges_Sales_ReplacementSaleId",
                        column: x => x.ReplacementSaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sales_Settlement",
                table: "Sales",
                sql: "\"TotalUsd\" > 0 AND \"PaymentsUsd\" >= \"ChangeUsd\" AND \"ChangeUsd\" >= 0 AND \"ExchangeCreditUsd\" >= 0 AND \"CustomerCreditUsedUsd\" >= 0 AND \"UnpaidDebtUsd\" >= 0 AND \"TotalUsd\" = \"PaymentsUsd\" - \"ChangeUsd\" + \"ExchangeCreditUsd\" + \"CustomerCreditUsedUsd\" + \"UnpaidDebtUsd\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SaleReturns_Settlement",
                table: "SaleReturns",
                sql: "\"TotalUsd\" > 0 AND \"AccountReductionUsd\" >= 0 AND \"ExchangeAppliedUsd\" >= 0 AND \"AccountReductionUsd\" + \"ExchangeAppliedUsd\" <= \"TotalUsd\"");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccountEntries_SaleExchangeId",
                table: "CustomerAccountEntries",
                column: "SaleExchangeId",
                unique: true,
                filter: "\"SaleExchangeId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomerEntries_Kind",
                table: "CustomerAccountEntries",
                sql: "(\"Kind\" = 'Opening' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NULL AND \"CustomerPaymentCorrectionId\" IS NULL) OR (\"Kind\" = 'OpeningCorrection' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NULL AND \"CustomerPaymentCorrectionId\" IS NULL AND \"OriginalOpeningEntryId\" IS NOT NULL AND \"CorrectedOpeningBalanceUsd\" IS NOT NULL AND \"BalanceAfterUsd\" IS NOT NULL AND \"AmountUsd\" <> 0) OR (\"Kind\" = 'SaleCharge' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" > 0) OR (\"Kind\" = 'SalePayment' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'ReturnReduction' AND \"SaleReturnId\" IS NOT NULL AND \"SaleId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'LegacyReturnReduction' AND \"LegacySaleReturnId\" IS NOT NULL AND \"SaleId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'ExchangeReturnValue' AND \"SaleExchangeId\" IS NOT NULL AND \"SaleId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'AccountPayment' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'PaymentCorrection' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NOT NULL AND \"AmountUsd\" > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_SaleExchanges_CustomerId",
                table: "SaleExchanges",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleExchanges_OperationId",
                table: "SaleExchanges",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleExchanges_OriginalSaleId_PostedAtUtc",
                table: "SaleExchanges",
                columns: new[] { "OriginalSaleId", "PostedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleExchanges_ReplacementSaleId",
                table: "SaleExchanges",
                column: "ReplacementSaleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleExchanges_SaleReturnId",
                table: "SaleExchanges",
                column: "SaleReturnId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerAccountEntries_SaleExchanges_SaleExchangeId",
                table: "CustomerAccountEntries",
                column: "SaleExchangeId",
                principalTable: "SaleExchanges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerAccountEntries_SaleExchanges_SaleExchangeId",
                table: "CustomerAccountEntries");

            migrationBuilder.DropTable(
                name: "SaleExchanges");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sales_Settlement",
                table: "Sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SaleReturns_Settlement",
                table: "SaleReturns");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAccountEntries_SaleExchangeId",
                table: "CustomerAccountEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerEntries_Kind",
                table: "CustomerAccountEntries");

            migrationBuilder.DropColumn(
                name: "ExchangeCreditUsd",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ExchangeAppliedUsd",
                table: "SaleReturns");

            migrationBuilder.DropColumn(
                name: "SaleExchangeId",
                table: "CustomerAccountEntries");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sales_Settlement",
                table: "Sales",
                sql: "\"TotalUsd\" > 0 AND \"PaymentsUsd\" >= \"ChangeUsd\" AND \"ChangeUsd\" >= 0 AND \"CustomerCreditUsedUsd\" >= 0 AND \"UnpaidDebtUsd\" >= 0 AND \"TotalUsd\" = \"PaymentsUsd\" - \"ChangeUsd\" + \"CustomerCreditUsedUsd\" + \"UnpaidDebtUsd\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SaleReturns_Settlement",
                table: "SaleReturns",
                sql: "\"TotalUsd\" > 0 AND \"AccountReductionUsd\" >= 0 AND \"AccountReductionUsd\" <= \"TotalUsd\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomerEntries_Kind",
                table: "CustomerAccountEntries",
                sql: "(\"Kind\" = 'Opening' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NULL AND \"CustomerPaymentCorrectionId\" IS NULL) OR (\"Kind\" = 'OpeningCorrection' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NULL AND \"CustomerPaymentCorrectionId\" IS NULL AND \"OriginalOpeningEntryId\" IS NOT NULL AND \"CorrectedOpeningBalanceUsd\" IS NOT NULL AND \"BalanceAfterUsd\" IS NOT NULL AND \"AmountUsd\" <> 0) OR (\"Kind\" = 'SaleCharge' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" > 0) OR (\"Kind\" = 'SalePayment' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'ReturnReduction' AND \"SaleReturnId\" IS NOT NULL AND \"SaleId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'LegacyReturnReduction' AND \"LegacySaleReturnId\" IS NOT NULL AND \"SaleId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'AccountPayment' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'PaymentCorrection' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NOT NULL AND \"AmountUsd\" > 0)");
        }
    }
}
