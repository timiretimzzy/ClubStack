using Microsoft.EntityFrameworkCore.Migrations;

namespace ClubStack.Infrastructure.Migrations;

/// <summary>
/// D7 database-level golf integrity. EF Core cannot express these rules, so the D7
/// migration installs them as PostgreSQL check constraints, unique indexes and
/// triggers, following the D6 precedent.
///
/// <para>The service layer already enforces every rule here. This layer exists so
/// the invariants survive a second writer, a direct SQL session, or a future code
/// path: GOLF-011 (no overbooking), GOLF-012/Phase 3 §44 (a player is a member
/// <i>or</i> a guest), GOLF-021 (the Phase 8 §7.7 state machine) and the
/// human-readable reference format of Phase 8 §3.2 cannot be bypassed.</para>
///
/// <para>These objects are deliberately absent from the EF model so a later
/// scaffolded migration will not try to drop them.</para>
/// </summary>
internal static class D7GolfIntegrity
{
    // Mirrors of the domain enums. Kept as named constants so the SQL reads as the
    // rule it enforces rather than as magic numbers.
    //
    // GolfBookingStatus: Draft=0, Confirmed=1, CheckedIn=2, Completed=3, Cancelled=4, NoShow=5.
    private const int DraftStatus = 0;
    private const int ConfirmedStatus = 1;
    private const int CheckedInStatus = 2;
    private const int CompletedStatus = 3;
    private const int CancelledStatus = 4;
    private const int NoShowStatus = 5;

    // GolfPlayerType: Member=0, Guest=1.
    private const int MemberPlayer = 0;
    private const int GuestPlayer = 1;

    // GolfBookingPlayerStatus: Booked=0, CheckedIn=1, NoShow=2, Cancelled=3.
    private const int PlayerCancelled = 3;

    // GolfChargeType: GreenFee=0, Additional=1.
    private const int GreenFeeCharge = 0;

    public static void Apply(MigrationBuilder migrationBuilder)
    {
        ApplyReferenceFormats(migrationBuilder);
        ApplyBookingPlayerIntegrity(migrationBuilder);
        ApplyGreenFeeUniqueness(migrationBuilder);
        ApplyChargeIntegrity(migrationBuilder);
        ApplyBookingStateMachine(migrationBuilder);
        ApplyCapacityInvariant(migrationBuilder);
    }

