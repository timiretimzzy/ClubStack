using Microsoft.EntityFrameworkCore.Migrations;

namespace ClubStack.Infrastructure.Migrations;

/// <summary>
/// D6 database-level finance integrity. EF Core cannot express these rules, so the D6
/// migration installs them as PostgreSQL check constraints, unique indexes and triggers.
/// B3 requires a posted journal to be balanced, B4 requires a posted journal to be
/// immutable, B5 requires posting into an open period, and B6 requires exactly one
/// journal per operational source.
///
/// Everything here is raw SQL on purpose: the indexes, constraints and the
/// <c>PostingTransactionId</c> column are deliberately absent from the EF model, so a
/// later scaffolded migration will not try to drop them.
/// </summary>
internal static class D6FinanceIntegrity
{
    // Mirrors of the domain enums. JournalStatus: Draft = 0, Posted = 1, Cancelled = 2.
    private const int PostedStatus = 1;

    // FinancialPeriodStatus: Open = 0, Closed = 1.
    private const int OpenPeriod = 0;

    // JournalDirection: Debit = 0, Credit = 1.
    private const int DebitDirection = 0;
    private const int CreditDirection = 1;

    public static void Apply(MigrationBuilder migrationBuilder)
    {
        // B7: identical business references can never be reused.
        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX "UX_financial_accounts_Reference" ON "financial_accounts" ("Reference");
            CREATE UNIQUE INDEX "UX_financial_periods_Year_Month" ON "financial_periods" ("Year", "Month");
            CREATE UNIQUE INDEX "UX_journals_Reference" ON "journals" ("Reference");
            CREATE UNIQUE INDEX "UX_accounts_receivable_Reference" ON "accounts_receivable" ("Reference");
            CREATE UNIQUE INDEX "UX_accounts_payable_Reference" ON "accounts_payable" ("Reference");
            CREATE UNIQUE INDEX "UX_payments_Reference" ON "payments" ("Reference");
            CREATE UNIQUE INDEX "UX_financial_reconciliations_Reference" ON "financial_reconciliations" ("Reference");

            -- B6: an operational source may produce at most one journal and at most one
            -- receivable/payable. Idempotent outbox replay depends on this.
            CREATE UNIQUE INDEX "UX_journals_SourceType_SourceId"
                ON "journals" ("SourceType", "SourceId");
            CREATE UNIQUE INDEX "UX_accounts_receivable_SourceType_SourceId"
                ON "accounts_receivable" ("SourceType", "SourceId");
            CREATE UNIQUE INDEX "UX_accounts_payable_SourceType_SourceId"
                ON "accounts_payable" ("SourceType", "SourceId");

            -- A payment can only be raised once per operational source.
            CREATE UNIQUE INDEX "UX_payments_SourceType_SourceId"
                ON "payments" ("SourceType", "SourceId")
                WHERE "SourceId" IS NOT NULL;

            -- Only the latest posted journal may be reversed, and a journal cannot be
            -- reversed more than once.
            CREATE UNIQUE INDEX "UX_journals_ReversalOfJournalId"
                ON "journals" ("ReversalOfJournalId")
                WHERE "ReversalOfJournalId" IS NOT NULL;
            """);

        // B7: the mandated business reference formats.
        migrationBuilder.Sql(
            """
            ALTER TABLE "financial_accounts"
                ADD CONSTRAINT "CK_financial_accounts_Reference"
                CHECK ("Reference" ~ '^FA-[0-9]{6}$');
            ALTER TABLE "journals"
                ADD CONSTRAINT "CK_journals_Reference"
                CHECK ("Reference" ~ '^JRN-[0-9]{6}$');
            ALTER TABLE "financial_periods"
                ADD CONSTRAINT "CK_financial_periods_Reference"
                CHECK ("Reference" ~ '^PER-[0-9]{6}$');
            ALTER TABLE "accounts_receivable"
                ADD CONSTRAINT "CK_accounts_receivable_Reference"
                CHECK ("Reference" ~ '^AR-[0-9]{6}$');
            ALTER TABLE "accounts_payable"
                ADD CONSTRAINT "CK_accounts_payable_Reference"
                CHECK ("Reference" ~ '^AP-[0-9]{6}$');
            ALTER TABLE "payments"
                ADD CONSTRAINT "CK_payments_Reference"
                CHECK ("Reference" ~ '^PAY-[0-9]{6}$');
            ALTER TABLE "financial_reconciliations"
                ADD CONSTRAINT "CK_financial_reconciliations_Reference"
                CHECK ("Reference" ~ '^REC-[0-9]{6}$');
            """);

        // B3/B4: line shape and column domains.
        migrationBuilder.Sql(
            """
            ALTER TABLE "journal_lines"
                ADD CONSTRAINT "CK_journal_lines_LineNumber" CHECK ("LineNumber" > 0),
                ADD CONSTRAINT "CK_journal_lines_Amount" CHECK ("Amount" > 0),
                ADD CONSTRAINT "CK_journal_lines_BaseAmount" CHECK ("BaseAmount" > 0),
                ADD CONSTRAINT "CK_journal_lines_Direction"
                    CHECK ("Direction" = 0 OR "Direction" = 1);

            -- B2: exactly one normal balance side, and a Cash Position classification
            -- limited to None / CashOnHand / Bank.
            ALTER TABLE "financial_accounts"
                ADD CONSTRAINT "CK_financial_accounts_NormalBalance"
                    CHECK ("NormalBalance" = 0 OR "NormalBalance" = 1);
            ALTER TABLE "financial_accounts"
                ADD CONSTRAINT "CK_financial_accounts_CashKind"
                    CHECK ("CashKind" >= 0 AND "CashKind" <= 2);
            ALTER TABLE "financial_accounts"
                ADD CONSTRAINT "CK_financial_accounts_AccountType"
                    CHECK ("AccountType" >= 0 AND "AccountType" <= 4);
            ALTER TABLE "financial_accounts"
                ADD CONSTRAINT "CK_financial_accounts_NotSelfParent" CHECK ("ParentAccountId" IS DISTINCT FROM "Id");

            ALTER TABLE "financial_periods"
                ADD CONSTRAINT "CK_financial_periods_Month" CHECK ("Month" BETWEEN 1 AND 12),
                ADD CONSTRAINT "CK_financial_periods_Year" CHECK ("Year" BETWEEN 2000 AND 9999),
                ADD CONSTRAINT "CK_financial_periods_Range" CHECK ("StartUtc" < "EndUtc"),
                ADD CONSTRAINT "CK_financial_periods_Closed"
                    CHECK ("Status" <> 1 OR ("ClosedAtUtc" IS NOT NULL AND "ClosedByUserId" IS NOT NULL));

            ALTER TABLE "accounts_receivable"
                ADD CONSTRAINT "CK_accounts_receivable_Amounts"
                    CHECK ("OriginalAmount" > 0
                       AND "AllocatedAmount" >= 0
                       AND "OutstandingAmount" >= 0
                       AND "OriginalAmount" = "AllocatedAmount" + "OutstandingAmount"),
                ADD CONSTRAINT "CK_accounts_receivable_Cancelled"
                    CHECK ("Status" <> 3 OR "CancelledReason" IS NOT NULL);
            ALTER TABLE "accounts_payable"
                ADD CONSTRAINT "CK_accounts_payable_Amounts"
                    CHECK ("OriginalAmount" > 0
                       AND "AllocatedAmount" >= 0
                       AND "OutstandingAmount" >= 0
                       AND "OriginalAmount" = "AllocatedAmount" + "OutstandingAmount"),
                ADD CONSTRAINT "CK_accounts_payable_Cancelled"
                    CHECK ("Status" <> 3 OR "CancelledReason" IS NOT NULL);

            ALTER TABLE "payments"
                ADD CONSTRAINT "CK_payments_Amount" CHECK ("Amount" > 0),
                ADD CONSTRAINT "CK_payments_UnappliedAmount"
                    CHECK ("UnappliedAmount" >= 0 AND "UnappliedAmount" <= "Amount"),
                ADD CONSTRAINT "CK_payments_Counterparty"
                    CHECK (num_nonnulls("MemberId", "SupplierId") = 1);

            ALTER TABLE "accounts_receivable_allocations"
                ADD CONSTRAINT "CK_accounts_receivable_allocations_Amount" CHECK ("Amount" > 0);
            ALTER TABLE "accounts_payable_allocations"
                ADD CONSTRAINT "CK_accounts_payable_allocations_Amount" CHECK ("Amount" > 0);
            ALTER TABLE "finance_payment_allocations"
                ADD CONSTRAINT "CK_finance_payment_allocations_Amount" CHECK ("Amount" > 0);

            ALTER TABLE "financial_posting_rules"
                ADD CONSTRAINT "CK_financial_posting_rules_Code"
                CHECK ("Code" ~ '^[A-Z0-9]+(-[A-Z0-9]+)*$');
            ALTER TABLE "financial_posting_rule_mappings"
                ADD CONSTRAINT "CK_financial_posting_rule_mappings_Sequence" CHECK ("Sequence" > 0),
                ADD CONSTRAINT "CK_financial_posting_rule_mappings_Direction"
                    CHECK ("Direction" = 0 OR "Direction" = 1);

            ALTER TABLE "journals"
                ADD CONSTRAINT "CK_journals_ExchangeRate" CHECK ("ExchangeRate" > 0),
                ADD CONSTRAINT "CK_journals_Posted"
                    CHECK ("Status" <> 1 OR ("PostedAtUtc" IS NOT NULL AND "PostedByUserId" IS NOT NULL)),
                ADD CONSTRAINT "CK_journals_Cancelled"
                    CHECK ("Status" <> 2 OR ("CancelledAtUtc" IS NOT NULL AND "CancelledReason" IS NOT NULL)),
                ADD CONSTRAINT "CK_journals_NotSelfReversal"
                    CHECK ("ReversalOfJournalId" IS DISTINCT FROM "Id");

            ALTER TABLE "financial_reconciliations"
                ADD CONSTRAINT "CK_financial_reconciliations_Completed"
                    CHECK ("Status" <> 1 OR ("CompletedAtUtc" IS NOT NULL AND "CompletedByUserId" IS NOT NULL));
            """);

        // B3: a posted journal must have at least two lines, at least one on each side,
        // and equal debit and credit totals. The trigger is deferred so a journal and
        // its dependent lines may be inserted in any order inside one transaction.
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION "fn_d6_assert_journal_balanced"() RETURNS trigger
            LANGUAGE plpgsql AS $$
            DECLARE
                target_journal uuid;
                line_count integer;
                debit_total numeric(19,4);
                credit_total numeric(19,4);
            BEGIN
                -- This one function serves triggers on both tables, and NEW is NULL on
                -- DELETE while OLD is NULL on INSERT, so neither record may be
                -- dereferenced blindly: a "journals" row has no "JournalId" column at
                -- all, and a "journal_lines" row has no "Id" to post.
                IF TG_TABLE_NAME = 'journal_lines' THEN
                    IF TG_OP = 'DELETE' THEN
                        target_journal := OLD."JournalId";
                    ELSE
                        target_journal := NEW."JournalId";
                    END IF;
                ELSE
                    target_journal := NEW."Id";
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "journals"
                               WHERE "Id" = target_journal AND "Status" = 1) THEN
                    RETURN NULL;
                END IF;

                SELECT count(*),
                       COALESCE(sum(l."Amount") FILTER (WHERE l."Direction" = 0), 0),
                       COALESCE(sum(l."Amount") FILTER (WHERE l."Direction" = 1), 0)
                  INTO line_count, debit_total, credit_total
                  FROM "journal_lines" l
                 WHERE l."JournalId" = target_journal;

                IF line_count < 2 THEN
                    RAISE EXCEPTION 'Journal % must have at least two lines.', target_journal
                        USING ERRCODE = 'check_violation';
                END IF;
                IF debit_total <= 0 OR credit_total <= 0 THEN
                    RAISE EXCEPTION 'Journal % must carry both a debit and a credit.', target_journal
                        USING ERRCODE = 'check_violation';
                END IF;
                IF debit_total <> credit_total THEN
                    RAISE EXCEPTION 'Journal % is not balanced: debits % versus credits %.',
                        target_journal, debit_total, credit_total
                        USING ERRCODE = 'check_violation';
                END IF;

                RETURN NULL;
            END;
            $$;

            CREATE CONSTRAINT TRIGGER "CK_journals_Balanced"
                AFTER INSERT OR UPDATE OR DELETE ON "journal_lines"
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION "fn_d6_assert_journal_balanced"();

            CREATE CONSTRAINT TRIGGER "CK_journals_Balanced_OnPost"
                AFTER INSERT OR UPDATE OF "Status" ON "journals"
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW WHEN (NEW."Status" = 1)
                EXECUTE FUNCTION "fn_d6_assert_journal_balanced"();
            """);

