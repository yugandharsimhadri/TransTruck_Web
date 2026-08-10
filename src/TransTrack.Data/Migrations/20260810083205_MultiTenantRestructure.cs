using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransTrack.Data.Migrations
{
    /// <inheritdoc />
    public partial class MultiTenantRestructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanySettings");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_RegNo",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Trips_TripNo",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Drivers_EmployeeCode",
                table: "Drivers");

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Vehicles",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "VehicleMaintenances",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "TripTransactions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Trips",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "TripExpenses",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "States",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Parties",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Owners",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "MaintenanceCategories",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "ExpenseCategories",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Drivers",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "DriverLedgerEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Counters",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Cities",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerName = table.Column<string>(type: "TEXT", nullable: false),
                    OwnerPhone = table.Column<string>(type: "TEXT", nullable: false),
                    LicenseStartsOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LicenseExpiresOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", nullable: false),
                    Tagline = table.Column<string>(type: "TEXT", nullable: true),
                    AddressLine = table.Column<string>(type: "TEXT", nullable: true),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    Cell = table.Column<string>(type: "TEXT", nullable: true),
                    Pan = table.Column<string>(type: "TEXT", nullable: true),
                    Gstin = table.Column<string>(type: "TEXT", nullable: true),
                    JurisdictionNote = table.Column<string>(type: "TEXT", nullable: true),
                    LogoBase64 = table.Column<string>(type: "TEXT", nullable: true),
                    LogoFileName = table.Column<string>(type: "TEXT", nullable: true),
                    Theme = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_CompanyId_RegNo",
                table: "Vehicles",
                columns: new[] { "CompanyId", "RegNo" },
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_CompanyId_TripNo",
                table: "Trips",
                columns: new[] { "CompanyId", "TripNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_CompanyId_EmployeeCode",
                table: "Drivers",
                columns: new[] { "CompanyId", "EmployeeCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Counters_CompanyId_Name",
                table: "Counters",
                columns: new[] { "CompanyId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_CompanyId_RegNo",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Trips_CompanyId_TripNo",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Drivers_CompanyId_EmployeeCode",
                table: "Drivers");

            migrationBuilder.DropIndex(
                name: "IX_Counters_CompanyId_Name",
                table: "Counters");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "VehicleMaintenances");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "TripTransactions");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "TripExpenses");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "States");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Owners");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "MaintenanceCategories");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ExpenseCategories");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "DriverLedgerEntries");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Counters");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Cities");

            migrationBuilder.CreateTable(
                name: "CompanySettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AddressLine = table.Column<string>(type: "TEXT", nullable: true),
                    Cell = table.Column<string>(type: "TEXT", nullable: true),
                    CompanyName = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Gstin = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    JurisdictionNote = table.Column<string>(type: "TEXT", nullable: true),
                    LogoBase64 = table.Column<string>(type: "TEXT", nullable: true),
                    LogoFileName = table.Column<string>(type: "TEXT", nullable: true),
                    Pan = table.Column<string>(type: "TEXT", nullable: true),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    Tagline = table.Column<string>(type: "TEXT", nullable: true),
                    Theme = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanySettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_RegNo",
                table: "Vehicles",
                column: "RegNo",
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TripNo",
                table: "Trips",
                column: "TripNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_EmployeeCode",
                table: "Drivers",
                column: "EmployeeCode",
                unique: true);
        }
    }
}
