using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReferenceIdentityAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CatalogueReferences_Kind_Issuer_SearchValue",
                table: "CatalogueReferences");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "SkuReferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "SkuReferences",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "IdentityKey",
                table: "CatalogueReferences",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Preserve surviving display values and IDs. Do not invent previously collapsed spellings.
            migrationBuilder.Sql("""
                UPDATE "CatalogueReferences"
                SET "IdentityKey" = CASE WHEN "Kind" = 'MercedesOE' THEN "SearchValue" ELSE upper(btrim("Value")) END;
                """);

            migrationBuilder.CreateTable(
                name: "ReferenceLinkEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceLinkEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferenceLinkEvents_SkuReferences_PartId_ReferenceId",
                        columns: x => new { x.PartId, x.ReferenceId },
                        principalTable: "SkuReferences",
                        principalColumns: new[] { "PartId", "ReferenceId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogueReferences_Kind_Issuer_IdentityKey",
                table: "CatalogueReferences",
                columns: new[] { "Kind", "Issuer", "IdentityKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceLinkEvents_PartId_ReferenceId_Revision",
                table: "ReferenceLinkEvents",
                columns: new[] { "PartId", "ReferenceId", "Revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Forward-only: a downgrade would lose reference audit history and collapse distinct identities. Use a reviewed backup restore/recovery plan.");
        }
    }
}
