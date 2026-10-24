using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DailyCashReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashReconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CashAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OpeningAmount = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    OpeningSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpectedClosingAmount = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    CountedAmount = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    DifferenceAmount = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    DifferenceReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CountedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CountedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashReconciliations", x => x.Id);
                    table.CheckConstraint("CK_CashReconciliations_Counted_Nonnegative", "\"CountedAmount\" >= 0");
                    table.CheckConstraint("CK_CashReconciliations_Opening_Nonnegative", "\"OpeningAmount\" >= 0");
                    table.CheckConstraint("CK_CashReconciliations_Reason_When_Different", "\"DifferenceAmount\" = 0 OR \"DifferenceReason\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_CashReconciliations_CashAccounts_CashAccountId",
                        column: x => x.CashAccountId,
                        principalTable: "CashAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashReconciliations_CashAccountId_BusinessDate",
                table: "CashReconciliations",
                columns: new[] { "CashAccountId", "BusinessDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashReconciliations_CashAccountId_ClosedAtUtc",
                table: "CashReconciliations",
                columns: new[] { "CashAccountId", "ClosedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CashReconciliations_OperationId",
                table: "CashReconciliations",
                column: "OperationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashReconciliations");
        }
    }
}
