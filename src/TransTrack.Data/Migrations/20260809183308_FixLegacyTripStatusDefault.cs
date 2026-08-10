using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransTrack.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixLegacyTripStatusDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Trips created before the Status column existed were backfilled
            // by that column's own defaultValue of 0 when it was added —
            // not a valid TripStatus (Open=1, Closed=2). A trip stuck at 0
            // fails both the "== Open" and "== Closed" checks at once, which
            // hid it from Trip Transactions' default Open filter and hid
            // both the Close and Reopen buttons on the Trip editor. Anything
            // not already a real status value is treated as Open, since that
            // was always the intended state for a pre-existing trip.
            migrationBuilder.Sql("UPDATE Trips SET Status = 1 WHERE Status NOT IN (1, 2);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible: the original (invalid) value each affected
            // row held before this fix isn't recoverable.
        }
    }
}
