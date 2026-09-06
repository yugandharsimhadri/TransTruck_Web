using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

/// <summary>
/// Settling a party's open trips in one go.
///
/// The flow this exists for: a party pays for a month's loads with one
/// transfer. Before, that meant opening each trip, recording a receipt,
/// getting each one approved, then closing each one — twenty trips, sixty
/// actions, and no single figure anywhere that said what the party actually
/// paid.
///
/// Here the money is still recorded per trip, because that is where a balance
/// lives and what every report reads. What is shared is the decision: one
/// approval covers the lot, and approving it posts every receipt and closes
/// every trip in the same transaction. Either the whole settlement lands or
/// none of it does — a half-applied settlement is the one outcome worse than
/// doing it by hand.
/// </summary>
public class SettlementService(IDbContextFactory<AppDbContext> factory)
{
    /// <summary>A settlement with no lines settles nothing, and one covering
    /// hundreds of trips is a sign something has gone wrong upstream rather
    /// than a month's work.</summary>
    public const int MaxTripsPerSettlement = 200;

    public const string NothingSelectedMessage = "Choose at least one trip to settle.";

    /// <summary>
    /// The party's trips that are still open and still owed on, newest last so
    /// the list reads like a statement.
    ///
    /// Balance is computed here rather than in SQL because it depends on the
    /// trip's GST rounding, which has no faithful SQL translation — the rows
    /// come back with their approved receipts attached and the arithmetic
    /// happens in one place, <see cref="Trip.BalanceReceivable"/>.
    /// </summary>
    public async Task<List<SettleableTrip>> GetSettleableTripsAsync(
        Guid partyId, DateTime? from = null, DateTime? to = null)
    {
        await using var db = await factory.CreateDbContextAsync();

        var query = db.Trips.AsNoTracking()
            .Include(t => t.Vehicle)
            .Include(t => t.Transactions.Where(x => !x.IsDeleted))
            .Where(t => t.PartyId == partyId && !t.IsDeleted && t.Status == TripStatus.Open);

        // A settlement is nearly always "the month they just paid for", so the
        // period narrows the list rather than making someone find those trips
        // among a year of them. Inclusive at both ends.
        if (from is { } f) query = query.Where(t => t.Date >= f.Date);
        if (to is { } t2) query = query.Where(t => t.Date <= t2.Date);

        var trips = await query
            .OrderBy(t => t.Date).ThenBy(t => t.TripNo)
            .ToListAsync();

        return trips
            // A trip already paid in full is not settleable — including it would
            // post a zero receipt and muddy the total the owner is confirming.
            .Where(t => t.BalanceReceivable > 0)
            .Select(t => new SettleableTrip(
                t.Id, t.TripNo, t.LrNo, t.Date, t.Vehicle.RegNo,
                t.GrandTotal, t.TotalApprovedReceived, t.BalanceReceivable))
            .ToList();
    }

    /// <summary>
    /// Records the payment against every selected trip and puts the whole thing
    /// up for one approval. Nothing moves on the books yet: the receipts are
    /// Pending, exactly as a single receipt would be, so balances only change
    /// when an Owner approves.
    /// </summary>
    public async Task<Guid> CreateAsync(
        Guid partyId,
        DateTime date,
        PaymentMode paymentMode,
        IReadOnlyCollection<Guid> tripIds,
        string? remarks,
        Guid? enteredByUserId)
    {
        if (tripIds.Count == 0) throw new InvalidOperationException(NothingSelectedMessage);
        if (tripIds.Count > MaxTripsPerSettlement)
            throw new InvalidOperationException(
                $"A settlement can cover at most {MaxTripsPerSettlement} trips. Split it into more than one.");

        await using var db = await factory.CreateDbContextAsync();

        var party = await db.Parties.FirstOrDefaultAsync(p => p.Id == partyId && !p.IsDeleted)
                    ?? throw new InvalidOperationException("Party not found.");

        var ids = tripIds.Distinct().ToList();

        var trips = await db.Trips
            .Include(t => t.Transactions.Where(x => !x.IsDeleted))
            .Where(t => ids.Contains(t.Id) && !t.IsDeleted)
            .ToListAsync();

        if (trips.Count != ids.Count)
            throw new InvalidOperationException("Some of those trips no longer exist.");

        // Every guard is re-checked here rather than trusted from the screen:
        // the list the user selected from may be minutes old, and in that time
        // a trip can be closed, paid, or reassigned to another party.
        foreach (var trip in trips)
        {
            if (trip.PartyId != partyId)
                throw new InvalidOperationException($"Trip {trip.TripNo} is not billed to {party.Name}.");

            if (trip.Status == TripStatus.Closed)
                throw new InvalidOperationException($"Trip {trip.TripNo} has already been closed.");

            if (trip.BalanceReceivable <= 0)
                throw new InvalidOperationException($"Trip {trip.TripNo} has nothing left to pay.");
        }

        var settlement = new Settlement
        {
            Id = Guid.NewGuid(),
            PartyId = partyId,
            Date = date.Date,
            PaymentMode = paymentMode,
            Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim(),
            EnteredByUserId = enteredByUserId,
            ApprovalStatus = ApprovalStatus.Pending,
        };

        db.Settlements.Add(settlement);

        foreach (var trip in trips)
        {
            db.TripTransactions.Add(new TripTransaction
            {
                Id = Guid.NewGuid(),
                TripId = trip.Id,
                SettlementId = settlement.Id,
                Date = settlement.Date,
                // The whole outstanding balance: this is a settlement, not a
                // part payment. Anything less is recorded trip by trip.
                Amount = trip.BalanceReceivable,
                PaymentMode = paymentMode,
                ReceiptType = ReceiptType.Payment,
                Remarks = settlement.Remarks,
                EnteredByUserId = enteredByUserId,
                ApprovalStatus = ApprovalStatus.Pending,
            });
        }

        await db.SaveChangesAsync();
        return settlement.Id;
    }

