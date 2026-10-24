using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PaperRecoveryEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaperRecoveryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaperReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OriginalOccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EnteredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EnteredByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OriginalSellerNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperRecoveryEntries", x => x.Id);
                    table.CheckConstraint("CK_PaperRecoveryEntries_OneTarget", "(\"SaleId\" IS NOT NULL AND \"CustomerPaymentId\" IS NULL) OR (\"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PaperRecoveryEntries_CustomerPayments_CustomerPaymentId",
                        column: x => x.CustomerPaymentId,
                        principalTable: "CustomerPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaperRecoveryEntries_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaperRecoveryEntries_CustomerPaymentId",
                table: "PaperRecoveryEntries",
                column: "CustomerPaymentId",
                unique: true,
                filter: "\"CustomerPaymentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaperRecoveryEntries_PaperReference",
                table: "PaperRecoveryEntries",
                column: "PaperReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaperRecoveryEntries_SaleId",
                table: "PaperRecoveryEntries",
                column: "SaleId",
                unique: true,
                filter: "\"SaleId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaperRecoveryEntries");
        }
    }
}