        // B4: posted journals and their lines are immutable, and lines may only be added
        // inside the transaction that posts the journal. The posting transaction id is
        // stamped when the journal first reaches Posted, so a later back-dated insert is
        // rejected even though the journal status has not changed.
        migrationBuilder.Sql(
            """
            ALTER TABLE "journals" ADD COLUMN IF NOT EXISTS "PostingTransactionId" bigint NULL;

            CREATE OR REPLACE FUNCTION "fn_d6_stamp_posting_transaction"() RETURNS trigger
            LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW."Status" = 1 AND NEW."PostingTransactionId" IS NULL THEN
                    NEW."PostingTransactionId" := txid_current();
                ELSIF NEW."Status" <> 1 THEN
                    NEW."PostingTransactionId" := NULL;
                END IF;
                RETURN NEW;
            END;
            $$;

            CREATE TRIGGER "TR_journals_StampPostingTransaction"
                BEFORE INSERT OR UPDATE ON "journals"
                FOR EACH ROW EXECUTE FUNCTION "fn_d6_stamp_posting_transaction"();

            CREATE OR REPLACE FUNCTION "fn_d6_reject_journal_mutation"() RETURNS trigger
            LANGUAGE plpgsql AS $$
            BEGIN
                IF OLD."Status" = 1 THEN
                    RAISE EXCEPTION 'Journal % is posted and cannot be changed.', OLD."Id"
                        USING ERRCODE = 'check_violation';
                END IF;
                IF TG_OP = 'DELETE' THEN
                    RETURN OLD;
                END IF;
                RETURN NEW;
            END;
            $$;

            CREATE TRIGGER "TR_journals_Immutability"
                BEFORE UPDATE OR DELETE ON "journals"
                FOR EACH ROW EXECUTE FUNCTION "fn_d6_reject_journal_mutation"();

            CREATE OR REPLACE FUNCTION "fn_d6_reject_journal_line_mutation"() RETURNS trigger
            LANGUAGE plpgsql AS $$
            DECLARE
                parent "journals"%ROWTYPE;
                line_journal uuid;
            BEGIN
                -- NEW is NULL on DELETE and OLD is NULL on INSERT.
                IF TG_OP = 'DELETE' THEN
                    line_journal := OLD."JournalId";
                ELSE
                    line_journal := NEW."JournalId";
                END IF;
                SELECT * INTO parent FROM "journals" WHERE "Id" = line_journal;

                IF parent."Id" IS NULL THEN
                    IF TG_OP = 'DELETE' THEN
                        RETURN OLD;
                    END IF;
                    RETURN NEW;
                END IF;

                IF parent."Status" = 1 THEN
                    IF TG_OP = 'INSERT' THEN
                        IF parent."PostingTransactionId" IS DISTINCT FROM txid_current() THEN
                            RAISE EXCEPTION 'Journal % is posted; lines cannot be added later.', parent."Id"
                                USING ERRCODE = 'check_violation';
                        END IF;
                        RETURN NEW;
                    END IF;

                    RAISE EXCEPTION 'Journal % is posted; lines are immutable.', parent."Id"
                        USING ERRCODE = 'check_violation';
                END IF;

                IF TG_OP = 'DELETE' THEN
                    RETURN OLD;
                END IF;
                RETURN NEW;
            END;
            $$;

            CREATE TRIGGER "TR_journal_lines_Immutability"
                BEFORE INSERT OR UPDATE OR DELETE ON "journal_lines"
                FOR EACH ROW EXECUTE FUNCTION "fn_d6_reject_journal_line_mutation"();
            """);

