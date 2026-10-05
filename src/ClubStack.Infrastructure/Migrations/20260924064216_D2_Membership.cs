using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubStack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class D2_Membership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "membership_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    EligibilityRules = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    RenewalPeriodMonths = table.Column<int>(type: "integer", nullable: false),
                    PrivilegeConfiguration = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    GuestRules = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    AccountRules = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    CredentialRules = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membership_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "persons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PreferredName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IdNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DateOfBirth = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    AddressLine1 = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AddressLine2 = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    City = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Region = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PostalCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Country = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_persons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "membership_fees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeType = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    IsProrationEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membership_fees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_membership_fees_membership_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "membership_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberNumber = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_members_persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "households",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Reference = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PrimaryMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_households", x => x.Id);
                    table.ForeignKey(
                        name: "FK_households_members_PrimaryMemberId",
                        column: x => x.PrimaryMemberId,
                        principalTable: "members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "membership_applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SponsorName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ReferrerName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    RequiredDocuments = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApproverUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membership_applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_membership_applications_members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_membership_applications_membership_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "membership_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_membership_applications_persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "membership_dependants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    DependantPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationType = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membership_dependants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_membership_dependants_members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_membership_dependants_persons_DependantPersonId",
                        column: x => x.DependantPersonId,
                        principalTable: "persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "memberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RenewalDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PreviousMembershipId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActivatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_memberships_members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_memberships_membership_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "membership_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_memberships_memberships_PreviousMembershipId",
                        column: x => x.PreviousMembershipId,
                        principalTable: "memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "household_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_household_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_household_members_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_household_members_persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "membership_fee_assessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeType = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    IsProrated = table.Column<bool>(type: "boolean", nullable: false),
                    AssessmentDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Source = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AdjustmentReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membership_fee_assessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_membership_fee_assessments_memberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "membership_status_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousState = table.Column<int>(type: "integer", nullable: false),
                    NewState = table.Column<int>(type: "integer", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membership_status_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_membership_status_history_memberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_household_members_HouseholdId_PersonId",
                table: "household_members",
                columns: new[] { "HouseholdId", "PersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_household_members_PersonId",
                table: "household_members",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_households_PrimaryMemberId",
                table: "households",
                column: "PrimaryMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_households_Reference",
                table: "households",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_members_MemberNumber",
                table: "members",
                column: "MemberNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_members_PersonId",
                table: "members",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_membership_applications_CategoryId",
                table: "membership_applications",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_membership_applications_MemberId",
                table: "membership_applications",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_membership_applications_PersonId_CategoryId",
                table: "membership_applications",
                columns: new[] { "PersonId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_membership_applications_Status",
                table: "membership_applications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_membership_categories_Code",
                table: "membership_categories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_membership_dependants_DependantPersonId",
                table: "membership_dependants",
                column: "DependantPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_membership_dependants_MemberId_DependantPersonId",
                table: "membership_dependants",
                columns: new[] { "MemberId", "DependantPersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_membership_fee_assessments_MembershipId",
                table: "membership_fee_assessments",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_membership_fee_assessments_Status",
                table: "membership_fee_assessments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_membership_fees_CategoryId_FeeType",
                table: "membership_fees",
                columns: new[] { "CategoryId", "FeeType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_membership_status_history_MembershipId_ChangedAtUtc",
                table: "membership_status_history",
                columns: new[] { "MembershipId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_memberships_CategoryId",
                table: "memberships",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_memberships_MemberId",
                table: "memberships",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_memberships_PreviousMembershipId",
                table: "memberships",
                column: "PreviousMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_memberships_Status",
                table: "memberships",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_persons_LegalName",
                table: "persons",
                column: "LegalName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "household_members");

            migrationBuilder.DropTable(
                name: "membership_applications");

            migrationBuilder.DropTable(
                name: "membership_dependants");

            migrationBuilder.DropTable(
                name: "membership_fee_assessments");

            migrationBuilder.DropTable(
                name: "membership_fees");

            migrationBuilder.DropTable(
                name: "membership_status_history");

            migrationBuilder.DropTable(
                name: "households");

            migrationBuilder.DropTable(
                name: "memberships");

            migrationBuilder.DropTable(
                name: "members");

            migrationBuilder.DropTable(
                name: "membership_categories");

            migrationBuilder.DropTable(
                name: "persons");
        }
    }
}
