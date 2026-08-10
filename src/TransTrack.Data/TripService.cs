using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

public class TripService(IDbContextFactory<AppDbContext> factory)
{
    private static IQueryable<Trip> WithDetails(AppDbContext db) => db.Trips
        .Include(t => t.Vehicle).ThenInclude(v => v.Owner)
        .Include(t => t.Driver)
        .Include(t => t.Party)
        .Include(t => t.FromCity).ThenInclude(c => c.State)
        .Include(t => t.ToCity).ThenInclude(c => c.State)
        .Include(t => t.Expenses).ThenInclude(e => e.ExpenseCategory)
        .Include(t => t.Transactions);

    public async Task<List<Trip>> GetTripsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await WithDetails(db).AsNoTracking().Where(t => !t.IsDeleted).OrderByDescending(t => t.Date).ToListAsync();
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
        if (string.IsNullOrWhiteSpace(trip.ConsignorName)) throw new InvalidOperationException("Consignor name is required.");
        if (string.IsNullOrWhiteSpace(trip.ConsigneeName)) throw new InvalidOperationException("Consignee name is required.");

        await using var db = await factory.CreateDbContextAsync();

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
        entity.ConsignorName = trip.ConsignorName.Trim();
        entity.ConsignorAddress = trip.ConsignorAddress;
        entity.ConsigneeName = trip.ConsigneeName.Trim();
        entity.ConsigneeAddress = trip.ConsigneeAddress;
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

    public async Task AddExpenseAsync(Guid tripId, TripExpense expense)
    {
        await using var db = await factory.CreateDbContextAsync();
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
        db.TripExpenses.Remove(entity);
        await db.SaveChangesAsync();
    }

    // ── Numbering for print ──────────────────────────────────────────────

    /// <summary>Assigns the LR number on first print and reuses it on every
    /// reprint. Returns whether this was the first print.</summary>
    public async Task<(string LrNo, bool IsFirstPrint)> AssignLrNumberAsync(Guid tripId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == tripId) ?? throw new InvalidOperationException("Trip not found.");

        if (!string.IsNullOrWhiteSpace(trip.LrNo)) return (trip.LrNo, false);

        trip.LrNo = await NumberService.NextAsync(db, NumberService.Lr);
        await db.SaveChangesAsync();
        return (trip.LrNo, true);
    }

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
    {
        await using var db = await factory.CreateDbContextAsync();
        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == tripId) ?? throw new InvalidOperationException("Trip not found.");

        if (!string.IsNullOrWhiteSpace(trip.BillNo)) return (trip.BillNo, false);

        trip.BillNo = await NumberService.NextAsync(db, NumberService.Bill);
        await db.SaveChangesAsync();
        return (trip.BillNo, true);
    }
}
