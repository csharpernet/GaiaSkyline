using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GaiaSkyline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _0008_GuestMagicLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuestMagicLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingReference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestMagicLinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuestMagicLinks_BookingReference",
                table: "GuestMagicLinks",
                column: "BookingReference");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuestMagicLinks");
        }
    }
}
