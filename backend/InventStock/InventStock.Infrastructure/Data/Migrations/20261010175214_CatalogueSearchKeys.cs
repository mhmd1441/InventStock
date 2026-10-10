using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CatalogueSearchKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.AddColumn<string>(
                name: "SearchName",
                table: "Parts",
                type: "text",
                nullable: false,
                computedColumnSql: "upper(\"Name\")",
                stored: true);

            migrationBuilder.AddColumn<string>(
                name: "SearchNumber",
                table: "Parts",
                type: "text",
                nullable: false,
                computedColumnSql: "upper(replace(replace(\"PartNumber\", ' ', ''), '-', ''))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parts_SearchName",
                table: "Parts",
                column: "SearchName")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Parts_SearchNumber_Exact",
                table: "Parts",
                column: "SearchNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_SearchNumber_Trigram",
                table: "Parts",
                column: "SearchNumber")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Parts_SearchName",
                table: "Parts");

            migrationBuilder.DropIndex(
                name: "IX_Parts_SearchNumber_Exact",
                table: "Parts");

            migrationBuilder.DropIndex(
                name: "IX_Parts_SearchNumber_Trigram",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "SearchName",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "SearchNumber",
                table: "Parts");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
