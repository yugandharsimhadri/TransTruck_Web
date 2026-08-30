using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransTrack.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdvancePaymentReceiptType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 2 = ReceiptType.Payment, not the scaffolded 0 (which isn't a
            // valid value of this enum at all — Advance is 1, Payment is 2).
            // Every row recorded before this column existed backfills to
            // Payment: the safer default of the two, since a stray "Advance"
            // tag on old data would be more misleading than a stray "Payment"
            // one, and a new row an older client saves without setting this
            // explicitly should land the same way.
            migrationBuilder.AddColumn<int>(
                name: "ReceiptType",
                table: "TripTransactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiptType",
                table: "TripTransactions");
        }
    }
}
