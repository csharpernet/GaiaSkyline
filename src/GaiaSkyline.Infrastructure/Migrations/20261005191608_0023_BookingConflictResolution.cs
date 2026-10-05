using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GaiaSkyline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _0023_BookingConflictResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAtUtc",
                table: "BookingConflicts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedNote",
                table: "BookingConflicts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResolvedAtUtc",
                table: "BookingConflicts");

            migrationBuilder.DropColumn(
                name: "ResolvedNote",
                table: "BookingConflicts");
        }
    }
}
