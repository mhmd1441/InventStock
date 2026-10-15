using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LegacySaleReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerEntries_Kind",
                table: "CustomerAccountEntries");

            migrationBuilder.AddColumn<Guid>(
                name: "LegacySaleReturnId",
                table: "CustomerAccountEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LegacySaleRefundTenderId",
                table: "CashMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LegacySaleReturns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OriginalSaleDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OriginalReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_LegacySaleReturns", x => x.Id);
                    table.CheckConstraint("CK_LegacySaleReturns_Settlement", "\"TotalUsd\" > 0 AND \"AccountReductionUsd\" >= 0 AND \"AccountReductionUsd\" <= \"TotalUsd\"");
                    table.ForeignKey(
                        name: "FK_LegacySaleReturns_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegacySaleRefundTenders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LegacySaleReturnId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_LegacySaleRefundTenders", x => x.Id);
                    table.CheckConstraint("CK_LegacySaleRefundTenders_Amount", "\"NativeAmount\" > 0 AND \"ConfirmedUsdAmount\" > 0 AND (\"Currency\" <> 'USD' OR \"NativeAmount\" = \"ConfirmedUsdAmount\")");
                    table.ForeignKey(
                        name: "FK_LegacySaleRefundTenders_CashAccounts_CashAccountId",
                        column: x => x.CashAccountId,
                        principalTable: "CashAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegacySaleRefundTenders_LegacySaleReturns_LegacySaleReturnId",
                        column: x => x.LegacySaleReturnId,
                        principalTable: "LegacySaleReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegacySaleReturnLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LegacySaleReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    PartNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Variant = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StockUnit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_LegacySaleReturnLines", x => x.Id);
                    table.CheckConstraint("CK_LegacySaleReturnLines_Quantity", "\"Quantity\" > 0 AND \"RefundUsd\" > 0 AND ((\"Condition\" = 'Quarantined' AND \"StockHoldMovementId\" IS NOT NULL AND \"IssueReason\" IS NOT NULL) OR (\"Condition\" = 'Sellable' AND \"StockHoldMovementId\" IS NULL))");
                    table.ForeignKey(
                        name: "FK_LegacySaleReturnLines_LegacySaleReturns_LegacySaleReturnId",
                        column: x => x.LegacySaleReturnId,
                        principalTable: "LegacySaleReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegacySaleReturnLines_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegacySaleReturnLines_StockHoldMovements_StockHoldMovementId",
                        column: x => x.StockHoldMovementId,
                        principalTable: "StockHoldMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegacySaleReturnLines_StockMovements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalTable: "StockMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccountEntries_LegacySaleReturnId",
                table: "CustomerAccountEntries",
                column: "LegacySaleReturnId",
                unique: true,
                filter: "\"LegacySaleReturnId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomerEntries_Kind",
                table: "CustomerAccountEntries",
                sql: "(\"Kind\" = 'Opening' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NULL AND \"CustomerPaymentCorrectionId\" IS NULL) OR (\"Kind\" = 'OpeningCorrection' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NULL AND \"CustomerPaymentCorrectionId\" IS NULL AND \"OriginalOpeningEntryId\" IS NOT NULL AND \"CorrectedOpeningBalanceUsd\" IS NOT NULL AND \"BalanceAfterUsd\" IS NOT NULL AND \"AmountUsd\" <> 0) OR (\"Kind\" = 'SaleCharge' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" > 0) OR (\"Kind\" = 'SalePayment' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'ReturnReduction' AND \"SaleReturnId\" IS NOT NULL AND \"SaleId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'LegacyReturnReduction' AND \"LegacySaleReturnId\" IS NOT NULL AND \"SaleId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'AccountPayment' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'PaymentCorrection' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NOT NULL AND \"AmountUsd\" > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_LegacySaleRefundTenderId",
                table: "CashMovements",
                column: "LegacySaleRefundTenderId",
                unique: true,
                filter: "\"LegacySaleRefundTenderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LegacySaleRefundTenders_CashAccountId",
                table: "LegacySaleRefundTenders",
                column: "CashAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LegacySaleRefundTenders_LegacySaleReturnId_Position",
                table: "LegacySaleRefundTenders",
                columns: new[] { "LegacySaleReturnId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegacySaleReturnLines_LegacySaleReturnId_Position",
                table: "LegacySaleReturnLines",
                columns: new[] { "LegacySaleReturnId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegacySaleReturnLines_PartId_CreatedAtUtc",
                table: "LegacySaleReturnLines",
                columns: new[] { "PartId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LegacySaleReturnLines_StockHoldMovementId",
                table: "LegacySaleReturnLines",
                column: "StockHoldMovementId",
                unique: true,
                filter: "\"StockHoldMovementId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LegacySaleReturnLines_StockMovementId",
                table: "LegacySaleReturnLines",
                column: "StockMovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegacySaleReturns_CustomerId_PostedAtUtc",
                table: "LegacySaleReturns",
                columns: new[] { "CustomerId", "PostedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LegacySaleReturns_OperationId",
                table: "LegacySaleReturns",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegacySaleReturns_PostedAtUtc",
                table: "LegacySaleReturns",
                column: "PostedAtUtc");

            migrationBuilder.AddForeignKey(
                name: "FK_CashMovements_LegacySaleRefundTenders_LegacySaleRefundTende~",
                table: "CashMovements",
                column: "LegacySaleRefundTenderId",
                principalTable: "LegacySaleRefundTenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerAccountEntries_LegacySaleReturns_LegacySaleReturnId",
                table: "CustomerAccountEntries",
                column: "LegacySaleReturnId",
                principalTable: "LegacySaleReturns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashMovements_LegacySaleRefundTenders_LegacySaleRefundTende~",
                table: "CashMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerAccountEntries_LegacySaleReturns_LegacySaleReturnId",
                table: "CustomerAccountEntries");

            migrationBuilder.DropTable(
                name: "LegacySaleRefundTenders");

            migrationBuilder.DropTable(
                name: "LegacySaleReturnLines");

            migrationBuilder.DropTable(
                name: "LegacySaleReturns");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAccountEntries_LegacySaleReturnId",
                table: "CustomerAccountEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerEntries_Kind",
                table: "CustomerAccountEntries");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_LegacySaleRefundTenderId",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "LegacySaleReturnId",
                table: "CustomerAccountEntries");

            migrationBuilder.DropColumn(
                name: "LegacySaleRefundTenderId",
                table: "CashMovements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomerEntries_Kind",
                table: "CustomerAccountEntries",
                sql: "(\"Kind\" = 'Opening' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NULL AND \"CustomerPaymentCorrectionId\" IS NULL) OR (\"Kind\" = 'OpeningCorrection' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NULL AND \"CustomerPaymentCorrectionId\" IS NULL AND \"OriginalOpeningEntryId\" IS NOT NULL AND \"CorrectedOpeningBalanceUsd\" IS NOT NULL AND \"BalanceAfterUsd\" IS NOT NULL AND \"AmountUsd\" <> 0) OR (\"Kind\" = 'SaleCharge' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" > 0) OR (\"Kind\" = 'SalePayment' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'ReturnReduction' AND \"SaleReturnId\" IS NOT NULL AND \"SaleId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'AccountPayment' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'PaymentCorrection' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NOT NULL AND \"AmountUsd\" > 0)");
        }
    }
}
