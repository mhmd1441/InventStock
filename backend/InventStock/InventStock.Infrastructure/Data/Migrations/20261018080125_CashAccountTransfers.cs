using System;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventStock.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261018080125_CashAccountTransfers")]
public partial class CashAccountTransfers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("CashTransferId", "CashMovements", "uuid", nullable: true);
        migrationBuilder.AddColumn<Guid>("CashTransferReversalId", "CashMovements", "uuid", nullable: true);
        migrationBuilder.CreateTable("CashTransfers", columns: table => new
        {
            Id = table.Column<Guid>("uuid", nullable: false), SourceCashAccountId = table.Column<Guid>("uuid", nullable: false), DestinationCashAccountId = table.Column<Guid>("uuid", nullable: false), FeeCashAccountId = table.Column<Guid>("uuid", nullable: true),
            TransferredOn = table.Column<DateOnly>("date", nullable: false), SourceAmount = table.Column<decimal>("numeric(20,6)", nullable: false), SourceCurrency = table.Column<string>("character varying(8)", maxLength: 8, nullable: false), DestinationAmount = table.Column<decimal>("numeric(20,6)", nullable: false), DestinationCurrency = table.Column<string>("character varying(8)", maxLength: 8, nullable: false), AgreedRate = table.Column<decimal>("numeric(20,10)", nullable: false), FeeAmount = table.Column<decimal>("numeric(20,6)", nullable: true), FeeCurrency = table.Column<string>("character varying(8)", maxLength: 8, nullable: true), Note = table.Column<string>("character varying(500)", maxLength: 500, nullable: true),
            RecordedByUserId = table.Column<Guid>("uuid", nullable: false), OperationId = table.Column<Guid>("uuid", nullable: false), PayloadFingerprint = table.Column<string>("character varying(64)", maxLength: 64, nullable: false), CreatedAtUtc = table.Column<DateTime>("timestamp with time zone", nullable: false), CreatedByUserId = table.Column<Guid>("uuid", nullable: true), UpdatedAtUtc = table.Column<DateTime>("timestamp with time zone", nullable: true), UpdatedByUserId = table.Column<Guid>("uuid", nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_CashTransfers", x => x.Id); table.CheckConstraint("CK_CashTransfers_Source_Positive", "\"SourceAmount\" > 0"); table.CheckConstraint("CK_CashTransfers_Destination_Positive", "\"DestinationAmount\" > 0"); table.CheckConstraint("CK_CashTransfers_Rate_Positive", "\"AgreedRate\" > 0");
            table.ForeignKey("FK_CashTransfers_CashAccounts_SourceCashAccountId", x => x.SourceCashAccountId, "CashAccounts", "Id", onDelete: ReferentialAction.Restrict); table.ForeignKey("FK_CashTransfers_CashAccounts_DestinationCashAccountId", x => x.DestinationCashAccountId, "CashAccounts", "Id", onDelete: ReferentialAction.Restrict); table.ForeignKey("FK_CashTransfers_CashAccounts_FeeCashAccountId", x => x.FeeCashAccountId, "CashAccounts", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateTable("CashTransferReversals", columns: table => new
        {
            Id = table.Column<Guid>("uuid", nullable: false), CashTransferId = table.Column<Guid>("uuid", nullable: false), Reason = table.Column<string>("character varying(500)", maxLength: 500, nullable: false), ReversedAtUtc = table.Column<DateTime>("timestamp with time zone", nullable: false), ReversedByUserId = table.Column<Guid>("uuid", nullable: false), OperationId = table.Column<Guid>("uuid", nullable: false), PayloadFingerprint = table.Column<string>("character varying(64)", maxLength: 64, nullable: false), CreatedAtUtc = table.Column<DateTime>("timestamp with time zone", nullable: false), CreatedByUserId = table.Column<Guid>("uuid", nullable: true), UpdatedAtUtc = table.Column<DateTime>("timestamp with time zone", nullable: true), UpdatedByUserId = table.Column<Guid>("uuid", nullable: true)
        }, constraints: table => { table.PrimaryKey("PK_CashTransferReversals", x => x.Id); table.ForeignKey("FK_CashTransferReversals_CashTransfers_CashTransferId", x => x.CashTransferId, "CashTransfers", "Id", onDelete: ReferentialAction.Restrict); });
        migrationBuilder.CreateIndex("IX_CashMovements_CashTransferId", "CashMovements", "CashTransferId"); migrationBuilder.CreateIndex("IX_CashMovements_CashTransferReversalId", "CashMovements", "CashTransferReversalId");
        migrationBuilder.CreateIndex("IX_CashTransfers_OperationId", "CashTransfers", "OperationId", unique: true); migrationBuilder.CreateIndex("IX_CashTransfers_TransferredOn_SourceCashAccountId", "CashTransfers", new[] { "TransferredOn", "SourceCashAccountId" }); migrationBuilder.CreateIndex("IX_CashTransfers_DestinationCashAccountId", "CashTransfers", "DestinationCashAccountId"); migrationBuilder.CreateIndex("IX_CashTransfers_FeeCashAccountId", "CashTransfers", "FeeCashAccountId");
        migrationBuilder.CreateIndex("IX_CashTransferReversals_OperationId", "CashTransferReversals", "OperationId", unique: true); migrationBuilder.CreateIndex("IX_CashTransferReversals_CashTransferId", "CashTransferReversals", "CashTransferId", unique: true);
        migrationBuilder.AddForeignKey(name: "FK_CashMovements_CashTransfers_CashTransferId", table: "CashMovements", column: "CashTransferId", principalTable: "CashTransfers", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_CashMovements_CashTransferReversals_CashTransferReversalId", table: "CashMovements", column: "CashTransferReversalId", principalTable: "CashTransferReversals", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_CashMovements_CashTransfers_CashTransferId", "CashMovements"); migrationBuilder.DropForeignKey("FK_CashMovements_CashTransferReversals_CashTransferReversalId", "CashMovements"); migrationBuilder.DropTable("CashTransferReversals"); migrationBuilder.DropTable("CashTransfers"); migrationBuilder.DropColumn("CashTransferId", "CashMovements"); migrationBuilder.DropColumn("CashTransferReversalId", "CashMovements");
    }
}
