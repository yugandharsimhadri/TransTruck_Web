using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransTrack.Data.Migrations
{
    /// <inheritdoc />
    public partial class TenantLeadingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TripTransactions_ApprovalStatus",
                table: "TripTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Trips_Date",
                table: "Trips");

            migrationBuilder.CreateIndex(
                name: "IX_TripTransactions_CompanyId_ApprovalStatus",
                table: "TripTransactions",
                columns: new[] { "CompanyId", "ApprovalStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_CompanyId_Date",
                table: "Trips",
                columns: new[] { "CompanyId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TripTransactions_CompanyId_ApprovalStatus",
                table: "TripTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Trips_CompanyId_Date",
                table: "Trips");

            migrationBuilder.CreateIndex(
                name: "IX_TripTransactions_ApprovalStatus",
                table: "TripTransactions",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_Date",
                table: "Trips",
                column: "Date");
        }
    }
}
