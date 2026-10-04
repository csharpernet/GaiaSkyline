using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GaiaSkyline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _0017_MediaSeoFilenameAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaAssetAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MediaAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OldSlug = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAssetAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaAssetAliases_MediaAssets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssetAliases_MediaAssetId",
                table: "MediaAssetAliases",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssetAliases_OldSlug",
                table: "MediaAssetAliases",
                column: "OldSlug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaAssetAliases");
        }
    }
}
