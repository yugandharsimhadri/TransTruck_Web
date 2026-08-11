using System.Text.Json;
using TransTrack.Core;

namespace TransTrack.Tests;

/// <summary>
/// The audit trail is captured in AppDbContext.SaveChanges by reading the
/// change tracker, so it covers every write path automatically. These tests
/// hold that promise: they go through the ordinary services and assert the
/// trail appeared, rather than calling any audit-writing code directly.
/// </summary>
public class AuditTrailTests
{
    [Fact]
    public async Task Adding_an_expense_is_recorded()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();

        await world.AddExpenseAsync(tripId, 500);

        var entries = await world.Audit.GetForTripAsync(tripId);
        var created = entries.Single(e => e.EntityType == nameof(TripExpense));

        Assert.Equal(nameof(AuditAction.Created), created.Action);
        Assert.Contains("500", created.Summary);
        Assert.Equal("Test Owner", created.ChangedBy);
    }

    [Fact]
    public async Task An_edit_records_which_fields_moved_and_to_what()
    {
        await using var world = await TestWorld.CreateAsync();

        await world.Maintenance.SaveAsync(new VehicleMaintenance
        {
            CompanyId = world.CompanyId,
            VehicleId = world.VehicleId,
            MaintenanceCategoryId = world.MaintenanceCategoryId,
            Date = DateTime.Today,
            Amount = 2500,
            VendorName = "City Garage"
        });

        var saved = (await world.Maintenance.GetForVehicleAsync(world.VehicleId)).Single();

        saved.Amount = 9999;
        saved.VendorName = "Other Garage";
        await world.Maintenance.SaveAsync(saved);

        var entries = await world.Audit.GetForRecordAsync(nameof(VehicleMaintenance), saved.Id);
        var updated = entries.Single(e => e.Action == nameof(AuditAction.Updated));

        Assert.NotNull(updated.Changes);
        var changes = JsonSerializer.Deserialize<List<FieldChange>>(updated.Changes!)!;

        var amount = changes.Single(c => c.field == nameof(VehicleMaintenance.Amount));
        Assert.Equal("2500", amount.from);
        Assert.Equal("9999", amount.to);

        var vendor = changes.Single(c => c.field == nameof(VehicleMaintenance.VendorName));
        Assert.Equal("City Garage", vendor.from);
        Assert.Equal("Other Garage", vendor.to);
    }

    [Fact]
    public async Task A_soft_delete_is_recorded_as_a_deletion_not_an_edit()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();
        await world.AddExpenseAsync(tripId, 500);

        var trip = await world.Trips.GetTripAsync(tripId);
        await world.Trips.DeleteExpenseAsync(trip!.Expenses.Single().Id);

        var entries = await world.Audit.GetForTripAsync(tripId);

        // It is an UPDATE at the database level (IsDeleted false -> true), but
        // the trail has to describe what the user did.
        Assert.Contains(entries, e =>
            e.EntityType == nameof(TripExpense) && e.Action == nameof(AuditAction.Deleted));
    }

    [Fact]
    public async Task Closing_and_reopening_are_described_in_plain_words()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();

        await world.Trips.CloseAsync(tripId, world.UserId);
        await world.Trips.ReopenAsync(tripId);

        var entries = await world.Audit.GetForTripAsync(tripId);

        Assert.Contains(entries, e => e.Summary == "Trip closed");
        Assert.Contains(entries, e => e.Summary == "Trip reopened");
    }

    [Fact]
    public async Task Approving_is_described_with_its_amount()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();
        var txnId = await world.AddAmountAsync(tripId, 4000);

        await world.Transactions.ApproveAsync(txnId, world.UserId, "fine");

        var entries = await world.Audit.GetForTripAsync(tripId);
        Assert.Contains(entries, e => e.Summary.Contains("approved") && e.Summary.Contains("4,000"));
    }

    [Fact]
    public async Task Raw_user_ids_are_kept_out_of_the_change_detail()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();
        await world.Trips.CloseAsync(tripId, world.UserId);

        var entries = await world.Audit.GetForTripAsync(tripId);
        var closed = entries.Single(e => e.Summary == "Trip closed");

        // The actor is already named in words; a GUID in the diff is noise.
        Assert.DoesNotContain("ByUserId", closed.Changes ?? "");
    }

    [Fact]
    public async Task Driver_ledger_entries_are_audited_too()
    {
        await using var world = await TestWorld.CreateAsync();

        await world.DriverLedger.SaveAsync(new DriverLedgerEntry
        {
            CompanyId = world.CompanyId,
            DriverId = world.DriverId,
            Date = DateTime.Today,
            Type = DriverLedgerEntryType.AdvanceGiven,
            Amount = 3000
        });

        var recent = await world.Audit.GetRecentAsync();
        Assert.Contains(recent, e => e.EntityType == nameof(DriverLedgerEntry)
                                     && e.Action == nameof(AuditAction.Created));
    }

    [Fact]
    public async Task Masters_are_not_audited()
    {
        await using var world = await TestWorld.CreateAsync();

        // Auditing is opt-in via IAuditable: rarely-changed reference data
        // carrying no money would only bury the entries that matter.
        var recent = await world.Audit.GetRecentAsync(take: 500);

        Assert.DoesNotContain(recent, e => e.EntityType == nameof(City));
        Assert.DoesNotContain(recent, e => e.EntityType == nameof(ExpenseCategory));
        Assert.DoesNotContain(recent, e => e.EntityType == nameof(Vehicle));
    }

    [Fact]
    public async Task The_trail_never_audits_itself()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();
        await world.AddExpenseAsync(tripId, 100);

        var recent = await world.Audit.GetRecentAsync(take: 500);

        // Auditing the audit table would recurse without end.
        Assert.DoesNotContain(recent, e => e.EntityType == nameof(AuditLog));
    }

    [Fact]
    public async Task One_companys_trail_is_invisible_to_another()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();
        await world.AddExpenseAsync(tripId, 500);

        var rival = await world.AddRivalCompanyAsync();
        world.CurrentUser.CompanyId = rival.CompanyId;

        Assert.Empty(await world.Audit.GetRecentAsync(take: 500));
        Assert.Empty(await world.Audit.GetForTripAsync(tripId));
    }

    private sealed record FieldChange(string field, string? from, string? to);
}
