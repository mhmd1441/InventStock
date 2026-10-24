using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SupplierPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SupplierPaymentLineId",
                table: "CashMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierPaymentReversalId",
                table: "CashMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SupplierPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceiptReference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierPayments_CashAccounts_CashAccountId",
                        column: x => x.CashAccountId,
                        principalTable: "CashAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierPayments_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierPaymentLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierPaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    AgreedUsdValue = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    CashMovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPaymentLines", x => x.Id);
                    table.CheckConstraint("CK_SupplierPaymentLines_OriginalAmount_Positive", "\"OriginalAmount\" > 0");
                    table.CheckConstraint("CK_SupplierPaymentLines_UsdValue_Positive", "\"AgreedUsdValue\" > 0");
                    table.ForeignKey(
                        name: "FK_SupplierPaymentLines_SupplierPayments_SupplierPaymentId",
                        column: x => x.SupplierPaymentId,
                        principalTable: "SupplierPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierPaymentReversals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierPaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReversedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReversedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPaymentReversals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierPaymentReversals_SupplierPayments_SupplierPaymentId",
                        column: x => x.SupplierPaymentId,
                        principalTable: "SupplierPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_SupplierPaymentLineId",
                table: "CashMovements",
                column: "SupplierPaymentLineId",
                unique: true,
                filter: "\"SupplierPaymentLineId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_SupplierPaymentReversalId",
                table: "CashMovements",
                column: "SupplierPaymentReversalId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPaymentLines_CashMovementId",
                table: "SupplierPaymentLines",
                column: "CashMovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPaymentLines_SupplierPaymentId",
                table: "SupplierPaymentLines",
                column: "SupplierPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPaymentReversals_OperationId",
                table: "SupplierPaymentReversals",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPaymentReversals_SupplierPaymentId",
                table: "SupplierPaymentReversals",
                column: "SupplierPaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_CashAccountId",
                table: "SupplierPayments",
                column: "CashAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_OperationId",
                table: "SupplierPayments",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_SupplierId_PaidOn",
                table: "SupplierPayments",
                columns: new[] { "SupplierId", "PaidOn" });

            migrationBuilder.AddForeignKey(
                name: "FK_CashMovements_SupplierPaymentLines_SupplierPaymentLineId",
                table: "CashMovements",
                column: "SupplierPaymentLineId",
                principalTable: "SupplierPaymentLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashMovements_SupplierPaymentReversals_SupplierPaymentRever~",
                table: "CashMovements",
                column: "SupplierPaymentReversalId",
                principalTable: "SupplierPaymentReversals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashMovements_SupplierPaymentLines_SupplierPaymentLineId",
                table: "CashMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_CashMovements_SupplierPaymentReversals_SupplierPaymentRever~",
                table: "CashMovements");

            migrationBuilder.DropTable(
                name: "SupplierPaymentLines");

            migrationBuilder.DropTable(
                name: "SupplierPaymentReversals");

            migrationBuilder.DropTable(
                name: "SupplierPayments");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_SupplierPaymentLineId",
                table: "CashMovements");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_SupplierPaymentReversalId",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "SupplierPaymentLineId",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "SupplierPaymentReversalId",
                table: "CashMovements");
        }
    }
}
