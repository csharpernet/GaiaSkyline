using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GaiaSkyline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _0022_PageMetaRobots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NoFollow",
                table: "PageMetaOverrides",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NoIndex",
                table: "PageMetaOverrides",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NoFollow",
                table: "PageMetaOverrides");

            migrationBuilder.DropColumn(
                name: "NoIndex",
                table: "PageMetaOverrides");
        }
    }
}
