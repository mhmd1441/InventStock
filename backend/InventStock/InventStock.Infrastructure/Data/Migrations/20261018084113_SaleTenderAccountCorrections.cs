using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SaleTenderAccountCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SaleTenderAccountCorrectionId",
                table: "CashMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SaleTenderAccountCorrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalSaleTenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalCashAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrectCashAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    NativeAmount = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    ConfirmedUsdAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CorrectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CorrectedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleTenderAccountCorrections", x => x.Id);
                    table.CheckConstraint("CK_SaleTenderAccountCorrections_Accounts", "\"OriginalCashAccountId\" <> \"CorrectCashAccountId\"");
                    table.CheckConstraint("CK_SaleTenderAccountCorrections_Amounts", "\"NativeAmount\" > 0 AND \"ConfirmedUsdAmount\" > 0 AND (\"Currency\" <> 'USD' OR \"NativeAmount\" = \"ConfirmedUsdAmount\")");
                    table.ForeignKey(
                        name: "FK_SaleTenderAccountCorrections_CashAccounts_CorrectCashAccoun~",
                        column: x => x.CorrectCashAccountId,
                        principalTable: "CashAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleTenderAccountCorrections_CashAccounts_OriginalCashAccou~",
                        column: x => x.OriginalCashAccountId,
                        principalTable: "CashAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleTenderAccountCorrections_SaleTenders_OriginalSaleTender~",
                        column: x => x.OriginalSaleTenderId,
                        principalTable: "SaleTenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_SaleTenderAccountCorrectionId",
                table: "CashMovements",
                column: "SaleTenderAccountCorrectionId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleTenderAccountCorrections_CorrectCashAccountId",
                table: "SaleTenderAccountCorrections",
                column: "CorrectCashAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleTenderAccountCorrections_OperationId",
                table: "SaleTenderAccountCorrections",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleTenderAccountCorrections_OriginalCashAccountId",
                table: "SaleTenderAccountCorrections",
                column: "OriginalCashAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleTenderAccountCorrections_OriginalSaleTenderId",
                table: "SaleTenderAccountCorrections",
                column: "OriginalSaleTenderId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CashMovements_SaleTenderAccountCorrections_SaleTenderAccoun~",
                table: "CashMovements",
                column: "SaleTenderAccountCorrectionId",
                principalTable: "SaleTenderAccountCorrections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashMovements_SaleTenderAccountCorrections_SaleTenderAccoun~",
                table: "CashMovements");

            migrationBuilder.DropTable(
                name: "SaleTenderAccountCorrections");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_SaleTenderAccountCorrectionId",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "SaleTenderAccountCorrectionId",
                table: "CashMovements");
        }
    }
}
