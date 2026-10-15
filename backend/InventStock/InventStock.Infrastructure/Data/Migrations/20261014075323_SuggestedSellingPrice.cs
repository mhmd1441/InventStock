using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SuggestedSellingPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PricingPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MarkupPercent = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    TvaPercent = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    EurUsdRate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    LbpPerUsdRate = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingPolicies", x => x.Id);
                    table.CheckConstraint("CK_PricingPolicies_Singleton", "\"Id\" = 1");
                });

            migrationBuilder.InsertData(
                table: "PricingPolicies",
                columns: new[] { "Id", "EurUsdRate", "LbpPerUsdRate", "MarkupPercent", "Revision", "TvaPercent", "UpdatedAtUtc", "UpdatedById" },
                values: new object[] { 1, null, null, 15m, 0, 11m, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PricingPolicies");
        }
    }
}
