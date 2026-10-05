using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubStack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class D7_GolfOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "golf_courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NumberOfHoles = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_golf_courses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "golf_guest_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FullName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ContactDetails = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HostMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    VisitCount = table.Column<int>(type: "integer", nullable: false),
                    LastVisitedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_golf_guest_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_golf_guest_records_members_HostMemberId",
                        column: x => x.HostMemberId,
                        principalTable: "members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "golf_holes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    HoleNumber = table.Column<int>(type: "integer", nullable: false),
                    Par = table.Column<int>(type: "integer", nullable: false),
                    Yardage = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_golf_holes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_golf_holes_golf_courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "golf_courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "golf_tees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MembersOnly = table.Column<bool>(type: "boolean", nullable: false),
                    GuestsAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    MinimumPlayers = table.Column<int>(type: "integer", nullable: true),
                    MaximumPlayers = table.Column<int>(type: "integer", nullable: true),
                    GreenFeeProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    GuestFeeProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_golf_tees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_golf_tees_golf_courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "golf_courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_golf_tees_products_GreenFeeProductId",
                        column: x => x.GreenFeeProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_golf_tees_products_GuestFeeProductId",
                        column: x => x.GuestFeeProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tee_slots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MaximumPlayers = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tee_slots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tee_slots_golf_courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "golf_courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tee_slots_golf_tees_TeeId",
                        column: x => x.TeeId,
                        principalTable: "golf_tees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "golf_bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TeeSlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookedByMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CheckedInAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NoShowAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CapacityOverridden = table.Column<bool>(type: "boolean", nullable: false),
                    OverrideReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    OverrideByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OverriddenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_golf_bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_golf_bookings_members_BookedByMemberId",
                        column: x => x.BookedByMemberId,
                        principalTable: "members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_golf_bookings_tee_slots_TeeSlotId",
                        column: x => x.TeeSlotId,
                        principalTable: "tee_slots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "golf_booking_players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerType = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    GolfGuestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Fee = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    AccountTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivableId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_golf_booking_players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_golf_booking_players_golf_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "golf_bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_golf_booking_players_golf_guest_records_GolfGuestId",
                        column: x => x.GolfGuestId,
                        principalTable: "golf_guest_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_golf_booking_players_members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "golf_charges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingPlayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChargeType = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivableId = table.Column<Guid>(type: "uuid", nullable: true),
                    JournalId = table.Column<Guid>(type: "uuid", nullable: true),
                    WaivedReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CancellationReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_golf_charges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_golf_charges_golf_booking_players_BookingPlayerId",
                        column: x => x.BookingPlayerId,
                        principalTable: "golf_booking_players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_golf_charges_golf_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "golf_bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_golf_charges_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_golf_booking_players_BookingId_GolfGuestId",
                table: "golf_booking_players",
                columns: new[] { "BookingId", "GolfGuestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_golf_booking_players_BookingId_MemberId",
                table: "golf_booking_players",
                columns: new[] { "BookingId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_golf_booking_players_BookingId_Position",
                table: "golf_booking_players",
                columns: new[] { "BookingId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_golf_booking_players_GolfGuestId",
                table: "golf_booking_players",
                column: "GolfGuestId");

            migrationBuilder.CreateIndex(
                name: "IX_golf_booking_players_MemberId",
                table: "golf_booking_players",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_golf_bookings_BookedByMemberId",
                table: "golf_bookings",
                column: "BookedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_golf_bookings_Reference",
                table: "golf_bookings",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_golf_bookings_Status_CreatedAtUtc",
                table: "golf_bookings",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_golf_bookings_TeeSlotId_Status",
                table: "golf_bookings",
                columns: new[] { "TeeSlotId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_golf_charges_BookingId_ChargeType_BookingPlayerId",
                table: "golf_charges",
                columns: new[] { "BookingId", "ChargeType", "BookingPlayerId" });

            migrationBuilder.CreateIndex(
                name: "IX_golf_charges_BookingPlayerId",
                table: "golf_charges",
                column: "BookingPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_golf_charges_ProductId",
                table: "golf_charges",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_golf_charges_Status",
                table: "golf_charges",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_golf_courses_Code",
                table: "golf_courses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_golf_courses_Status",
                table: "golf_courses",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_golf_guest_records_FullName",
                table: "golf_guest_records",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "IX_golf_guest_records_HostMemberId",
                table: "golf_guest_records",
                column: "HostMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_golf_guest_records_Reference",
                table: "golf_guest_records",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_golf_holes_CourseId_HoleNumber",
                table: "golf_holes",
                columns: new[] { "CourseId", "HoleNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_golf_tees_Code",
                table: "golf_tees",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_golf_tees_CourseId_Status",
                table: "golf_tees",
                columns: new[] { "CourseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_golf_tees_GreenFeeProductId",
                table: "golf_tees",
                column: "GreenFeeProductId");

            migrationBuilder.CreateIndex(
                name: "IX_golf_tees_GuestFeeProductId",
                table: "golf_tees",
                column: "GuestFeeProductId");

            migrationBuilder.CreateIndex(
                name: "IX_tee_slots_CourseId",
                table: "tee_slots",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_tee_slots_Date_CourseId",
                table: "tee_slots",
                columns: new[] { "Date", "CourseId" });

            migrationBuilder.CreateIndex(
                name: "IX_tee_slots_TeeId_Date_StartTime",
                table: "tee_slots",
                columns: new[] { "TeeId", "Date", "StartTime" },
                unique: true);

            D7GolfIntegrity.Apply(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The triggers must be detached from their tables first: Revert drops them by
            // name, and "DROP TRIGGER ... ON golf_booking_players" fails once the table
            // itself is gone, which would make the whole rollback impossible.
            D7GolfIntegrity.Revert(migrationBuilder);

            migrationBuilder.DropTable(
                name: "golf_charges");

            migrationBuilder.DropTable(
                name: "golf_holes");

            migrationBuilder.DropTable(
                name: "golf_booking_players");

            migrationBuilder.DropTable(
                name: "golf_bookings");

            migrationBuilder.DropTable(
                name: "golf_guest_records");

            migrationBuilder.DropTable(
                name: "tee_slots");

            migrationBuilder.DropTable(
                name: "golf_tees");

            migrationBuilder.DropTable(
                name: "golf_courses");
        }
    }
}
