using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubStack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class D5ContinuationIdempotencyScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_idempotency_records_IdempotencyKey",
                table: "idempotency_records");

            migrationBuilder.AddColumn<string>(
                name: "ActorUserId",
                table: "idempotency_records",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Operation",
                table: "idempotency_records",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "idempotency_records",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_IdempotencyKey_Operation_ActorUserId",
                table: "idempotency_records",
                columns: new[] { "IdempotencyKey", "Operation", "ActorUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_idempotency_records_IdempotencyKey_Operation_ActorUserId",
                table: "idempotency_records");

            migrationBuilder.DropColumn(
                name: "ActorUserId",
                table: "idempotency_records");

            migrationBuilder.DropColumn(
                name: "Operation",
                table: "idempotency_records");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "idempotency_records");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_IdempotencyKey",
                table: "idempotency_records",
                column: "IdempotencyKey",
                unique: true);
        }
    }
}
