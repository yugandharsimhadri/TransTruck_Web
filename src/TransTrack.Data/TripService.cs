using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

public class TripService(IDbContextFactory<AppDbContext> factory)
{
    // Filtered includes on the two child collections: expenses and amounts
    // are soft-deleted (the audit trail needs the row to survive), so an
    // unfiltered Include would keep showing deleted lines and — worse — keep
    // counting them in Trip.TotalExpenses / TotalApprovedReceived, which are
    // computed straight off these collections.
    private static IQueryable<Trip> WithDetails(AppDbContext db) => db.Trips
        .Include(t => t.Vehicle).ThenInclude(v => v.Owner)
        .Include(t => t.Driver)
        .Include(t => t.Party)
        .Include(t => t.FromCity).ThenInclude(c => c.State)
        .Include(t => t.ToCity).ThenInclude(c => c.State)
        // Ordered, not just filtered: without an explicit sort SQLite is free
        // to return these rows in any order, so a list of money entries could
        // reshuffle between one page load and the next. Oldest first reads as
        // a running log, which is how people work down a trip's costs.
        .Include(t => t.Expenses.Where(e => !e.IsDeleted)
            .OrderBy(e => e.Date).ThenBy(e => e.CreatedAt))
        .ThenInclude(e => e.ExpenseCategory)
        .Include(t => t.Transactions.Where(x => !x.IsDeleted)
            .OrderBy(x => x.Date).ThenBy(x => x.CreatedAt))
        // Two collection includes in one query means SQLite returns a row per
        // expense *per* amount — 10 of each fetches 100 rows to build 20.
        // Split issues one query per collection instead. Safe here because
        // every caller orders explicitly.
        .AsSplitQuery();

    /// <summary>The most rows one request will return, however large a `take`
    /// asks for — the point of paging is that no single response can grow
    /// without limit.</summary>
    public const int MaxPageSize = 100;

    public const int DefaultPageSize = 25;

    /// <summary>One page of the trips list, as a flat projection of just the
    /// columns that screen actually renders. <see cref="GetTripAsync"/>
    /// returns the whole object graph — vehicle, driver, party, both cities
    /// and every expense and amount row — which is right for the detail
    /// screen and roughly 6 KB per trip on the wire for a list that shows
    /// nine short fields. This keeps a long list cheap on a phone: the sums
    /// become SQL subqueries, so no child collection is ever materialised.
    ///
    /// Status, vehicle and sort are applied here rather than in the browser,
    /// and that is the whole reason this method takes them. Filtering a page
    /// client-side filters only the rows that page happens to contain: ask for
    /// open trips when the most recent twenty-five are all closed and you get
    /// an empty list next to a total that says otherwise. Every control the
    /// list offers has to reach the database, or paging quietly starts lying.
    ///
    /// Every sort carries a tiebreaker for the same reason. Rows that compare
    /// equal are otherwise free to come back in a different order per query,
    /// which in a paged list means a row appearing on two pages while another
    /// never appears at all.</summary>
    public async Task<TripListPage> GetTripListAsync(
        TripStatus? status = null,
        string? regNo = null,
        TripListSort sort = TripListSort.DateDesc,
        int skip = 0,
        int take = DefaultPageSize)
    {
        await using var db = await factory.CreateDbContextAsync();

        take = Math.Clamp(take, 1, MaxPageSize);
        skip = Math.Max(0, skip);

        var query = db.Trips.AsNoTracking().Where(t => !t.IsDeleted);

        if (status is { } s) query = query.Where(t => t.Status == s);
        if (!string.IsNullOrWhiteSpace(regNo)) query = query.Where(t => t.Vehicle.RegNo == regNo);

        // Counted against the same filters, before paging — this is what tells
        // the user there are more trips than the page in front of them.
        var total = await query.CountAsync();

        query = sort switch
        {
            TripListSort.DateAsc => query.OrderBy(t => t.Date).ThenBy(t => t.TripNo),
            TripListSort.AmountDesc => query.OrderByDescending(t => t.Amount).ThenByDescending(t => t.Date).ThenBy(t => t.TripNo),
            TripListSort.BalanceDesc => query
                .OrderByDescending(t => t.Amount - (t.Transactions
                    .Where(x => !x.IsDeleted && x.ApprovalStatus == ApprovalStatus.Approved)
                    .Sum(x => (decimal?)x.Amount) ?? 0m))
                .ThenByDescending(t => t.Date).ThenBy(t => t.TripNo),
            _ => query.OrderByDescending(t => t.Date).ThenBy(t => t.TripNo),
        };

        var items = await query
            .Skip(skip)
            .Take(take)
            .Select(t => new TripListItem(
                t.Id,
                t.TripNo,
                t.Date,
                t.Vehicle.RegNo,
                t.Driver.Name,
                t.Party.Name,
                t.FromCity.Name,
                t.ToCity.Name,
                t.Amount,
                t.Expenses.Where(e => !e.IsDeleted).Sum(e => (decimal?)e.Amount) ?? 0m,
                t.Transactions.Where(x => !x.IsDeleted && x.ApprovalStatus == ApprovalStatus.Approved)
                    .Sum(x => (decimal?)x.Amount) ?? 0m,
                t.Status))
            .ToListAsync();

        return new TripListPage(items, total);
    }

