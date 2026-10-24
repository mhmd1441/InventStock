using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CashReconciliationCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPartialDay",
                table: "CashReconciliations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CashReconciliationCorrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CashReconciliationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedClosingAmount = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    CountedAmount = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    DifferenceAmount = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CountedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CorrectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CorrectedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPartialDay = table.Column<bool>(type: "boolean", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashReconciliationCorrections", x => x.Id);
                    table.CheckConstraint("CK_CashReconciliationCorrections_Counted_Nonnegative", "\"CountedAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_CashReconciliationCorrections_CashReconciliations_CashRecon~",
                        column: x => x.CashReconciliationId,
                        principalTable: "CashReconciliations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashReconciliationCorrections_CashReconciliationId_Correcte~",
                table: "CashReconciliationCorrections",
                columns: new[] { "CashReconciliationId", "CorrectedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CashReconciliationCorrections_OperationId",
                table: "CashReconciliationCorrections",
                column: "OperationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashReconciliationCorrections");

            migrationBuilder.DropColumn(
                name: "IsPartialDay",
                table: "CashReconciliations");
        }
    }
}
