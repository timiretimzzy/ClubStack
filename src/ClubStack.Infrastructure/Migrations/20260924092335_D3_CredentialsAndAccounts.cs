using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubStack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class D3_CredentialsAndAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "credentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialNumber = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CredentialType = table.Column<int>(type: "integer", nullable: false),
                    CredentialValue = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SuspendedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReplacedByCredentialId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_credentials_credentials_ReplacedByCredentialId",
                        column: x => x.ReplacedByCredentialId,
                        principalTable: "credentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credentials_members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "member_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    OpenedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_member_accounts_members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "credential_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousState = table.Column<int>(type: "integer", nullable: false),
                    NewState = table.Column<int>(type: "integer", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credential_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_credential_history_credentials_CredentialId",
                        column: x => x.CredentialId,
                        principalTable: "credentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "account_statements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatementNumber = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FromDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ToDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    GeneratedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_statements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_account_statements_member_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "member_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "account_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PaymentState = table.Column<int>(type: "integer", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Reference = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReverseOfTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentMethodId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransactionDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BusinessDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_account_transactions_account_transactions_ReverseOfTransact~",
                        column: x => x.ReverseOfTransactionId,
                        principalTable: "account_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_account_transactions_member_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "member_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_account_transactions_payment_methods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "payment_methods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "payment_allocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToChargeTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_allocations_account_transactions_FromTransactionId",
                        column: x => x.FromTransactionId,
                        principalTable: "account_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payment_allocations_account_transactions_ToChargeTransactio~",
                        column: x => x.ToChargeTransactionId,
                        principalTable: "account_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payment_allocations_member_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "member_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_statements_AccountId_GeneratedAtUtc",
                table: "account_statements",
                columns: new[] { "AccountId", "GeneratedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_account_statements_StatementNumber",
                table: "account_statements",
                column: "StatementNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_transactions_AccountId_BusinessDateUtc",
                table: "account_transactions",
                columns: new[] { "AccountId", "BusinessDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_account_transactions_AccountId_Status",
                table: "account_transactions",
                columns: new[] { "AccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_account_transactions_PaymentMethodId",
                table: "account_transactions",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_account_transactions_Reference",
                table: "account_transactions",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_transactions_ReverseOfTransactionId",
                table: "account_transactions",
                column: "ReverseOfTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_credential_history_CredentialId_ChangedAtUtc",
                table: "credential_history",
                columns: new[] { "CredentialId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_credentials_CredentialNumber",
                table: "credentials",
                column: "CredentialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credentials_CredentialValue",
                table: "credentials",
                column: "CredentialValue",
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_credentials_MemberId",
                table: "credentials",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_credentials_ReplacedByCredentialId",
                table: "credentials",
                column: "ReplacedByCredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_credentials_Status",
                table: "credentials",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_member_accounts_AccountNumber",
                table: "member_accounts",
                column: "AccountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_member_accounts_MemberId",
                table: "member_accounts",
                column: "MemberId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_AccountId",
                table: "payment_allocations",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_FromTransactionId_ToChargeTransactionId",
                table: "payment_allocations",
                columns: new[] { "FromTransactionId", "ToChargeTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_ToChargeTransactionId",
                table: "payment_allocations",
                column: "ToChargeTransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_statements");

            migrationBuilder.DropTable(
                name: "credential_history");

            migrationBuilder.DropTable(
                name: "payment_allocations");

            migrationBuilder.DropTable(
                name: "credentials");

            migrationBuilder.DropTable(
                name: "account_transactions");

            migrationBuilder.DropTable(
                name: "member_accounts");
        }
    }
}
