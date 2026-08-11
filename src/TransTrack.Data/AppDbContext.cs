using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

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

        b.Entity<Counter>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.Name }).IsUnique();

            // The counter's own value is its concurrency token: EF puts the
            // value it read into the UPDATE's WHERE clause, so if another
            // request incremented the counter in between, zero rows match and
            // the save throws instead of silently issuing a duplicate
            // document number. NumberService.AllocateAsync catches that and
            // retries. Without this, two concurrent bookings both produce the
            // same TripNo and the unique index rejects one with a raw error.
            e.Property(x => x.LastNumber).IsConcurrencyToken();
        });

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

        b.Entity<AuditLog>(e =>
        {
            // The three ways the trail gets read: newest-first for the
            // activity feed, by record for one row's history, and by trip for
            // a trip's whole story.
            e.HasIndex(x => x.ChangedOn);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
            e.HasIndex(x => x.TripId);
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
        AddAuditEntries();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Stamp();
        AddAuditEntries();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Columns that say nothing a reader of the audit trail cares
    /// about — the row's identity, its tenant, and the stamps the audit entry
    /// already records more directly.</summary>
    private static readonly HashSet<string> AuditIgnoredProperties =
    [
        nameof(BaseEntity.Id),
        nameof(BaseEntity.CreatedAt),
        nameof(BaseEntity.UpdatedAt),
        nameof(BaseEntity.CreatedByUserId),
        nameof(BaseEntity.UpdatedByUserId),
        nameof(ITenantEntity.CompanyId)
    ];

    /// <summary>
    /// Writes the audit trail by reading the ChangeTracker just before the
    /// save. Doing it here rather than in each service means every write path
    /// is covered — including any added later — with no per-service call to
    /// remember, and it lands in the same transaction as the change itself,
    /// so a record and its audit entry can never disagree.
    /// </summary>
    private void AddAuditEntries()
    {
        // Snapshot first: adding audit rows below mutates the tracker, and
        // enumerating it while it changes would throw.
        var tracked = ChangeTracker.Entries()
            .Where(e => e.Entity is IAuditable
                        && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (tracked.Count == 0) return;

        var userId = _currentUser.UserId;
        var now = DateTime.Now;
        var logs = new List<AuditLog>(tracked.Count);

        foreach (var entry in tracked)
        {
            var entityType = entry.Entity.GetType().Name;

            // A soft delete arrives as a Modified entry with IsDeleted going
            // false -> true. Report it as the deletion it actually is, so the
            // trail reads the way the user experienced it.
            var softDeleted = entry.State == EntityState.Modified
                              && entry.Property(nameof(BaseEntity.IsDeleted)) is { } flag
                              && flag.IsModified
                              && Equals(flag.CurrentValue, true);

            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Created,
                EntityState.Deleted => AuditAction.Deleted,
                _ => softDeleted ? AuditAction.Deleted : AuditAction.Updated
            };

            var changes = action == AuditAction.Updated ? DescribeChanges(entry) : null;

            // An edit that touched nothing a reader cares about (only the
            // stamps, say) isn't worth a row.
            if (action == AuditAction.Updated && changes is null) continue;

            logs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                CompanyId = TenantIdOf(entry) ?? CurrentCompanyId,
                EntityType = entityType,
                EntityId = entry.Property(nameof(BaseEntity.Id)).CurrentValue is Guid id ? id : Guid.Empty,
                TripId = TripIdOf(entry),
                Action = action,
                ChangedByUserId = userId,
                ChangedOn = now,
                Summary = Describe(entityType, action, entry),
                Changes = changes,
                CreatedAt = now,
                CreatedByUserId = userId
            });
        }

        if (logs.Count > 0) AuditLogs.AddRange(logs);
    }

    private static Guid? TenantIdOf(EntityEntry entry) =>
        entry.Entity is ITenantEntity { CompanyId: var c } && c != Guid.Empty ? c : null;

    /// <summary>The owning trip for the entities that hang off one, so a
    /// trip's history is a single indexed lookup.</summary>
    private static Guid? TripIdOf(EntityEntry entry) => entry.Entity switch
    {
        Trip t => t.Id,
        TripExpense e => e.TripId,
        TripTransaction x => x.TripId,
        _ => null
    };

    /// <summary>Field-level detail for an edit, or null when nothing
    /// meaningful moved.</summary>
    private static string? DescribeChanges(EntityEntry entry)
    {
        var moved = entry.Properties
            .Where(p => p.IsModified
                        && !AuditIgnoredProperties.Contains(p.Metadata.Name)
                        // Raw user ids read as noise — the entry already names
                        // who made the change, in words.
                        && !p.Metadata.Name.EndsWith("ByUserId", StringComparison.Ordinal)
                        && !Equals(p.OriginalValue, p.CurrentValue))
            .Select(p => new
            {
                field = p.Metadata.Name,
                from = Format(p.OriginalValue),
                to = Format(p.CurrentValue)
            })
            .ToList();

        return moved.Count == 0 ? null : JsonSerializer.Serialize(moved);
    }

    private static string? Format(object? value) => value switch
    {
        null => null,
        DateTime d => d.ToString("yyyy-MM-dd"),
        decimal m => m.ToString("0.##"),
        bool b => b ? "yes" : "no",
        _ => value.ToString()
    };

    /// <summary>A plain-English line for the activity feed. Written at capture
    /// time so it reflects what the app did, and can't drift if the code is
    /// later reorganised.</summary>
    private static string Describe(string entityType, AuditAction action, EntityEntry entry)
    {
        // The handful of state changes that carry real meaning get named
        // outright — "Trip closed" tells the story, "Trip of 5,000.00 changed"
        // does not, and these are precisely the events an audit trail exists
        // to record.
        if (action == AuditAction.Updated && Named(entry) is { } named) return named;

        var noun = entityType switch
        {
            nameof(Trip) => "Trip",
            nameof(TripExpense) => "Expense",
            nameof(TripTransaction) => "Amount received",
            nameof(VehicleMaintenance) => "Maintenance record",
            nameof(DriverLedgerEntry) => "Driver ledger entry",
            _ => entityType
        };

        var verb = action switch
        {
            AuditAction.Created => "added",
            AuditAction.Deleted => "deleted",
            _ => "changed"
        };

        // Money is the thing anyone scanning the feed is looking for, so put
        // it in the line itself rather than making them open the detail.
        var amount = entry.Entity switch
        {
            TripExpense e => e.Amount,
            TripTransaction x => x.Amount,
            VehicleMaintenance m => m.Amount,
            DriverLedgerEntry d => d.Amount,
            Trip t => t.Amount,
            _ => (decimal?)null
        };

        return amount is { } value
            ? $"{noun} of {value:N2} {verb}"
            : $"{noun} {verb}";
    }

    /// <summary>A purpose-written line for the state transitions worth calling
    /// out by name, or null to fall back to the generic description.</summary>
    private static string? Named(EntityEntry entry)
    {
        bool Changed(string property) =>
            entry.Properties.Any(p => p.Metadata.Name == property && p.IsModified
                                      && !Equals(p.OriginalValue, p.CurrentValue));

        switch (entry.Entity)
        {
            case Trip trip when Changed(nameof(Trip.Status)):
                return trip.Status == TripStatus.Closed ? "Trip closed" : "Trip reopened";

            case Trip when Changed(nameof(Trip.LrNo)):
                return "LR number assigned";

            case Trip when Changed(nameof(Trip.BillNo)):
                return "Bill number assigned";

            case TripTransaction t when Changed(nameof(TripTransaction.ApprovalStatus)):
                return t.ApprovalStatus switch
                {
                    ApprovalStatus.Approved => $"Amount of {t.Amount:N2} approved",
                    ApprovalStatus.Rejected => $"Amount of {t.Amount:N2} rejected",
                    _ => null
                };

            default:
                return null;
        }
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
