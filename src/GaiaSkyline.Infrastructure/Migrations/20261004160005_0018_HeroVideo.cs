using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GaiaSkyline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _0018_HeroVideo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HeroVideos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    SourceByteSize = table.Column<long>(type: "bigint", nullable: false),
                    SourceDurationSec = table.Column<double>(type: "float", nullable: false),
                    TrimStartSec = table.Column<double>(type: "float", nullable: false),
                    TrimEndSec = table.Column<double>(type: "float", nullable: false),
                    CrossfadeSec = table.Column<double>(type: "float", nullable: false),
                    FocalX = table.Column<double>(type: "float", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsLive = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DesktopPosterBlobUri = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    DesktopPosterLqip = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    MobilePosterBlobUri = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    MobilePosterLqip = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadyAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeroVideos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HeroVideoRenditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HeroVideoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BlobUri = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    ByteSize = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeroVideoRenditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HeroVideoRenditions_HeroVideos_HeroVideoId",
                        column: x => x.HeroVideoId,
                        principalTable: "HeroVideos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HeroVideoRenditions_HeroVideoId_Kind",
                table: "HeroVideoRenditions",
                columns: new[] { "HeroVideoId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HeroVideos_IsLive",
                table: "HeroVideos",
                column: "IsLive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HeroVideoRenditions");

            migrationBuilder.DropTable(
                name: "HeroVideos");
        }
    }
}
