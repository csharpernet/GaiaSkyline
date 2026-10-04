using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GaiaSkyline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _0014_ContentDraftValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DraftBoolean",
                table: "ContentTranslations",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DraftMediaAssetId",
                table: "ContentTranslations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DraftNumber",
                table: "ContentTranslations",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DraftText",
                table: "ContentTranslations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasDraft",
                table: "ContentTranslations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DraftBoolean",
                table: "ContentTranslations");

            migrationBuilder.DropColumn(
                name: "DraftMediaAssetId",
                table: "ContentTranslations");

            migrationBuilder.DropColumn(
                name: "DraftNumber",
                table: "ContentTranslations");

            migrationBuilder.DropColumn(
                name: "DraftText",
                table: "ContentTranslations");

            migrationBuilder.DropColumn(
                name: "HasDraft",
                table: "ContentTranslations");
        }
    }
}
