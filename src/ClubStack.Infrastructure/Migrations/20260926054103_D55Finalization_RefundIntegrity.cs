using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubStack.Infrastructure.Migrations
{
    /// <summary>
    /// D5.5 finalisation — database-enforced one-credit-note-per-refund.
    ///
    /// Previously the one-credit-note-per-refund invariant existed only in application
    /// code, and <c>RefundId</c> referenced nothing, so a duplicate credit note for a
    /// single refund would have been accepted by the database. Phase 8 §9 requires the
    /// database to enforce foreign-key integrity, and a fiscal row is the durable
    /// statement that a document is owed to ZIMRA, so the reference has to be real and
    /// the invariant has to be the database's to reject.
    ///
    /// The index is filtered because invoice rows leave RefundId NULL; a plain unique
    /// index would collide on the second invoice. Restrict mirrors the existing Sale
    /// relationship so a fiscal obligation cannot be orphaned.
    /// </summary>
    public partial class D55Finalization_RefundIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_fiscalisations_RefundId",
                table: "fiscalisations",
                column: "RefundId",
                unique: true,
                filter: "\"RefundId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_fiscalisations_sale_refunds_RefundId",
                table: "fiscalisations",
                column: "RefundId",
                principalTable: "sale_refunds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_fiscalisations_sale_refunds_RefundId",
                table: "fiscalisations");

            migrationBuilder.DropIndex(
                name: "IX_fiscalisations_RefundId",
                table: "fiscalisations");
        }
    }
}
