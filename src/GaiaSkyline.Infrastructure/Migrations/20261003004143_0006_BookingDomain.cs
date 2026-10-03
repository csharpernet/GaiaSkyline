using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GaiaSkyline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _0006_BookingDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CancellationPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tiers = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CancellationPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalCalendarBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExternalUid = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalCalendarBlocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Fees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    IsPerNight = table.Column<bool>(type: "bit", nullable: false),
                    IsPerGuest = table.Column<bool>(type: "bit", nullable: false),
                    MaxNights = table.Column<int>(type: "int", nullable: true),
                    MinAgeExempt = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PricingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    NightlyRate = table.Column<long>(type: "bigint", nullable: false),
                    MinNights = table.Column<int>(type: "int", nullable: false),
                    WeeklyDiscountPct = table.Column<int>(type: "int", nullable: false),
                    MonthlyDiscountPct = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromoCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DiscountPct = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StripeEventLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StripeEventId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StripeEventLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CheckIn = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckOut = table.Column<DateOnly>(type: "date", nullable: false),
                    Adults = table.Column<int>(type: "int", nullable: false),
                    Children = table.Column<int>(type: "int", nullable: false),
                    Infants = table.Column<int>(type: "int", nullable: false),
                    GuestName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GuestEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    GuestPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GuestCountry = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GuestLanguage = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    GuestUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NightlyRateSnapshot = table.Column<long>(type: "bigint", nullable: false),
                    Subtotal = table.Column<long>(type: "bigint", nullable: false),
                    DiscountAmount = table.Column<long>(type: "bigint", nullable: false),
                    CleaningFee = table.Column<long>(type: "bigint", nullable: false),
                    TouristTax = table.Column<long>(type: "bigint", nullable: false),
                    Total = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PromoCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StripeCustomerId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    StripePaymentIntentId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PaymentMethodType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MultibancoEntity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    MultibancoReference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PaymentExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArrivalEstimateLocal = table.Column<TimeOnly>(type: "time", nullable: true),
                    SpecialRequests = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AccountCreationRequested = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookings_PromoCodes_PromoCodeId",
                        column: x => x.PromoCodeId,
                        principalTable: "PromoCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookingDateOccupancies",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingDateOccupancies", x => x.Date);
                    table.ForeignKey(
                        name: "FK_BookingDateOccupancies_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingDateOccupancies_BookingId",
                table: "BookingDateOccupancies",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PromoCodeId",
                table: "Bookings",
                column: "PromoCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ReferenceCode",
                table: "Bookings",
                column: "ReferenceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Status",
                table: "Bookings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_StripePaymentIntentId",
                table: "Bookings",
                column: "StripePaymentIntentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalCalendarBlocks_ExternalUid",
                table: "ExternalCalendarBlocks",
                column: "ExternalUid");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalCalendarBlocks_IsActive_StartDate_EndDate",
                table: "ExternalCalendarBlocks",
                columns: new[] { "IsActive", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PricingRules_StartDate_EndDate",
                table: "PricingRules",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_Code",
                table: "PromoCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StripeEventLogs_StripeEventId",
                table: "StripeEventLogs",
                column: "StripeEventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingDateOccupancies");

            migrationBuilder.DropTable(
                name: "CancellationPolicies");

            migrationBuilder.DropTable(
                name: "ExternalCalendarBlocks");

            migrationBuilder.DropTable(
                name: "Fees");

            migrationBuilder.DropTable(
                name: "PricingRules");

            migrationBuilder.DropTable(
                name: "StripeEventLogs");

            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "PromoCodes");
        }
    }
}
