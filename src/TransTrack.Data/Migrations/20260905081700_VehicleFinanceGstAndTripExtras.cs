using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransTrack.Data.Migrations
{
    /// <inheritdoc />
    public partial class VehicleFinanceGstAndTripExtras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EmiAmount",
                table: "Vehicles",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmiDayOfMonth",
                table: "Vehicles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LoanAmount",
                table: "Vehicles",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LoanEndDate",
                table: "Vehicles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LoanStartDate",
                table: "Vehicles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GstPercentage",
                table: "Trips",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LoadingCharge",
                table: "Trips",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnloadingCharge",
                table: "Trips",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "WaymentCharge",
                table: "Trips",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GstPercentage",
                table: "Parties",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGstEnabled",
                table: "Parties",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "VehicleExpenseSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VehicleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    Recurrence = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Remarks = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleExpenseSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleExpenseSchedules_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VehicleExpenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VehicleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleExpenses_VehicleExpenseSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "VehicleExpenseSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VehicleExpenses_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleExpenses_CompanyId_Date",
                table: "VehicleExpenses",
                columns: new[] { "CompanyId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleExpenses_ScheduleId",
                table: "VehicleExpenses",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleExpenses_VehicleId",
                table: "VehicleExpenses",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleExpenseSchedules_VehicleId",
                table: "VehicleExpenseSchedules",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleExpenses");

            migrationBuilder.DropTable(
                name: "VehicleExpenseSchedules");

            migrationBuilder.DropColumn(
                name: "EmiAmount",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "EmiDayOfMonth",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "LoanAmount",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "LoanEndDate",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "LoanStartDate",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "GstPercentage",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "LoadingCharge",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "UnloadingCharge",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "WaymentCharge",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "GstPercentage",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "IsGstEnabled",
                table: "Parties");
        }
    }
}
