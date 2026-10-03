using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GaiaSkyline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _0011_ExternalCalendarSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExternalCalendarBlocks_ExternalUid",
                table: "ExternalCalendarBlocks");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceId",
                table: "ExternalCalendarBlocks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BookingConflicts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingReference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingConflicts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalCalendarSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IcsUrlProtected = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LastHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LastSuccessUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ConsecutiveFailures = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalCalendarSources", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalCalendarBlocks_SourceId_ExternalUid",
                table: "ExternalCalendarBlocks",
                columns: new[] { "SourceId", "ExternalUid" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingConflicts_BookingReference_SourceName_StartDate_EndDate",
                table: "BookingConflicts",
                columns: new[] { "BookingReference", "SourceName", "StartDate", "EndDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalCalendarSources_Name",
                table: "ExternalCalendarSources",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingConflicts");

            migrationBuilder.DropTable(
                name: "ExternalCalendarSources");

            migrationBuilder.DropIndex(
                name: "IX_ExternalCalendarBlocks_SourceId_ExternalUid",
                table: "ExternalCalendarBlocks");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "ExternalCalendarBlocks");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalCalendarBlocks_ExternalUid",
                table: "ExternalCalendarBlocks",
                column: "ExternalUid");
        }
    }
}
