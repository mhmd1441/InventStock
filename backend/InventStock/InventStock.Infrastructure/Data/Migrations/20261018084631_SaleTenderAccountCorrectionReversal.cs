using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SaleTenderAccountCorrectionReversal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SaleTenderAccountCorrectionReversalId",
                table: "CashMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SaleTenderAccountCorrectionReversals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleTenderAccountCorrectionId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_SaleTenderAccountCorrectionReversals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleTenderAccountCorrectionReversals_SaleTenderAccountCorre~",
                        column: x => x.SaleTenderAccountCorrectionId,
                        principalTable: "SaleTenderAccountCorrections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_SaleTenderAccountCorrectionReversalId",
                table: "CashMovements",
                column: "SaleTenderAccountCorrectionReversalId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleTenderAccountCorrectionReversals_OperationId",
                table: "SaleTenderAccountCorrectionReversals",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleTenderAccountCorrectionReversals_SaleTenderAccountCorre~",
                table: "SaleTenderAccountCorrectionReversals",
                column: "SaleTenderAccountCorrectionId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CashMovements_SaleTenderAccountCorrectionReversals_SaleTend~",
                table: "CashMovements",
                column: "SaleTenderAccountCorrectionReversalId",
                principalTable: "SaleTenderAccountCorrectionReversals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashMovements_SaleTenderAccountCorrectionReversals_SaleTend~",
                table: "CashMovements");

            migrationBuilder.DropTable(
                name: "SaleTenderAccountCorrectionReversals");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_SaleTenderAccountCorrectionReversalId",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "SaleTenderAccountCorrectionReversalId",
                table: "CashMovements");
        }
    }
}
