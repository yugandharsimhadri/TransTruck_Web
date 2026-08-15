using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransTrack.Data.Migrations
{
    /// <inheritdoc />
    public partial class WayBillNoAndCompanyBankDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WayBillNo",
                table: "Trips",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountNo",
                table: "Companies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ifsc",
                table: "Companies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowBankDetailsOnBill",
                table: "Companies",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WayBillNo",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "BankAccountNo",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Ifsc",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ShowBankDetailsOnBill",
                table: "Companies");
        }
    }
}
