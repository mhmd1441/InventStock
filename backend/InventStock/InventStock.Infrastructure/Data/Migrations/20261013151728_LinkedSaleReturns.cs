using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkedSaleReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerEntries_Kind",
                table: "CustomerAccountEntries");

            migrationBuilder.AddColumn<Guid>(
                name: "SaleReturnId",
                table: "CustomerAccountEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SaleRefundTenderId",
                table: "CashMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SaleReturns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TotalUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AccountReductionUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleReturns", x => x.Id);
                    table.CheckConstraint("CK_SaleReturns_Settlement", "\"TotalUsd\" > 0 AND \"AccountReductionUsd\" >= 0 AND \"AccountReductionUsd\" <= \"TotalUsd\"");
                    table.ForeignKey(
                        name: "FK_SaleReturns_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleReturns_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SaleRefundTenders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    CashAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    NativeAmount = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    ConfirmedUsdAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleRefundTenders", x => x.Id);
                    table.CheckConstraint("CK_SaleRefundTenders_Amount", "\"NativeAmount\" > 0 AND \"ConfirmedUsdAmount\" > 0 AND (\"Currency\" <> 'USD' OR \"NativeAmount\" = \"ConfirmedUsdAmount\")");
                    table.ForeignKey(
                        name: "FK_SaleRefundTenders_CashAccounts_CashAccountId",
                        column: x => x.CashAccountId,
                        principalTable: "CashAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleRefundTenders_SaleReturns_SaleReturnId",
                        column: x => x.SaleReturnId,
                        principalTable: "SaleReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SaleReturnLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    RefundUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Condition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StockMovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockHoldMovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssueReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleReturnLines", x => x.Id);
                    table.CheckConstraint("CK_SaleReturnLines_Quantity", "\"Quantity\" > 0 AND \"RefundUsd\" >= 0 AND ((\"Condition\" = 'Quarantined' AND \"StockHoldMovementId\" IS NOT NULL AND \"IssueReason\" IS NOT NULL) OR (\"Condition\" = 'Sellable' AND \"StockHoldMovementId\" IS NULL))");
                    table.ForeignKey(
                        name: "FK_SaleReturnLines_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleReturnLines_SaleLines_SaleLineId",
                        column: x => x.SaleLineId,
                        principalTable: "SaleLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleReturnLines_SaleReturns_SaleReturnId",
                        column: x => x.SaleReturnId,
                        principalTable: "SaleReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleReturnLines_StockHoldMovements_StockHoldMovementId",
                        column: x => x.StockHoldMovementId,
                        principalTable: "StockHoldMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleReturnLines_StockMovements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalTable: "StockMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccountEntries_SaleReturnId",
                table: "CustomerAccountEntries",
                column: "SaleReturnId",
                unique: true,
                filter: "\"SaleReturnId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomerEntries_Kind",
                table: "CustomerAccountEntries",
                sql: "(\"Kind\" = 'Opening' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NULL AND \"CustomerPaymentCorrectionId\" IS NULL) OR (\"Kind\" = 'OpeningCorrection' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NULL AND \"CustomerPaymentCorrectionId\" IS NULL AND \"OriginalOpeningEntryId\" IS NOT NULL AND \"CorrectedOpeningBalanceUsd\" IS NOT NULL AND \"BalanceAfterUsd\" IS NOT NULL AND \"AmountUsd\" <> 0) OR (\"Kind\" = 'SaleCharge' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" > 0) OR (\"Kind\" = 'SalePayment' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'ReturnReduction' AND \"SaleReturnId\" IS NOT NULL AND \"SaleId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'AccountPayment' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'PaymentCorrection' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NOT NULL AND \"AmountUsd\" > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_SaleRefundTenderId",
                table: "CashMovements",
                column: "SaleRefundTenderId",
                unique: true,
                filter: "\"SaleRefundTenderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SaleRefundTenders_CashAccountId",
                table: "SaleRefundTenders",
                column: "CashAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleRefundTenders_SaleReturnId_Position",
                table: "SaleRefundTenders",
                columns: new[] { "SaleReturnId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnLines_PartId",
                table: "SaleReturnLines",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnLines_SaleLineId",
                table: "SaleReturnLines",
                column: "SaleLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnLines_SaleReturnId_Position",
                table: "SaleReturnLines",
                columns: new[] { "SaleReturnId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnLines_StockHoldMovementId",
                table: "SaleReturnLines",
                column: "StockHoldMovementId",
                unique: true,
                filter: "\"StockHoldMovementId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnLines_StockMovementId",
                table: "SaleReturnLines",
                column: "StockMovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_CustomerId",
                table: "SaleReturns",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_OperationId",
                table: "SaleReturns",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_SaleId_PostedAtUtc",
                table: "SaleReturns",
                columns: new[] { "SaleId", "PostedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_CashMovements_SaleRefundTenders_SaleRefundTenderId",
                table: "CashMovements",
                column: "SaleRefundTenderId",
                principalTable: "SaleRefundTenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerAccountEntries_SaleReturns_SaleReturnId",
                table: "CustomerAccountEntries",
                column: "SaleReturnId",
                principalTable: "SaleReturns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashMovements_SaleRefundTenders_SaleRefundTenderId",
                table: "CashMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerAccountEntries_SaleReturns_SaleReturnId",
                table: "CustomerAccountEntries");

            migrationBuilder.DropTable(
                name: "SaleRefundTenders");

            migrationBuilder.DropTable(
                name: "SaleReturnLines");

            migrationBuilder.DropTable(
                name: "SaleReturns");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAccountEntries_SaleReturnId",
                table: "CustomerAccountEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerEntries_Kind",
                table: "CustomerAccountEntries");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_SaleRefundTenderId",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "SaleReturnId",
                table: "CustomerAccountEntries");

            migrationBuilder.DropColumn(
                name: "SaleRefundTenderId",
                table: "CashMovements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomerEntries_Kind",
                table: "CustomerAccountEntries",
                sql: "(\"Kind\" = 'Opening' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NULL AND \"CustomerPaymentCorrectionId\" IS NULL) OR (\"Kind\" = 'OpeningCorrection' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NULL AND \"CustomerPaymentCorrectionId\" IS NULL AND \"OriginalOpeningEntryId\" IS NOT NULL AND \"CorrectedOpeningBalanceUsd\" IS NOT NULL AND \"BalanceAfterUsd\" IS NOT NULL AND \"AmountUsd\" <> 0) OR (\"Kind\" = 'SaleCharge' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" > 0) OR (\"Kind\" = 'SalePayment' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'AccountPayment' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'PaymentCorrection' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NOT NULL AND \"AmountUsd\" > 0)");
        }
    }
}
