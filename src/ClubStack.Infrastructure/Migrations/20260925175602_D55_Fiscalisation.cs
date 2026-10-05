using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubStack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class D55_Fiscalisation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fiscalisations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleNumber = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RefundId = table.Column<Guid>(type: "uuid", nullable: true),
                    RefundNumber = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DocumentKind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    OriginalInvoiceNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ErrorInfo = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    DeviceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DeviceSerialNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FiscalDayNo = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    ReceiptCounter = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    ReceiptGlobalNo = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    ReceiptId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OperationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DeviceSignature = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ServerSignature = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    QrUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PendingSync = table.Column<bool>(type: "boolean", nullable: false),
                    PaymentMapping = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TendersJson = table.Column<string>(type: "text", nullable: false),
                    ValidationWarningsJson = table.Column<string>(type: "text", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FiscalisedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiscalisations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fiscalisations_sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fiscalisations_FiscalDayNo",
                table: "fiscalisations",
                column: "FiscalDayNo");

            migrationBuilder.CreateIndex(
                name: "IX_fiscalisations_InvoiceNumber",
                table: "fiscalisations",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fiscalisations_SaleId",
                table: "fiscalisations",
                column: "SaleId",
                unique: true,
                filter: "\"RefundId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_fiscalisations_SaleId_RefundId",
                table: "fiscalisations",
                columns: new[] { "SaleId", "RefundId" });

            migrationBuilder.CreateIndex(
                name: "IX_fiscalisations_Status",
                table: "fiscalisations",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fiscalisations");
        }
    }
}
