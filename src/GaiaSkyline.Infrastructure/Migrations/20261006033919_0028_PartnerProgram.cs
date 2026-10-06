using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GaiaSkyline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _0028_PartnerProgram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RefundedAmount",
                table: "Bookings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "Commissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BasisAmount = table.Column<long>(type: "bigint", nullable: false),
                    Pct = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PayoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Commissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartnerAttributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PromoCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerAttributions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartnerClicks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandingPath = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    UtcAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AnonymousId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerClicks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartnerInvites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerInvites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Partners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    PromoCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GuestDiscountPct = table.Column<int>(type: "int", nullable: false),
                    CommissionPct = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TermsVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TermsAcceptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PayoutIban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    PayoutAccountHolder = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PayoutTaxId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PayoutCountry = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Partners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Payouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodLabel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SettledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payouts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Commissions_BookingId",
                table: "Commissions",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Commissions_PartnerId_Status",
                table: "Commissions",
                columns: new[] { "PartnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Commissions_PayoutId",
                table: "Commissions",
                column: "PayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerAttributions_BookingId",
                table: "PartnerAttributions",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerAttributions_PartnerId",
                table: "PartnerAttributions",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerClicks_PartnerId_UtcAt",
                table: "PartnerClicks",
                columns: new[] { "PartnerId", "UtcAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerInvites_PartnerId",
                table: "PartnerInvites",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Partners_ApplicationId",
                table: "Partners",
                column: "ApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Partners_Email",
                table: "Partners",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Partners_UserId",
                table: "Partners",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_PartnerId_PeriodLabel",
                table: "Payouts",
                columns: new[] { "PartnerId", "PeriodLabel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Commissions");

            migrationBuilder.DropTable(
                name: "PartnerAttributions");

            migrationBuilder.DropTable(
                name: "PartnerClicks");

            migrationBuilder.DropTable(
                name: "PartnerInvites");

            migrationBuilder.DropTable(
                name: "Partners");

            migrationBuilder.DropTable(
                name: "Payouts");

            migrationBuilder.DropColumn(
                name: "RefundedAmount",
                table: "Bookings");
        }
    }
}