        // B5: a journal may only be posted into an open period.
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION "fn_d6_reject_post_to_closed_period"() RETURNS trigger
            LANGUAGE plpgsql AS $$
            DECLARE
                period_status integer;
            BEGIN
                IF NEW."Status" <> 1 THEN
                    RETURN NEW;
                END IF;

                SELECT p."Status" INTO period_status
                  FROM "financial_periods" p
                 WHERE p."Id" = NEW."FinancialPeriodId";

                IF period_status IS NULL THEN
                    RAISE EXCEPTION 'Financial period % does not exist.', NEW."FinancialPeriodId"
                        USING ERRCODE = 'foreign_key_violation';
                END IF;
                IF period_status <> 0 THEN
                    RAISE EXCEPTION 'Financial period % is closed; journal % cannot be posted.', NEW."FinancialPeriodId", NEW."Id"
                        USING ERRCODE = 'check_violation';
                END IF;

                RETURN NEW;
            END;
            $$;

            CREATE TRIGGER "TR_journals_OpenPeriod"
                BEFORE INSERT OR UPDATE ON "journals"
                FOR EACH ROW EXECUTE FUNCTION "fn_d6_reject_post_to_closed_period"();
            """);
    }

    public static void Revert(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS "TR_journals_OpenPeriod" ON "journals";
            DROP TRIGGER IF EXISTS "TR_journal_lines_Immutability" ON "journal_lines";
            DROP TRIGGER IF EXISTS "TR_journals_Immutability" ON "journals";
            DROP TRIGGER IF EXISTS "TR_journals_StampPostingTransaction" ON "journals";
            DROP TRIGGER IF EXISTS "CK_journals_Balanced_OnPost" ON "journals";
            DROP TRIGGER IF EXISTS "CK_journals_Balanced" ON "journal_lines";
            DROP FUNCTION IF EXISTS "fn_d6_reject_post_to_closed_period"();
            DROP FUNCTION IF EXISTS "fn_d6_reject_journal_line_mutation"();
            DROP FUNCTION IF EXISTS "fn_d6_reject_journal_mutation"();
            DROP FUNCTION IF EXISTS "fn_d6_stamp_posting_transaction"();
            DROP FUNCTION IF EXISTS "fn_d6_assert_journal_balanced"();
            ALTER TABLE "journals" DROP COLUMN IF EXISTS "PostingTransactionId";

            DROP INDEX IF EXISTS "UX_journals_ReversalOfJournalId";
            DROP INDEX IF EXISTS "UX_payments_SourceType_SourceId";
            DROP INDEX IF EXISTS "UX_accounts_payable_SourceType_SourceId";
            DROP INDEX IF EXISTS "UX_accounts_receivable_SourceType_SourceId";
            DROP INDEX IF EXISTS "UX_journals_SourceType_SourceId";
            DROP INDEX IF EXISTS "UX_financial_reconciliations_Reference";
            DROP INDEX IF EXISTS "UX_payments_Reference";
            DROP INDEX IF EXISTS "UX_accounts_payable_Reference";
            DROP INDEX IF EXISTS "UX_accounts_receivable_Reference";
            DROP INDEX IF EXISTS "UX_journals_Reference";
            DROP INDEX IF EXISTS "UX_financial_periods_Year_Month";
            DROP INDEX IF EXISTS "UX_financial_accounts_Reference";

            ALTER TABLE "financial_reconciliations"
                DROP CONSTRAINT IF EXISTS "CK_financial_reconciliations_Completed";
            ALTER TABLE "journals"
                DROP CONSTRAINT IF EXISTS "CK_journals_NotSelfReversal",
                DROP CONSTRAINT IF EXISTS "CK_journals_Cancelled",
                DROP CONSTRAINT IF EXISTS "CK_journals_Posted",
                DROP CONSTRAINT IF EXISTS "CK_journals_ExchangeRate";
            ALTER TABLE "financial_posting_rule_mappings"
                DROP CONSTRAINT IF EXISTS "CK_financial_posting_rule_mappings_Direction",
                DROP CONSTRAINT IF EXISTS "CK_financial_posting_rule_mappings_Sequence";
            ALTER TABLE "financial_posting_rules"
                DROP CONSTRAINT IF EXISTS "CK_financial_posting_rules_Code";
            ALTER TABLE "finance_payment_allocations"
                DROP CONSTRAINT IF EXISTS "CK_finance_payment_allocations_Amount";
            ALTER TABLE "accounts_payable_allocations"
                DROP CONSTRAINT IF EXISTS "CK_accounts_payable_allocations_Amount";
            ALTER TABLE "accounts_receivable_allocations"
                DROP CONSTRAINT IF EXISTS "CK_accounts_receivable_allocations_Amount";
            ALTER TABLE "payments"
                DROP CONSTRAINT IF EXISTS "CK_payments_Counterparty",
                DROP CONSTRAINT IF EXISTS "CK_payments_UnappliedAmount",
                DROP CONSTRAINT IF EXISTS "CK_payments_Amount";
            ALTER TABLE "accounts_payable"
                DROP CONSTRAINT IF EXISTS "CK_accounts_payable_Cancelled",
                DROP CONSTRAINT IF EXISTS "CK_accounts_payable_Amounts";
            ALTER TABLE "accounts_receivable"
                DROP CONSTRAINT IF EXISTS "CK_accounts_receivable_Cancelled",
                DROP CONSTRAINT IF EXISTS "CK_accounts_receivable_Amounts";
            ALTER TABLE "financial_periods"
                DROP CONSTRAINT IF EXISTS "CK_financial_periods_Closed",
                DROP CONSTRAINT IF EXISTS "CK_financial_periods_Range",
                DROP CONSTRAINT IF EXISTS "CK_financial_periods_Year",
                DROP CONSTRAINT IF EXISTS "CK_financial_periods_Month";
            ALTER TABLE "financial_accounts"
                DROP CONSTRAINT IF EXISTS "CK_financial_accounts_NotSelfParent",
                DROP CONSTRAINT IF EXISTS "CK_financial_accounts_AccountType",
                DROP CONSTRAINT IF EXISTS "CK_financial_accounts_CashKind",
                DROP CONSTRAINT IF EXISTS "CK_financial_accounts_NormalBalance";
            ALTER TABLE "journal_lines"
                DROP CONSTRAINT IF EXISTS "CK_journal_lines_Direction",
                DROP CONSTRAINT IF EXISTS "CK_journal_lines_BaseAmount",
                DROP CONSTRAINT IF EXISTS "CK_journal_lines_Amount",
                DROP CONSTRAINT IF EXISTS "CK_journal_lines_LineNumber";
            ALTER TABLE "financial_reconciliations"
                DROP CONSTRAINT IF EXISTS "CK_financial_reconciliations_Reference";
            ALTER TABLE "payments"
                DROP CONSTRAINT IF EXISTS "CK_payments_Reference";
            ALTER TABLE "accounts_payable"
                DROP CONSTRAINT IF EXISTS "CK_accounts_payable_Reference";
            ALTER TABLE "accounts_receivable"
                DROP CONSTRAINT IF EXISTS "CK_accounts_receivable_Reference";
            ALTER TABLE "journals"
                DROP CONSTRAINT IF EXISTS "CK_journals_Reference";
            ALTER TABLE "financial_periods"
                DROP CONSTRAINT IF EXISTS "CK_financial_periods_Reference";
            ALTER TABLE "financial_accounts"
                DROP CONSTRAINT IF EXISTS "CK_financial_accounts_Reference";
            """);
    }
}
