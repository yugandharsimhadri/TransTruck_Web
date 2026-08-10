using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransTrack.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompanySettingsTheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Theme",
                table: "CompanySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Theme",
                table: "CompanySettings");
        }
    }
}
