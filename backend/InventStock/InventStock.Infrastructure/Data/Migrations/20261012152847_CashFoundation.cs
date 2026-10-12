using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CashFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CashAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovementType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AmountChange = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OpeningBalanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutgoingPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutgoingPaymentReversalId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashMovements", x => x.Id);
                    table.CheckConstraint("CK_CashMovements_Amount_NotZero_ExceptOpening", "\"AmountChange\" <> 0 OR \"MovementType\" = 'OpeningBalance'");
                    table.ForeignKey(
                        name: "FK_CashMovements_CashAccounts_CashAccountId",
                        column: x => x.CashAccountId,
                        principalTable: "CashAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashOpeningBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CashAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountedAmount = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    CountedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CountedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CashMovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashOpeningBalances", x => x.Id);
                    table.CheckConstraint("CK_CashOpeningBalances_Amount_Nonnegative", "\"CountedAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_CashOpeningBalances_CashAccounts_CashAccountId",
                        column: x => x.CashAccountId,
                        principalTable: "CashAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OutgoingPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    FundingSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CashAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalAmount = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    AgreedUsdValue = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Payee = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ForPeriod = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ReceiptReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CashMovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutgoingPayments", x => x.Id);
                    table.CheckConstraint("CK_OutgoingPayments_Amount_Positive", "\"OriginalAmount\" > 0");
                    table.CheckConstraint("CK_OutgoingPayments_FundingAccount", "(\"FundingSource\" = 'ShopAccount' AND \"CashAccountId\" IS NOT NULL) OR (\"FundingSource\" = 'PersonalPocket' AND \"CashAccountId\" IS NULL)");
                    table.CheckConstraint("CK_OutgoingPayments_UsdValue_Positive", "\"AgreedUsdValue\" > 0");
                    table.ForeignKey(
                        name: "FK_OutgoingPayments_CashAccounts_CashAccountId",
                        column: x => x.CashAccountId,
                        principalTable: "CashAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OutgoingPayments_ExpenseCategories_ExpenseCategoryId",
                        column: x => x.ExpenseCategoryId,
                        principalTable: "ExpenseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OutgoingPaymentReversals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OutgoingPaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReversedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReversedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CashMovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutgoingPaymentReversals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutgoingPaymentReversals_OutgoingPayments_OutgoingPaymentId",
                        column: x => x.OutgoingPaymentId,
                        principalTable: "OutgoingPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashAccounts_Code",
                table: "CashAccounts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_CashAccountId_OccurredAtUtc",
                table: "CashMovements",
                columns: new[] { "CashAccountId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_OpeningBalanceId",
                table: "CashMovements",
                column: "OpeningBalanceId",
                unique: true,
                filter: "\"OpeningBalanceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_OutgoingPaymentId",
                table: "CashMovements",
                column: "OutgoingPaymentId",
                unique: true,
                filter: "\"OutgoingPaymentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_OutgoingPaymentReversalId",
                table: "CashMovements",
                column: "OutgoingPaymentReversalId",
                unique: true,
                filter: "\"OutgoingPaymentReversalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashOpeningBalances_CashAccountId",
                table: "CashOpeningBalances",
                column: "CashAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashOpeningBalances_CashMovementId",
                table: "CashOpeningBalances",
                column: "CashMovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashOpeningBalances_OperationId",
                table: "CashOpeningBalances",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_NormalizedName",
                table: "ExpenseCategories",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingPaymentReversals_CashMovementId",
                table: "OutgoingPaymentReversals",
                column: "CashMovementId",
                unique: true,
                filter: "\"CashMovementId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingPaymentReversals_OperationId",
                table: "OutgoingPaymentReversals",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingPaymentReversals_OutgoingPaymentId",
                table: "OutgoingPaymentReversals",
                column: "OutgoingPaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingPayments_CashAccountId",
                table: "OutgoingPayments",
                column: "CashAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingPayments_CashMovementId",
                table: "OutgoingPayments",
                column: "CashMovementId",
                unique: true,
                filter: "\"CashMovementId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingPayments_ExpenseCategoryId",
                table: "OutgoingPayments",
                column: "ExpenseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingPayments_OperationId",
                table: "OutgoingPayments",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingPayments_PaidOn_ExpenseCategoryId",
                table: "OutgoingPayments",
                columns: new[] { "PaidOn", "ExpenseCategoryId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashMovements");

            migrationBuilder.DropTable(
                name: "CashOpeningBalances");

            migrationBuilder.DropTable(
                name: "OutgoingPaymentReversals");

            migrationBuilder.DropTable(
                name: "OutgoingPayments");

            migrationBuilder.DropTable(
                name: "CashAccounts");

            migrationBuilder.DropTable(
                name: "ExpenseCategories");
        }
    }
}
