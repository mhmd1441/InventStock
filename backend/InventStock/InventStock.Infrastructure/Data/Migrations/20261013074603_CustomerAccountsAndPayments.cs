using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomerAccountsAndPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerEntries_Kind",
                table: "CustomerAccountEntries");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Customers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Customers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerPaymentCorrectionId",
                table: "CustomerAccountEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerPaymentId",
                table: "CustomerAccountEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveAtUtc",
                table: "CustomerAccountEntries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayloadFingerprint",
                table: "CustomerAccountEntries",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "CustomerAccountEntries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerPaymentCorrectionId",
                table: "CashMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerPaymentTenderId",
                table: "CashMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsdCredit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExcessCreditUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceBeforeUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAfterUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPayments", x => x.Id);
                    table.CheckConstraint("CK_CustomerPayments_Balance", "\"UsdCredit\" > 0 AND \"ExcessCreditUsd\" >= 0 AND \"ExcessCreditUsd\" <= \"UsdCredit\" AND \"BalanceAfterUsd\" = \"BalanceBeforeUsd\" - \"UsdCredit\"");
                    table.ForeignKey(
                        name: "FK_CustomerPayments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerProfileEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DistinguishingLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerProfileEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerProfileEvents_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPaymentCorrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalPaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CorrectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RestoredDebtUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAfterUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPaymentCorrections", x => x.Id);
                    table.CheckConstraint("CK_CustomerPaymentCorrections_Positive", "\"RestoredDebtUsd\" > 0");
                    table.ForeignKey(
                        name: "FK_CustomerPaymentCorrections_CustomerPayments_OriginalPayment~",
                        column: x => x.OriginalPaymentId,
                        principalTable: "CustomerPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerPaymentCorrections_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPaymentTenders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerPaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    CashAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    IsChange = table.Column<bool>(type: "boolean", nullable: false),
                    NativeAmount = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    ConfirmedUsdAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPaymentTenders", x => x.Id);
                    table.CheckConstraint("CK_CustomerPaymentTenders_Amounts", "\"NativeAmount\" > 0 AND \"ConfirmedUsdAmount\" > 0 AND (\"Currency\" <> 'USD' OR \"NativeAmount\" = \"ConfirmedUsdAmount\")");
                    table.ForeignKey(
                        name: "FK_CustomerPaymentTenders_CashAccounts_CashAccountId",
                        column: x => x.CashAccountId,
                        principalTable: "CashAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerPaymentTenders_CustomerPayments_CustomerPaymentId",
                        column: x => x.CustomerPaymentId,
                        principalTable: "CustomerPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccountEntries_CustomerId",
                table: "CustomerAccountEntries",
                column: "CustomerId",
                unique: true,
                filter: "\"Kind\" = 'Opening'");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccountEntries_CustomerPaymentCorrectionId",
                table: "CustomerAccountEntries",
                column: "CustomerPaymentCorrectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccountEntries_CustomerPaymentId_Kind",
                table: "CustomerAccountEntries",
                columns: new[] { "CustomerPaymentId", "Kind" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomerEntries_Kind",
                table: "CustomerAccountEntries",
                sql: "(\"Kind\" = 'Opening' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NULL AND \"CustomerPaymentCorrectionId\" IS NULL) OR (\"Kind\" = 'SaleCharge' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" > 0) OR (\"Kind\" = 'SalePayment' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'AccountPayment' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'PaymentCorrection' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NOT NULL AND \"AmountUsd\" > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_CustomerPaymentCorrectionId_CustomerPaymentTe~",
                table: "CashMovements",
                columns: new[] { "CustomerPaymentCorrectionId", "CustomerPaymentTenderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_CustomerPaymentTenderId",
                table: "CashMovements",
                column: "CustomerPaymentTenderId",
                unique: true,
                filter: "\"CustomerPaymentTenderId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentCorrections_CustomerId",
                table: "CustomerPaymentCorrections",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentCorrections_OperationId",
                table: "CustomerPaymentCorrections",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentCorrections_OriginalPaymentId",
                table: "CustomerPaymentCorrections",
                column: "OriginalPaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_CustomerId_PostedAtUtc",
                table: "CustomerPayments",
                columns: new[] { "CustomerId", "PostedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_OperationId",
                table: "CustomerPayments",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentTenders_CashAccountId",
                table: "CustomerPaymentTenders",
                column: "CashAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentTenders_CustomerPaymentId_Position",
                table: "CustomerPaymentTenders",
                columns: new[] { "CustomerPaymentId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProfileEvents_CustomerId_Revision",
                table: "CustomerProfileEvents",
                columns: new[] { "CustomerId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProfileEvents_OperationId",
                table: "CustomerProfileEvents",
                column: "OperationId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CashMovements_CustomerPaymentCorrections_CustomerPaymentCor~",
                table: "CashMovements",
                column: "CustomerPaymentCorrectionId",
                principalTable: "CustomerPaymentCorrections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashMovements_CustomerPaymentTenders_CustomerPaymentTenderId",
                table: "CashMovements",
                column: "CustomerPaymentTenderId",
                principalTable: "CustomerPaymentTenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerAccountEntries_CustomerPaymentCorrections_CustomerP~",
                table: "CustomerAccountEntries",
                column: "CustomerPaymentCorrectionId",
                principalTable: "CustomerPaymentCorrections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerAccountEntries_CustomerPayments_CustomerPaymentId",
                table: "CustomerAccountEntries",
                column: "CustomerPaymentId",
                principalTable: "CustomerPayments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashMovements_CustomerPaymentCorrections_CustomerPaymentCor~",
                table: "CashMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_CashMovements_CustomerPaymentTenders_CustomerPaymentTenderId",
                table: "CashMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerAccountEntries_CustomerPaymentCorrections_CustomerP~",
                table: "CustomerAccountEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerAccountEntries_CustomerPayments_CustomerPaymentId",
                table: "CustomerAccountEntries");

            migrationBuilder.DropTable(
                name: "CustomerPaymentCorrections");

            migrationBuilder.DropTable(
                name: "CustomerPaymentTenders");

            migrationBuilder.DropTable(
                name: "CustomerProfileEvents");

            migrationBuilder.DropTable(
                name: "CustomerPayments");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAccountEntries_CustomerId",
                table: "CustomerAccountEntries");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAccountEntries_CustomerPaymentCorrectionId",
                table: "CustomerAccountEntries");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAccountEntries_CustomerPaymentId_Kind",
                table: "CustomerAccountEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerEntries_Kind",
                table: "CustomerAccountEntries");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_CustomerPaymentCorrectionId_CustomerPaymentTe~",
                table: "CashMovements");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_CustomerPaymentTenderId",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CustomerPaymentCorrectionId",
                table: "CustomerAccountEntries");

            migrationBuilder.DropColumn(
                name: "CustomerPaymentId",
                table: "CustomerAccountEntries");

            migrationBuilder.DropColumn(
                name: "EffectiveAtUtc",
                table: "CustomerAccountEntries");

            migrationBuilder.DropColumn(
                name: "PayloadFingerprint",
                table: "CustomerAccountEntries");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "CustomerAccountEntries");

            migrationBuilder.DropColumn(
                name: "CustomerPaymentCorrectionId",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "CustomerPaymentTenderId",
                table: "CashMovements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomerEntries_Kind",
                table: "CustomerAccountEntries",
                sql: "(\"Kind\" = 'Opening' AND \"SaleId\" IS NULL) OR (\"Kind\" = 'SaleCharge' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" > 0) OR (\"Kind\" = 'SalePayment' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" < 0)");
        }
    }
}
