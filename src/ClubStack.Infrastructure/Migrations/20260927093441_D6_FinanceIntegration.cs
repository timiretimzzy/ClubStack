using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubStack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class D6_FinanceIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "financial_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AccountType = table.Column<int>(type: "integer", nullable: false),
                    NormalBalance = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ParentAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsPostingAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    CashKind = table.Column<int>(type: "integer", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financial_accounts_financial_accounts_ParentAccountId",
                        column: x => x.ParentAccountId,
                        principalTable: "financial_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "financial_periods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OpenedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OpenedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReopenedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReopenedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReopenReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ReopenCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_periods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_posting_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SourceSection = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PostingStrategy = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_posting_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_reconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatementDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StatementClosingBalance = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    LedgerClosingBalance = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    Variance = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CancelledReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_reconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financial_reconciliations_financial_accounts_FinancialAccou~",
                        column: x => x.FinancialAccountId,
                        principalTable: "financial_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "financial_posting_rule_mappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Selector = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_posting_rule_mappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financial_posting_rule_mappings_financial_accounts_Financia~",
                        column: x => x.FinancialAccountId,
                        principalTable: "financial_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_posting_rule_mappings_financial_posting_rules_Rul~",
                        column: x => x.RuleId,
                        principalTable: "financial_posting_rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "accounts_payable",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OriginalAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    OutstandingAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    TransactionDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    JournalId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CancelledReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts_payable", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accounts_payable_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "accounts_payable_allocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayableId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    AllocatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AllocatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts_payable_allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accounts_payable_allocations_accounts_payable_PayableId",
                        column: x => x.PayableId,
                        principalTable: "accounts_payable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "accounts_receivable",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OriginalAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    OutstandingAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    TransactionDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    JournalId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CancelledReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts_receivable", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accounts_receivable_member_accounts_MemberAccountId",
                        column: x => x.MemberAccountId,
                        principalTable: "member_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounts_receivable_members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "journals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    FinancialPeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BusinessDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TransactionCurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    BaseCurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    RateDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ReversalOfJournalId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivableId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayableId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_journals_accounts_payable_PayableId",
                        column: x => x.PayableId,
                        principalTable: "accounts_payable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journals_accounts_receivable_ReceivableId",
                        column: x => x.ReceivableId,
                        principalTable: "accounts_receivable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journals_financial_periods_FinancialPeriodId",
                        column: x => x.FinancialPeriodId,
                        principalTable: "financial_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journals_journals_ReversalOfJournalId",
                        column: x => x.ReversalOfJournalId,
                        principalTable: "journals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "financial_reconciliation_matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReconciliationId = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalId = table.Column<Guid>(type: "uuid", nullable: false),
                    LedgerAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    Note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    MatchedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MatchedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_reconciliation_matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financial_reconciliation_matches_financial_reconciliations_~",
                        column: x => x.ReconciliationId,
                        principalTable: "financial_reconciliations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_financial_reconciliation_matches_journals_JournalId",
                        column: x => x.JournalId,
                        principalTable: "journals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "journal_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SourceLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_journal_lines_financial_accounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "financial_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_lines_journal_lines_SourceLineId",
                        column: x => x.SourceLineId,
                        principalTable: "journal_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_lines_journals_JournalId",
                        column: x => x.JournalId,
                        principalTable: "journals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemberAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    UnappliedAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    TransactionDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValueDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaymentMethodCode = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    ExternalReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    JournalId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payments_journals_JournalId",
                        column: x => x.JournalId,
                        principalTable: "journals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_member_accounts_MemberAccountId",
                        column: x => x.MemberAccountId,
                        principalTable: "member_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "accounts_receivable_allocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivableId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    AllocatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AllocatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts_receivable_allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accounts_receivable_allocations_accounts_receivable_Receiva~",
                        column: x => x.ReceivableId,
                        principalTable: "accounts_receivable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_accounts_receivable_allocations_payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_payment_allocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivableId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayableId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_payment_allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finance_payment_allocations_accounts_payable_PayableId",
                        column: x => x.PayableId,
                        principalTable: "accounts_payable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_payment_allocations_accounts_receivable_ReceivableId",
                        column: x => x.ReceivableId,
                        principalTable: "accounts_receivable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_payment_allocations_payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_payable_JournalId",
                table: "accounts_payable",
                column: "JournalId");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_payable_Reference",
                table: "accounts_payable",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_payable_SourceType_SourceId",
                table: "accounts_payable",
                columns: new[] { "SourceType", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_payable_Status_DueDate",
                table: "accounts_payable",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_payable_SupplierId",
                table: "accounts_payable",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_payable_allocations_PayableId",
                table: "accounts_payable_allocations",
                column: "PayableId");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_payable_allocations_PaymentId_PayableId",
                table: "accounts_payable_allocations",
                columns: new[] { "PaymentId", "PayableId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_receivable_JournalId",
                table: "accounts_receivable",
                column: "JournalId");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_receivable_MemberAccountId",
                table: "accounts_receivable",
                column: "MemberAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_receivable_MemberId",
                table: "accounts_receivable",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_receivable_Reference",
                table: "accounts_receivable",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_receivable_SourceType_SourceId",
                table: "accounts_receivable",
                columns: new[] { "SourceType", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_receivable_Status_DueDate",
                table: "accounts_receivable",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_receivable_allocations_PaymentId_ReceivableId",
                table: "accounts_receivable_allocations",
                columns: new[] { "PaymentId", "ReceivableId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_receivable_allocations_ReceivableId",
                table: "accounts_receivable_allocations",
                column: "ReceivableId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_payment_allocations_PayableId",
                table: "finance_payment_allocations",
                column: "PayableId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_payment_allocations_PaymentId_PayableId",
                table: "finance_payment_allocations",
                columns: new[] { "PaymentId", "PayableId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_payment_allocations_PaymentId_ReceivableId",
                table: "finance_payment_allocations",
                columns: new[] { "PaymentId", "ReceivableId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_payment_allocations_ReceivableId",
                table: "finance_payment_allocations",
                column: "ReceivableId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_accounts_AccountType",
                table: "financial_accounts",
                column: "AccountType");

            migrationBuilder.CreateIndex(
                name: "IX_financial_accounts_Code",
                table: "financial_accounts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_accounts_ParentAccountId",
                table: "financial_accounts",
                column: "ParentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_accounts_Reference",
                table: "financial_accounts",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_accounts_Status",
                table: "financial_accounts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_financial_periods_Reference",
                table: "financial_periods",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_periods_Status",
                table: "financial_periods",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_financial_periods_Year_Month",
                table: "financial_periods",
                columns: new[] { "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_posting_rule_mappings_FinancialAccountId",
                table: "financial_posting_rule_mappings",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_posting_rule_mappings_RuleId_Sequence",
                table: "financial_posting_rule_mappings",
                columns: new[] { "RuleId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_posting_rules_Code",
                table: "financial_posting_rules",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_reconciliation_matches_JournalId",
                table: "financial_reconciliation_matches",
                column: "JournalId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_reconciliation_matches_ReconciliationId_JournalId",
                table: "financial_reconciliation_matches",
                columns: new[] { "ReconciliationId", "JournalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_reconciliations_FinancialAccountId_StatementDateU~",
                table: "financial_reconciliations",
                columns: new[] { "FinancialAccountId", "StatementDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_reconciliations_Reference",
                table: "financial_reconciliations",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_reconciliations_Status",
                table: "financial_reconciliations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_journal_lines_FinancialAccountId",
                table: "journal_lines",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_lines_JournalId_LineNumber",
                table: "journal_lines",
                columns: new[] { "JournalId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_lines_SourceLineId",
                table: "journal_lines",
                column: "SourceLineId");

            migrationBuilder.CreateIndex(
                name: "IX_journals_FinancialPeriodId_Status",
                table: "journals",
                columns: new[] { "FinancialPeriodId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_journals_PayableId",
                table: "journals",
                column: "PayableId");

            migrationBuilder.CreateIndex(
                name: "IX_journals_ReceivableId",
                table: "journals",
                column: "ReceivableId");

            migrationBuilder.CreateIndex(
                name: "IX_journals_Reference",
                table: "journals",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journals_ReversalOfJournalId",
                table: "journals",
                column: "ReversalOfJournalId");

            migrationBuilder.CreateIndex(
                name: "IX_journals_SourceType_SourceId",
                table: "journals",
                columns: new[] { "SourceType", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journals_TransactionDate",
                table: "journals",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_payments_IdempotencyKey",
                table: "payments",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payments_JournalId",
                table: "payments",
                column: "JournalId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_MemberAccountId",
                table: "payments",
                column: "MemberAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_MemberId",
                table: "payments",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_Reference",
                table: "payments",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_SourceType_SourceId",
                table: "payments",
                columns: new[] { "SourceType", "SourceId" },
                unique: true,
                filter: "\"SourceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payments_SupplierId",
                table: "payments",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_TransactionDate",
                table: "payments",
                column: "TransactionDate");

            migrationBuilder.AddForeignKey(
                name: "FK_accounts_payable_journals_JournalId",
                table: "accounts_payable",
                column: "JournalId",
                principalTable: "journals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_accounts_payable_allocations_payments_PaymentId",
                table: "accounts_payable_allocations",
                column: "PaymentId",
                principalTable: "payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_accounts_receivable_journals_JournalId",
                table: "accounts_receivable",
                column: "JournalId",
                principalTable: "journals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            D6FinanceIntegrity.Apply(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            D6FinanceIntegrity.Revert(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_accounts_payable_journals_JournalId",
                table: "accounts_payable");

            migrationBuilder.DropForeignKey(
                name: "FK_accounts_receivable_journals_JournalId",
                table: "accounts_receivable");

            migrationBuilder.DropTable(
                name: "accounts_payable_allocations");

            migrationBuilder.DropTable(
                name: "accounts_receivable_allocations");

            migrationBuilder.DropTable(
                name: "finance_payment_allocations");

            migrationBuilder.DropTable(
                name: "financial_posting_rule_mappings");

            migrationBuilder.DropTable(
                name: "financial_reconciliation_matches");

            migrationBuilder.DropTable(
                name: "journal_lines");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "financial_posting_rules");

            migrationBuilder.DropTable(
                name: "financial_reconciliations");

            migrationBuilder.DropTable(
                name: "financial_accounts");

            migrationBuilder.DropTable(
                name: "journals");

            migrationBuilder.DropTable(
                name: "accounts_payable");

            migrationBuilder.DropTable(
                name: "accounts_receivable");

            migrationBuilder.DropTable(
                name: "financial_periods");
        }
    }
}
