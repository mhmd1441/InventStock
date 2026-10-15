using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DirectCutHoseSellingRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CutHoseSellingRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    UsdPerMeter = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CutHoseSellingRates", x => x.Id);
                    table.CheckConstraint("CK_CutHoseSellingRates_Rate", "\"Revision\" > 0 AND (\"UsdPerMeter\" IS NULL OR \"UsdPerMeter\" > 0)");
                    table.ForeignKey(
                        name: "FK_CutHoseSellingRates_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CutHoseSellingRates_PartId_Revision",
                table: "CutHoseSellingRates",
                columns: new[] { "PartId", "Revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CutHoseSellingRates");
        }
    }
}
