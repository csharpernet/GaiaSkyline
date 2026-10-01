using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GaiaSkyline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _0002_Content : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Section = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentBlocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    BlobUri = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    PosterBlobUri = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    DurationSec = table.Column<int>(type: "int", nullable: true),
                    ByteSize = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaCollections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaCollections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    GuestFirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GuestLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StayedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentTranslations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentBlockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ValueText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueMediaAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValueNumber = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    ValueBoolean = table.Column<bool>(type: "bit", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentTranslations_ContentBlocks_ContentBlockId",
                        column: x => x.ContentBlockId,
                        principalTable: "ContentBlocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentTranslations_MediaAssets_ValueMediaAssetId",
                        column: x => x.ValueMediaAssetId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MediaCollectionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MediaCollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MediaAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsHero = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaCollectionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaCollectionItems_MediaAssets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MediaCollectionItems_MediaCollections_MediaCollectionId",
                        column: x => x.MediaCollectionId,
                        principalTable: "MediaCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentBlocks_Key",
                table: "ContentBlocks",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentBlocks_Section",
                table: "ContentBlocks",
                column: "Section");

            migrationBuilder.CreateIndex(
                name: "IX_ContentTranslations_ContentBlockId_LanguageCode",
                table: "ContentTranslations",
                columns: new[] { "ContentBlockId", "LanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentTranslations_ValueMediaAssetId",
                table: "ContentTranslations",
                column: "ValueMediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaCollectionItems_MediaAssetId",
                table: "MediaCollectionItems",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaCollectionItems_MediaCollectionId_MediaAssetId",
                table: "MediaCollectionItems",
                columns: new[] { "MediaCollectionId", "MediaAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaCollections_Key",
                table: "MediaCollections",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentTranslations");

            migrationBuilder.DropTable(
                name: "MediaCollectionItems");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "ContentBlocks");

            migrationBuilder.DropTable(
                name: "MediaAssets");

            migrationBuilder.DropTable(
                name: "MediaCollections");
        }
    }
}
