using Microsoft.EntityFrameworkCore;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Tests;

/// <summary>
/// A throwaway company on a throwaway database, wired up the way the API
/// wires the real thing.
///
/// Deliberately a real SQLite file rather than the in-memory provider: the
/// behaviour most worth testing here — the global tenant query filters, the
/// unique indexes, the concurrency token on the counter, soft-delete
/// filtering — is all decided by the relational provider, and the in-memory
/// one would happily pass tests that fail against the database the app
/// actually ships with.
/// </summary>
public sealed class TestWorld : IAsyncDisposable
{
    private readonly string _dbPath;

    public IDbContextFactory<AppDbContext> Factory { get; }
    public MutableUserContext CurrentUser { get; }

    public Guid CompanyId { get; private set; }
    public Guid UserId { get; } = Guid.NewGuid();

    // Seeded masters, so a test can book a trip in one line.
    public Guid VehicleId { get; private set; }
    public Guid OtherOwnerVehicleId { get; private set; }
    public Guid DriverId { get; private set; }
    public Guid PartyId { get; private set; }
    public Guid FromCityId { get; private set; }
    public Guid ToCityId { get; private set; }
    public Guid ExpenseCategoryId { get; private set; }
    public Guid MaintenanceCategoryId { get; private set; }

    public TripService Trips { get; }
    public TripTransactionService Transactions { get; }
    public MaintenanceService Maintenance { get; }
    public DriverLedgerService DriverLedger { get; }
    public AuditService Audit { get; }

