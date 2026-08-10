using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransTrack.Data.Migrations
{
    /// <inheritdoc />
    public partial class TripStatusAndCompanyLogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClosedByUserId",
                table: "Trips",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedOn",
                table: "Trips",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Trips",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LogoBase64",
                table: "CompanySettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoFileName",
                table: "CompanySettings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosedByUserId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ClosedOn",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "LogoBase64",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "LogoFileName",
                table: "CompanySettings");
        }
    }
}
