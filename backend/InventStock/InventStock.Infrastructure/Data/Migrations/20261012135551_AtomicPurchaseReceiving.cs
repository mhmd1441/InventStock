using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AtomicPurchaseReceiving : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseReceiptLineId",
                table: "SupplierCostObservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseReceiptLineId",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PurchaseReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SupplierReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseReceipts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReceiptLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageCount = table.Column<int>(type: "integer", nullable: false),
                    BaseUnitsPerPackage = table.Column<int>(type: "integer", nullable: false),
                    BaseQuantity = table.Column<int>(type: "integer", nullable: false),
                    CostPerPackage = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    TaxTreatment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SupplierCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    StockMovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierCostObservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReceiptLines", x => x.Id);
                    table.CheckConstraint("CK_PurchaseReceiptLines_BaseQuantity_Positive", "\"BaseQuantity\" > 0");
                    table.CheckConstraint("CK_PurchaseReceiptLines_BaseUnitsPerPackage_Positive", "\"BaseUnitsPerPackage\" > 0");
                    table.CheckConstraint("CK_PurchaseReceiptLines_CostCurrency_Paired", "(\"CostPerPackage\" IS NULL AND \"Currency\" IS NULL) OR (\"CostPerPackage\" IS NOT NULL AND \"Currency\" IS NOT NULL)");
                    table.CheckConstraint("CK_PurchaseReceiptLines_PackageCount_Positive", "\"PackageCount\" > 0");
                    table.ForeignKey(
                        name: "FK_PurchaseReceiptLines_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReceiptLines_PurchaseReceipts_PurchaseReceiptId",
                        column: x => x.PurchaseReceiptId,
                        principalTable: "PurchaseReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCostObservations_PurchaseReceiptLineId",
                table: "SupplierCostObservations",
                column: "PurchaseReceiptLineId",
                unique: true,
                filter: "\"PurchaseReceiptLineId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_PurchaseReceiptLineId",
                table: "StockMovements",
                column: "PurchaseReceiptLineId",
                unique: true,
                filter: "\"PurchaseReceiptLineId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceiptLines_PartId",
                table: "PurchaseReceiptLines",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceiptLines_PurchaseReceiptId",
                table: "PurchaseReceiptLines",
                column: "PurchaseReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceiptLines_StockMovementId",
                table: "PurchaseReceiptLines",
                column: "StockMovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceiptLines_SupplierCostObservationId",
                table: "PurchaseReceiptLines",
                column: "SupplierCostObservationId",
                unique: true,
                filter: "\"SupplierCostObservationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_OperationId",
                table: "PurchaseReceipts",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_SupplierId_ReceivedAtUtc",
                table: "PurchaseReceipts",
                columns: new[] { "SupplierId", "ReceivedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseReceiptLines");

            migrationBuilder.DropTable(
                name: "PurchaseReceipts");

            migrationBuilder.DropIndex(
                name: "IX_SupplierCostObservations_PurchaseReceiptLineId",
                table: "SupplierCostObservations");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_PurchaseReceiptLineId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "PurchaseReceiptLineId",
                table: "SupplierCostObservations");

            migrationBuilder.DropColumn(
                name: "PurchaseReceiptLineId",
                table: "StockMovements");
        }
    }
}