    private TestWorld()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"transtruck-test-{Guid.NewGuid():N}.db");

        CurrentUser = new MutableUserContext { UserId = UserId };

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        Factory = new TestContextFactory(options, CurrentUser);

        Trips = new TripService(Factory);
        Transactions = new TripTransactionService(Factory);
        Maintenance = new MaintenanceService(Factory);
        DriverLedger = new DriverLedgerService(Factory);
        Audit = new AuditService(Factory);
    }

    public static async Task<TestWorld> CreateAsync()
    {
        var world = new TestWorld();
        await world.SeedAsync();
        return world;
    }

    private async Task SeedAsync()
    {
        await using (var db = await Factory.CreateDbContextAsync())
        {
            await db.Database.MigrateAsync();
        }

        // Seeding runs with no tenant context, so the global filters would
        // hide these rows from the very context writing them — set CompanyId
        // explicitly and read back with IgnoreQueryFilters where needed.
        await using var seed = await Factory.CreateDbContextAsync();

        var company = new Company
        {
            CompanyName = "Test Transport",
            OwnerName = "Test Owner",
            OwnerPhone = "9999999999",
            LicenseExpiresOn = DateTime.UtcNow.AddYears(1),
            IsActive = true
        };
        seed.Companies.Add(company);
        await seed.SaveChangesAsync();
        CompanyId = company.Id;

        var state = new State { CompanyId = CompanyId, Name = "Karnataka" };
        var fromCity = new City { CompanyId = CompanyId, Name = "Bengaluru", State = state };
        var toCity = new City { CompanyId = CompanyId, Name = "Hyderabad", State = state };
        var owner = new Owner { CompanyId = CompanyId, Name = "Ramesh Owner", Phone = "9888888888" };
        var vehicle = new Vehicle { CompanyId = CompanyId, RegNo = "KA01AA1111", Ownership = VehicleOwnership.Own };
        var otherVehicle = new Vehicle
        {
            CompanyId = CompanyId,
            RegNo = "KA02BB2222",
            Ownership = VehicleOwnership.Other,
            Owner = owner
        };
        var driver = new Driver
        {
            CompanyId = CompanyId, EmployeeCode = "EMP00001", Name = "Suresh",
            Phone = "9777777777", Salary = 30000
        };
        var party = new Party { CompanyId = CompanyId, Name = "Test Party", Phone = "9666666666" };
        var expenseCategory = new ExpenseCategory { CompanyId = CompanyId, Name = "Fuel" };
        var maintenanceCategory = new MaintenanceCategory { CompanyId = CompanyId, Name = "Service" };
        var user = new User
        {
            Id = UserId, CompanyId = CompanyId, Username = "9999999999",
            DisplayName = "Test Owner", Role = UserRole.Owner, IsActive = true,
            PasswordHash = "x", PasswordSalt = "x"
        };

        seed.AddRange(state, fromCity, toCity, owner, vehicle, otherVehicle,
            driver, party, expenseCategory, maintenanceCategory, user);
        await seed.SaveChangesAsync();

        VehicleId = vehicle.Id;
        OtherOwnerVehicleId = otherVehicle.Id;
        DriverId = driver.Id;
        PartyId = party.Id;
        FromCityId = fromCity.Id;
        ToCityId = toCity.Id;
        ExpenseCategoryId = expenseCategory.Id;
        MaintenanceCategoryId = maintenanceCategory.Id;

        // From here on, act as the signed-in owner of this company.
        CurrentUser.CompanyId = CompanyId;
    }

    /// <summary>Adds a second company with its own vehicle, for the isolation
    /// tests. Returns that company's id.</summary>
    public async Task<(Guid CompanyId, Guid VehicleId)> AddRivalCompanyAsync()
    {
        var previousCompany = CurrentUser.CompanyId;
        CurrentUser.CompanyId = null;

        await using var db = await Factory.CreateDbContextAsync();

        var rival = new Company
        {
            CompanyName = "Rival Transport",
            OwnerName = "Rival Owner",
            OwnerPhone = "9555555555",
            LicenseExpiresOn = DateTime.UtcNow.AddYears(1)
        };
        db.Companies.Add(rival);
        await db.SaveChangesAsync();

        var vehicle = new Vehicle { CompanyId = rival.Id, RegNo = "TN09ZZ9999", Ownership = VehicleOwnership.Own };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();

        CurrentUser.CompanyId = previousCompany;
        return (rival.Id, vehicle.Id);
    }

    /// <summary>Books a trip with sensible defaults; every field a test cares
    /// about is overridable.</summary>
    public async Task<Guid> BookTripAsync(decimal amount = 10000, Guid? vehicleId = null, decimal? commission = null)
    {
        return await Trips.SaveTripAsync(new Trip
        {
            Date = DateTime.Today,
            VehicleId = vehicleId ?? VehicleId,
            DriverId = DriverId,
            PartyId = PartyId,
            FromCityId = FromCityId,
            ToCityId = ToCityId,
            ConsignorName = "Consignor",
            ConsigneeName = "Consignee",
            Amount = amount,
            CommissionAmount = commission
        });
    }

    public async Task AddExpenseAsync(Guid tripId, decimal amount)
        => await Trips.AddExpenseAsync(tripId, new TripExpense
        {
            TripId = tripId,
            CompanyId = CompanyId,
            Date = DateTime.Today,
            ExpenseCategoryId = ExpenseCategoryId,
            Amount = amount
        });

    public async Task<Guid> AddAmountAsync(Guid tripId, decimal amount)
    {
        var transaction = new TripTransaction
        {
            TripId = tripId,
            CompanyId = CompanyId,
            Date = DateTime.Today,
            Amount = amount,
            PaymentMode = PaymentMode.Cash
        };
        await Transactions.AddAsync(tripId, transaction, UserId);
        return transaction.Id;
    }

    public async ValueTask DisposeAsync()
    {
        // Pooled connections keep a handle on the file; without this the
        // delete fails on Windows and temp files pile up.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        await Task.Yield();

        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); }
        catch (IOException) { /* a leftover temp file is not worth failing a test run over */ }
    }

    private sealed class TestContextFactory(
        DbContextOptions<AppDbContext> options,
        ICurrentUserContext currentUser) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options, currentUser);
    }
}

/// <summary>The signed-in user, swappable mid-test so one test can act as two
/// different companies (or as nobody) without rebuilding the world.</summary>
public sealed class MutableUserContext : ICurrentUserContext
{
    public Guid? UserId { get; set; }
    public Guid? CompanyId { get; set; }
}
