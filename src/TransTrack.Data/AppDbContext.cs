using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

public class AppDbContext : DbContext
{
    private readonly ICurrentUserContext _currentUser;

    // The current-user context is optional so every existing caller — every
    // test, the design-time factory — keeps working unchanged; a context
    // built without one simply stamps nothing and, per the global query
    // filters below, sees no tenant rows at all — a safe empty default
    // rather than an accidental cross-tenant view.
    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserContext? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser ?? new NullCurrentUserContext();
    }

    /// <summary>The signed-in user's own company, or Guid.Empty when there
    /// isn't one (no session, or EnterpriseAdmin — who is never scoped to a
    /// company). Guid.Empty never matches a real CompanyId, so every global
    /// query filter below fails safe to "no rows" rather than "all rows"
    /// when there's no authenticated tenant context.</summary>
    public Guid CurrentCompanyId => _currentUser.CompanyId ?? Guid.Empty;

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<State> States => Set<State>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<MaintenanceCategory> MaintenanceCategories => Set<MaintenanceCategory>();
    public DbSet<Counter> Counters => Set<Counter>();
    public DbSet<User> Users => Set<User>();

    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripExpense> TripExpenses => Set<TripExpense>();
    public DbSet<TripTransaction> TripTransactions => Set<TripTransaction>();
    public DbSet<VehicleMaintenance> VehicleMaintenances => Set<VehicleMaintenance>();
    public DbSet<DriverLedgerEntry> DriverLedgerEntries => Set<DriverLedgerEntry>();

    private static readonly MethodInfo SetTenantFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(SetTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Money: 12,2 is plenty for a trucking company and keeps SQLite storage predictable.
        foreach (var property in b.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(12);
            property.SetScale(2);
        }

        b.Entity<Company>(e => e.Ignore(x => x.HasLogo).Ignore(x => x.IsLicenseValid));

        b.Entity<State>(e => e.HasIndex(x => x.Name));

        b.Entity<City>(e =>
        {
            e.HasIndex(x => x.Name);
            e.HasOne(x => x.State).WithMany(s => s.Cities)
                .HasForeignKey(x => x.StateId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.Display);
        });

        b.Entity<Owner>(e => e.HasIndex(x => x.Name));

        b.Entity<Party>(e => e.HasIndex(x => x.Name));

        b.Entity<Driver>(e =>
        {
            // Employee codes are assigned per company (NumberService), not
            // globally — two companies each start at EMP00001.
            e.HasIndex(x => new { x.CompanyId, x.EmployeeCode }).IsUnique();
            e.Ignore(x => x.Display);
        });

        b.Entity<Vehicle>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.RegNo }).IsUnique().HasFilter("\"IsDeleted\" = 0");
            e.HasOne(x => x.Owner).WithMany()
                .HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.Display);
        });

        b.Entity<ExpenseCategory>(e => e.HasIndex(x => x.Name));
        b.Entity<MaintenanceCategory>(e => e.HasIndex(x => x.Name));

        b.Entity<Counter>(e => e.HasIndex(x => new { x.CompanyId, x.Name }).IsUnique());

        b.Entity<User>(e =>
        {
            // Deliberately global, not per-company: login takes just a
            // username and password, no separate "which company" step.
            e.HasIndex(x => x.Username).IsUnique();
            e.Ignore(x => x.Display);
        });

        b.Entity<Trip>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.TripNo }).IsUnique();
            e.HasIndex(x => x.Date);
            e.HasOne(x => x.Vehicle).WithMany()
                .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Driver).WithMany()
                .HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Party).WithMany()
                .HasForeignKey(x => x.PartyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.FromCity).WithMany()
                .HasForeignKey(x => x.FromCityId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ToCity).WithMany()
                .HasForeignKey(x => x.ToCityId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Expenses).WithOne(x => x.Trip)
                .HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Transactions).WithOne(x => x.Trip)
                .HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Cascade);
            e.Ignore(x => x.TotalExpenses);
            e.Ignore(x => x.TotalApprovedReceived);
            e.Ignore(x => x.BalanceReceivable);
            e.Ignore(x => x.NetAfterExpenses);
            e.Ignore(x => x.IsOwnAccounting);
            e.Ignore(x => x.CompanyRevenue);
            e.Ignore(x => x.CompanyExpenses);
        });

        b.Entity<TripExpense>(e =>
        {
            e.HasOne(x => x.ExpenseCategory).WithMany()
                .HasForeignKey(x => x.ExpenseCategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TripTransaction>(e =>
        {
            e.HasIndex(x => x.ApprovalStatus);
        });

        b.Entity<VehicleMaintenance>(e =>
        {
            e.HasOne(x => x.Vehicle).WithMany()
                .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.MaintenanceCategory).WithMany()
                .HasForeignKey(x => x.MaintenanceCategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<DriverLedgerEntry>(e =>
        {
            e.HasOne(x => x.Driver).WithMany()
                .HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
        });

        // One global query filter, applied uniformly to every entity that
        // implements ITenantEntity, instead of a hand-written HasQueryFilter
        // per type — so a newly added tenant-scoped entity is automatically
        // covered just by implementing the interface, with no separate step
        // to remember here.
        foreach (var entityType in b.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
                SetTenantFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [b]);
        }
    }

    private void SetTenantFilter<T>(ModelBuilder b) where T : class, ITenantEntity
    {
        Expression<Func<T, bool>> filter = e => e.CompanyId == CurrentCompanyId;
        b.Entity<T>().HasQueryFilter(filter);
    }

    public override int SaveChanges()
    {
        Stamp();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Stamp();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void Stamp()
    {
        var userId = _currentUser.UserId;
        var companyId = _currentUser.CompanyId;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.Now;
                entry.Entity.CreatedByUserId = userId;

                // Safety net alongside services setting CompanyId explicitly:
                // any new row of a tenant entity that hasn't already been
                // assigned a company (e.g. EnterpriseAdmin's onboarding code,
                // which sets it explicitly for the company being created)
                // gets stamped with the caller's own company.
                if (entry.Entity is ITenantEntity { CompanyId: var existing } tenant
                    && existing == Guid.Empty && companyId is { } cid)
                {
                    tenant.CompanyId = cid;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.Now;
                entry.Entity.UpdatedByUserId = userId;
            }
        }
    }
}
