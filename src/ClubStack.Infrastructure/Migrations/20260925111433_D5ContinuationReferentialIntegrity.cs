using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubStack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class D5ContinuationReferentialIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "DiscountType",
                table: "sale_lines",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_sales_LocationId",
                table: "sales",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_PosOutletId",
                table: "sales",
                column: "PosOutletId");

            migrationBuilder.CreateIndex(
                name: "IX_sale_payments_AccountTransactionId",
                table: "sale_payments",
                column: "AccountTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_sale_payments_PaymentMethodId",
                table: "sale_payments",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_sale_lines_ProductId",
                table: "sale_lines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_sale_discounts_LineId",
                table: "sale_discounts",
                column: "LineId");

            migrationBuilder.CreateIndex(
                name: "IX_cashier_sessions_RegisterId",
                table: "cashier_sessions",
                column: "RegisterId",
                unique: true,
                filter: "\"Status\" IN (0, 1, 2)");

            migrationBuilder.AddForeignKey(
                name: "FK_sale_discounts_sale_lines_LineId",
                table: "sale_discounts",
                column: "LineId",
                principalTable: "sale_lines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sale_lines_products_ProductId",
                table: "sale_lines",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sale_payments_account_transactions_AccountTransactionId",
                table: "sale_payments",
                column: "AccountTransactionId",
                principalTable: "account_transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sale_payments_payment_methods_PaymentMethodId",
                table: "sale_payments",
                column: "PaymentMethodId",
                principalTable: "payment_methods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sales_inventory_locations_LocationId",
                table: "sales",
                column: "LocationId",
                principalTable: "inventory_locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sales_pos_outlets_PosOutletId",
                table: "sales",
                column: "PosOutletId",
                principalTable: "pos_outlets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sale_discounts_sale_lines_LineId",
                table: "sale_discounts");

            migrationBuilder.DropForeignKey(
                name: "FK_sale_lines_products_ProductId",
                table: "sale_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_sale_payments_account_transactions_AccountTransactionId",
                table: "sale_payments");

            migrationBuilder.DropForeignKey(
                name: "FK_sale_payments_payment_methods_PaymentMethodId",
                table: "sale_payments");

            migrationBuilder.DropForeignKey(
                name: "FK_sales_inventory_locations_LocationId",
                table: "sales");

            migrationBuilder.DropForeignKey(
                name: "FK_sales_pos_outlets_PosOutletId",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "IX_sales_LocationId",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "IX_sales_PosOutletId",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "IX_sale_payments_AccountTransactionId",
                table: "sale_payments");

            migrationBuilder.DropIndex(
                name: "IX_sale_payments_PaymentMethodId",
                table: "sale_payments");

            migrationBuilder.DropIndex(
                name: "IX_sale_lines_ProductId",
                table: "sale_lines");

            migrationBuilder.DropIndex(
                name: "IX_sale_discounts_LineId",
                table: "sale_discounts");

            migrationBuilder.DropIndex(
                name: "IX_cashier_sessions_RegisterId",
                table: "cashier_sessions");

            migrationBuilder.AlterColumn<int>(
                name: "DiscountType",
                table: "sale_lines",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
