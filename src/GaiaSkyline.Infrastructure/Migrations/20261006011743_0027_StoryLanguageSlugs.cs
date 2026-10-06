using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GaiaSkyline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _0027_StoryLanguageSlugs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StorySlugAliases_OldSlug",
                table: "StorySlugAliases");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "StoryTranslations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LanguageCode",
                table: "StorySlugAliases",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            // Existing translations publish under their story's canonical slug (Stage 7 §4). Backfill before
            // the unique (LanguageCode, Slug) index: without it every pre-existing row holds '' and collides.
            migrationBuilder.Sql(
                """
                UPDATE st
                SET st.Slug = s.Slug
                FROM StoryTranslations st
                INNER JOIN Stories s ON s.Id = st.StoryId;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StoryTranslations_LanguageCode_Slug",
                table: "StoryTranslations",
                columns: new[] { "LanguageCode", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorySlugAliases_OldSlug_LanguageCode",
                table: "StorySlugAliases",
                columns: new[] { "OldSlug", "LanguageCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StoryTranslations_LanguageCode_Slug",
                table: "StoryTranslations");

            migrationBuilder.DropIndex(
                name: "IX_StorySlugAliases_OldSlug_LanguageCode",
                table: "StorySlugAliases");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "StoryTranslations");

            migrationBuilder.DropColumn(
                name: "LanguageCode",
                table: "StorySlugAliases");

            migrationBuilder.CreateIndex(
                name: "IX_StorySlugAliases_OldSlug",
                table: "StorySlugAliases",
                column: "OldSlug",
                unique: true);
        }
    }
}