    /// <summary>
    /// Drops the trigger functions. The tables, check constraints and indexes are
    /// already removed by the migration's own <c>Down</c>; the functions are not owned
    /// by a table, so PostgreSQL would keep them without this.
    ///
    /// <para>Each trigger is detached behind a relation-existence test rather than
    /// unconditionally, so this stays correct regardless of how far a rollback got
    /// before it stopped: PostgreSQL rejects <c>DROP TRIGGER ... ON table</c> when the
    /// table no longer exists, even with <c>IF EXISTS</c> on the trigger.</para>
    /// </summary>
    public static void Revert(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF to_regclass('golf_booking_players') IS NOT NULL THEN
                    DROP TRIGGER IF EXISTS "TR_golf_booking_players_slot_capacity" ON "golf_booking_players";
                END IF;

                IF to_regclass('golf_bookings') IS NOT NULL THEN
                    DROP TRIGGER IF EXISTS "TR_golf_bookings_state_machine" ON "golf_bookings";
                END IF;
            END
            $$;

            DROP FUNCTION IF EXISTS "clubstack_golf_enforce_slot_capacity"();
            DROP FUNCTION IF EXISTS "clubstack_golf_enforce_booking_transition"();
            """);
    }

    /// <summary>
    /// Phase 8 §3.2: human-readable business references in the module's documented
    /// shape. A truncated or mis-prefixed reference would break the reference a
    /// member is quoted on the tee sheet.
    /// </summary>
    private static void ApplyReferenceFormats(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "golf_bookings"
                ADD CONSTRAINT "CK_golf_bookings_Reference"
                CHECK ("Reference" ~ '^GBK-[0-9]{6}$');

            ALTER TABLE "golf_guest_records"
                ADD CONSTRAINT "CK_golf_guest_records_Reference"
                CHECK ("Reference" ~ '^GST-[0-9]{6}$');
            """);
    }

    /// <summary>
    /// Phase 3 §44 stores a nullable member reference "for visitor" and a nullable
    /// guest reference. Exactly one must be present and it must agree with the
    /// player-type discriminator, so a guest can never be recorded as a member and a
    /// member can never be recorded as a guest.
    /// </summary>
    private static void ApplyBookingPlayerIntegrity(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "golf_booking_players"
                ADD CONSTRAINT "CK_golf_booking_players_Person"
                CHECK (
                    ("PlayerType" = 0 AND "MemberId" IS NOT NULL AND "GolfGuestId" IS NULL)
                    OR ("PlayerType" = 1 AND "GolfGuestId" IS NOT NULL AND "MemberId" IS NULL)
                );

            ALTER TABLE "golf_booking_players"
                ADD CONSTRAINT "CK_golf_booking_players_Position"
                CHECK ("Position" >= 1);

            ALTER TABLE "golf_booking_players"
                ADD CONSTRAINT "CK_golf_booking_players_Fee"
                CHECK ("Fee" >= 0);
            """);
    }

    /// <summary>
    /// GOLF-016/GOLF-017: a player's green fee is raised once. A second green-fee
    /// charge for the same player would double-bill the member, and the partial
    /// index is what makes that impossible even for a booking-level row
    /// (BookingPlayerId IS NULL) appearing twice.
    /// </summary>
    private static void ApplyGreenFeeUniqueness(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX "UX_golf_charges_PlayerGreenFee"
                ON "golf_charges" ("BookingPlayerId")
                WHERE "ChargeType" = 0 AND "BookingPlayerId" IS NOT NULL;

            CREATE UNIQUE INDEX "UX_golf_charges_BookingGreenFee"
                ON "golf_charges" ("BookingId")
                WHERE "ChargeType" = 0 AND "BookingPlayerId" IS NULL;
            """);
    }

    /// <summary>
    /// A charge records a money amount and a lifecycle state. Phase 3 §75: a charge
    /// is never deleted, so cancellation and waiver are recorded states — which is
    /// only safe while a posted charge cannot be quietly rewritten.
    /// </summary>
    private static void ApplyChargeIntegrity(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "golf_charges"
                ADD CONSTRAINT "CK_golf_charges_Amount"
                CHECK ("Amount" >= 0);

            -- A charge that carries a financial link is Charged, and a charged row
            -- keeps its account/finance traceability (XMOD-004, XMOD-005).
            ALTER TABLE "golf_charges"
                ADD CONSTRAINT "CK_golf_charges_ChargedTraceability"
                CHECK (
                    "Status" <> 1
                    OR ("AccountTransactionId" IS NOT NULL AND "ReceivableId" IS NOT NULL)
                );
            """);
    }

    /// <summary>
    /// GOLF-021: enforce the Phase 8 §7.7 state machine in the database, so no
    /// writer can move a booking from Draft straight to Completed, or revive a
    /// cancelled booking. Same-state updates are allowed because they carry the
    /// timestamps, audit fields and concurrency token of a legitimate update.
    ///
    /// <para>Note what the table deliberately forbids: there is no Draft → Cancelled
    /// edge, because §7.7 does not define one.</para>
    /// </summary>
    private static void ApplyBookingStateMachine(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION "clubstack_golf_enforce_booking_transition"()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                IF OLD."Status" IS NOT DISTINCT FROM NEW."Status" THEN
                    RETURN NEW;
                END IF;

                IF NOT (
                    (OLD."Status" = 0 AND NEW."Status" = 1)
                    OR (OLD."Status" = 1 AND NEW."Status" = 2)
                    OR (OLD."Status" = 2 AND NEW."Status" = 3)
                    OR (OLD."Status" = 1 AND NEW."Status" = 4)
                    OR (OLD."Status" = 1 AND NEW."Status" = 5)
                ) THEN
                    RAISE EXCEPTION
                        'Golf booking % cannot move from % to %; Phase 8 7.7 allows Draft->Confirmed, Confirmed->CheckedIn, CheckedIn->Completed, Confirmed->Cancelled and Confirmed->NoShow only.',
                        OLD."Reference", OLD."Status", NEW."Status"
                        USING ERRCODE = 'check_violation';
                END IF;

                RETURN NEW;
            END;
            $$;

            CREATE TRIGGER "TR_golf_bookings_state_machine"
                BEFORE UPDATE ON "golf_bookings"
                FOR EACH ROW
                EXECUTE FUNCTION "clubstack_golf_enforce_booking_transition"();
            """);
    }

    /// <summary>
    /// GOLF-011: prevent overbooking at the storage layer. This is the invariant the
    /// whole D7 concurrency work exists to protect, so it is stated where no
    /// application bug can weaken it: the number of players held by
    /// capacity-consuming bookings on a tee slot may never exceed the slot's
    /// configured capacity.
    ///
    /// <para>The single exception is the recorded override. Phase 9 §12 requires an
    /// override to carry the actor, reason and timestamp, and those live on the
    /// booking, so the trigger permits the overflow only when the booking is flagged
    /// as overridden with that record present. An override therefore leaves an
    /// auditable trace rather than a silent hole in capacity.</para>
    ///
    /// <para>Counting is done against the live table inside the statement, so it is
    /// correct under concurrency for the same reason the service takes a row lock:
    /// PostgreSQL serialises the conflicting writes.</para>
    /// </summary>
    private static void ApplyCapacityInvariant(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "golf_courses"
                ADD CONSTRAINT "CK_golf_courses_NumberOfHoles"
                CHECK ("NumberOfHoles" >= 1);

            ALTER TABLE "golf_tees"
                ADD CONSTRAINT "CK_golf_tees_Capacity"
                CHECK ("Capacity" >= 1);

            ALTER TABLE "tee_slots"
                ADD CONSTRAINT "CK_tee_slots_Capacity"
                CHECK ("Capacity" >= 1);

            ALTER TABLE "tee_slots"
                ADD CONSTRAINT "CK_tee_slots_MaximumPlayers"
                CHECK ("MaximumPlayers" IS NULL OR "MaximumPlayers" >= 1);

            CREATE OR REPLACE FUNCTION "clubstack_golf_enforce_slot_capacity"()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            DECLARE
                slot_id uuid;
                slot_capacity integer;
                override_recorded boolean;
                players_held integer;
                BEGIN
                slot_id := COALESCE(NEW."BookingId", OLD."BookingId");

                -- Only a capacity-consuming booking holds a place. A Draft hold does
                -- not, and a cancelled player does not.
                IF NOT EXISTS (
                    SELECT 1
                    FROM "golf_bookings" b
                    WHERE b."Id" = slot_id
                      AND b."Status" IN (1, 2, 3, 5)
                ) THEN
                    RETURN NULL;
                END IF;

                SELECT s."Capacity" INTO slot_capacity
                FROM "tee_slots" s
                WHERE s."Id" = (SELECT b."TeeSlotId" FROM "golf_bookings" b WHERE b."Id" = slot_id);

                SELECT b."CapacityOverridden"
                      AND b."OverrideByUserId" IS NOT NULL
                      AND b."OverrideReason" IS NOT NULL
                      AND b."OverriddenAtUtc" IS NOT NULL
                INTO override_recorded
                FROM "golf_bookings" b
                WHERE b."Id" = slot_id;

                SELECT count(*) INTO players_held
                FROM "golf_booking_players" p
                JOIN "golf_bookings" b ON b."Id" = p."BookingId"
                WHERE b."TeeSlotId" = (SELECT b2."TeeSlotId" FROM "golf_bookings" b2 WHERE b2."Id" = slot_id)
                  AND b."Status" IN (1, 2, 3, 5)
                  AND p."Status" <> 3;

                IF players_held > slot_capacity AND NOT COALESCE(override_recorded, false) THEN
                    RAISE EXCEPTION
                        'Golf tee slot % holds % player(s) but its capacity is %; overbooking is not permitted without a recorded override.',
                        (SELECT b."TeeSlotId" FROM "golf_bookings" b WHERE b."Id" = slot_id),
                        players_held,
                        slot_capacity
                        USING ERRCODE = 'check_violation';
                END IF;

                RETURN NULL;
            END;
            $$;

            -- AFTER, not BEFORE: the check counts the rows as they now stand, which is
            -- the state the caller is trying to commit.
            CREATE TRIGGER "TR_golf_booking_players_slot_capacity"
                AFTER INSERT OR UPDATE OR DELETE ON "golf_booking_players"
                FOR EACH ROW
                EXECUTE FUNCTION "clubstack_golf_enforce_slot_capacity"();
            """);
    }
}