    public async Task<Trip?> GetTripAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await WithDetails(db).AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
    }

    /// <summary>Creates or updates a trip's header fields only — expenses are
    /// managed separately via <see cref="AddExpenseAsync"/>/<see cref="DeleteExpenseAsync"/>
    /// so the editor can add lines to a trip that has not been saved yet
    /// without re-diffing the whole collection on every save.</summary>
    public async Task<Guid> SaveTripAsync(Trip trip)
    {
        if (trip.VehicleId == Guid.Empty) throw new InvalidOperationException("Choose a vehicle.");
        if (trip.DriverId == Guid.Empty) throw new InvalidOperationException("Choose a driver.");
        if (trip.PartyId == Guid.Empty) throw new InvalidOperationException("Choose a party.");
        if (trip.FromCityId == Guid.Empty || trip.ToCityId == Guid.Empty) throw new InvalidOperationException("Choose the from and to cities.");

        // Consignor and consignee are deliberately not required: plenty of
        // trips are booked against the billing party alone, with no separate
        // consignor/consignee to name. Blank simply prints as "—" on the LR.

        // Booking allocates a trip number, so it goes through the retrying
        // allocator: two people booking at the same instant must get TRP00007
        // and TRP00008, not a collision on the unique index.
        return await NumberService.AllocateAsync(factory, async db =>
            await SaveTripCoreAsync(db, trip));
    }

    private static async Task<Guid> SaveTripCoreAsync(AppDbContext db, Trip trip)
    {
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == trip.VehicleId)
                      ?? throw new InvalidOperationException("Vehicle not found.");

        var entity = trip.Id == Guid.Empty ? null : await db.Trips.FirstOrDefaultAsync(x => x.Id == trip.Id);
        var isNew = entity is null;
        entity ??= new Trip();

        entity.Date = trip.Date;
        entity.VehicleId = trip.VehicleId;
        entity.DriverId = trip.DriverId;
        entity.PartyId = trip.PartyId;
        entity.FromCityId = trip.FromCityId;
        entity.FromAddress = trip.FromAddress;
        entity.ToCityId = trip.ToCityId;
        entity.ToAddress = trip.ToAddress;
        entity.ConsignorName = (trip.ConsignorName ?? string.Empty).Trim();
        entity.ConsignorAddress = trip.ConsignorAddress;
        entity.ConsigneeName = (trip.ConsigneeName ?? string.Empty).Trim();
        entity.ConsigneeAddress = trip.ConsigneeAddress;
        entity.WayBillNo = string.IsNullOrWhiteSpace(trip.WayBillNo) ? null : trip.WayBillNo.Trim();
        entity.Weight = trip.Weight;
        entity.Rate = trip.Rate;
        entity.Amount = trip.Amount;
        entity.StartReading = trip.StartReading;
        entity.EndReading = trip.EndReading;
        entity.CommissionAmount = vehicle.Ownership == VehicleOwnership.Other ? trip.CommissionAmount : null;
        entity.Remarks = trip.Remarks;

        if (isNew)
        {
            entity.TripNo = await NumberService.NextAsync(db, NumberService.Trip);
            db.Trips.Add(entity);
        }

        await db.SaveChangesAsync();
        return entity.Id;
    }

    public async Task DeleteTripAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.Trips.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return;
        entity.IsDeleted = true;
        await db.SaveChangesAsync();
    }

    // ── Expenses ──────────────────────────────────────────────────────────

    /// <summary>The message every "you can't change a closed trip" refusal
    /// uses, so the wording — and the instruction on how to proceed — is
    /// identical wherever the rule is hit.</summary>
    public const string ClosedTripMessage =
        "This trip is closed. Reopen it first if you need to make changes.";

    /// <summary>A closed trip is a settled one: its expenses and amounts are
    /// final. Reopening is always available and is a deliberate, audited act,
    /// so this blocks rather than silently allowing edits after settlement.
    /// Enforced here in the service, not only in the controller, so every
    /// caller is covered.</summary>
    private static async Task EnsureTripOpenAsync(AppDbContext db, Guid tripId)
    {
        var status = await db.Trips.Where(t => t.Id == tripId)
            .Select(t => (TripStatus?)t.Status).FirstOrDefaultAsync();

        if (status is null) throw new InvalidOperationException("Trip not found.");
        if (status == TripStatus.Closed) throw new InvalidOperationException(ClosedTripMessage);
    }

    public async Task AddExpenseAsync(Guid tripId, TripExpense expense)
    {
        await using var db = await factory.CreateDbContextAsync();
        await EnsureTripOpenAsync(db, tripId);

        expense.Id = Guid.NewGuid();
        expense.TripId = tripId;
        db.TripExpenses.Add(expense);
        await db.SaveChangesAsync();
    }

    public async Task DeleteExpenseAsync(Guid expenseId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.TripExpenses.FirstOrDefaultAsync(x => x.Id == expenseId);
        if (entity is null) return;

        await EnsureTripOpenAsync(db, entity.TripId);

        // Soft delete, not a row removal: the audit trail has to be able to
        // show what was taken off a trip and by whom, which a hard delete
        // would erase along with the row.
        entity.IsDeleted = true;
        await db.SaveChangesAsync();
    }

    // ── Numbering for print ──────────────────────────────────────────────

    /// <summary>Assigns the LR number on first print and reuses it on every
    /// reprint. Returns whether this was the first print.</summary>
    public async Task<(string LrNo, bool IsFirstPrint)> AssignLrNumberAsync(Guid tripId)
        => await NumberService.AllocateAsync(factory, async db =>
        {
            var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == tripId)
                       ?? throw new InvalidOperationException("Trip not found.");

            if (!string.IsNullOrWhiteSpace(trip.LrNo)) return (trip.LrNo, false);

            trip.LrNo = await NumberService.NextAsync(db, NumberService.Lr);
            await db.SaveChangesAsync();
            return (trip.LrNo, true);
        });

    // ── Close / reopen ────────────────────────────────────────────────────

    /// <summary>Marks a trip Closed once its amounts have been reconciled.
    /// Never a delete and never automatic — always a deliberate action on
    /// the Close Trip screen, and always reversible via <see cref="ReopenAsync"/>.</summary>
    public async Task CloseAsync(Guid tripId, Guid? closedByUserId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == tripId) ?? throw new InvalidOperationException("Trip not found.");

        trip.Status = TripStatus.Closed;
        trip.ClosedOn = DateTime.Now;
        trip.ClosedByUserId = closedByUserId;

        await db.SaveChangesAsync();
    }

    public async Task ReopenAsync(Guid tripId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == tripId) ?? throw new InvalidOperationException("Trip not found.");

        trip.Status = TripStatus.Open;
        trip.ClosedOn = null;
        trip.ClosedByUserId = null;

        await db.SaveChangesAsync();
    }

    public async Task<(string BillNo, bool IsFirstPrint)> AssignBillNumberAsync(Guid tripId)
        => await NumberService.AllocateAsync(factory, async db =>
        {
            var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == tripId)
                       ?? throw new InvalidOperationException("Trip not found.");

            if (!string.IsNullOrWhiteSpace(trip.BillNo)) return (trip.BillNo, false);

            trip.BillNo = await NumberService.NextAsync(db, NumberService.Bill);
            await db.SaveChangesAsync();
            return (trip.BillNo, true);
        });
}

/// <summary>One row of the trips list — flat, and only what that screen
/// draws. Balance is derived here rather than stored so it can never drift
/// from the two figures it comes from.</summary>
public enum TripListSort { DateDesc, DateAsc, BalanceDesc, AmountDesc }

/// <summary>A page of trips, and how many the filters match in total, so the
/// list can say "25 of 312" rather than leaving the user guessing whether
/// there is more behind it.</summary>
public record TripListPage(IReadOnlyList<TripListItem> Items, int Total);

public record TripListItem(
    Guid Id,
    string TripNo,
    DateTime Date,
    string VehicleRegNo,
    string DriverName,
    string PartyName,
    string FromCity,
    string ToCity,
    decimal Amount,
    decimal TotalExpenses,
    decimal TotalApprovedReceived,
    TripStatus Status)
{
    public decimal BalanceReceivable => Amount - TotalApprovedReceived;
}