    /// <summary>Settlements waiting on an Owner, with their lines, so the
    /// Approvals screen can show one row carrying the whole figure.</summary>
    public async Task<List<Settlement>> GetPendingAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Settlements.AsNoTracking()
            .Include(s => s.Party)
            .Include(s => s.Transactions.Where(t => !t.IsDeleted)).ThenInclude(t => t.Trip)
            .Where(s => s.ApprovalStatus == ApprovalStatus.Pending && !s.IsDeleted)
            .OrderBy(s => s.Date)
            .ToListAsync();
    }

    public async Task<Settlement?> GetAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Settlements.AsNoTracking()
            .Include(s => s.Party)
            .Include(s => s.Transactions.Where(t => !t.IsDeleted)).ThenInclude(t => t.Trip)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
    }

    public const string AlreadyDecidedMessage =
        "This settlement has already been decided and can no longer be changed.";

    /// <summary>
    /// Approves the whole settlement: every receipt becomes Approved and every
    /// trip it paid for is closed, in one SaveChanges so the two can never come
    /// apart. A trip closed but unpaid — or paid but left open — is exactly the
    /// mess this feature exists to prevent.
    /// </summary>
    public async Task ApproveAsync(Guid settlementId, Guid approvedByUserId, string? remarks)
    {
        await using var db = await factory.CreateDbContextAsync();

        var settlement = await db.Settlements
            .Include(s => s.Transactions.Where(t => !t.IsDeleted))
            .FirstOrDefaultAsync(s => s.Id == settlementId && !s.IsDeleted)
            ?? throw new InvalidOperationException("Settlement not found.");

        if (settlement.ApprovalStatus != ApprovalStatus.Pending)
            throw new InvalidOperationException(AlreadyDecidedMessage);

        var now = DateTime.Now;
        var trimmed = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();

        settlement.ApprovalStatus = ApprovalStatus.Approved;
        settlement.ApprovedByUserId = approvedByUserId;
        settlement.ApprovedOn = now;
        settlement.ApprovalRemarks = trimmed;

        var tripIds = settlement.Transactions.Select(t => t.TripId).ToList();

        foreach (var transaction in settlement.Transactions)
        {
            transaction.ApprovalStatus = ApprovalStatus.Approved;
            transaction.ApprovedByUserId = approvedByUserId;
            transaction.ApprovedOn = now;
            transaction.ApprovalRemarks = trimmed;
        }

        var trips = await db.Trips.Where(t => tripIds.Contains(t.Id)).ToListAsync();
        foreach (var trip in trips)
        {
            // Already closed is not an error: someone may have closed it by
            // hand while this waited. The receipt still belongs on it.
            if (trip.Status == TripStatus.Closed) continue;

            trip.Status = TripStatus.Closed;
            trip.ClosedOn = now;
            trip.ClosedByUserId = approvedByUserId;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Rejects the whole settlement. The trips are left exactly as
    /// they were — open, unpaid — because a rejected settlement means the money
    /// was never accepted, not that it was accepted and reversed.</summary>
    public async Task RejectAsync(Guid settlementId, Guid approvedByUserId, string? remarks)
    {
        await using var db = await factory.CreateDbContextAsync();

        var settlement = await db.Settlements
            .Include(s => s.Transactions.Where(t => !t.IsDeleted))
            .FirstOrDefaultAsync(s => s.Id == settlementId && !s.IsDeleted)
            ?? throw new InvalidOperationException("Settlement not found.");

        if (settlement.ApprovalStatus != ApprovalStatus.Pending)
            throw new InvalidOperationException(AlreadyDecidedMessage);

        var now = DateTime.Now;
        var trimmed = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();

        settlement.ApprovalStatus = ApprovalStatus.Rejected;
        settlement.ApprovedByUserId = approvedByUserId;
        settlement.ApprovedOn = now;
        settlement.ApprovalRemarks = trimmed;

        foreach (var transaction in settlement.Transactions)
        {
            transaction.ApprovalStatus = ApprovalStatus.Rejected;
            transaction.ApprovedByUserId = approvedByUserId;
            transaction.ApprovedOn = now;
            transaction.ApprovalRemarks = trimmed;
        }

        await db.SaveChangesAsync();
    }
}

/// <summary>One line on the "what does this party still owe" list: enough to
/// recognise the trip, and the three figures that decide whether to tick it.</summary>
public record SettleableTrip(
    Guid TripId,
    string TripNo,
    string? LrNo,
    DateTime Date,
    string VehicleRegNo,
    decimal GrandTotal,
    decimal Received,
    decimal Balance);
