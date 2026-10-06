using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GaiaSkyline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _0030_BookingDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookingDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingReference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingDocuments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingDocuments_BookingReference_DocumentType_Language",
                table: "BookingDocuments",
                columns: new[] { "BookingReference", "DocumentType", "Language" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingDocuments");
        }
    }
}
