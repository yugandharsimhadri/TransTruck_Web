namespace TransTrack.Automation.Workflows;

/// <summary>
/// The definitive list of TransTruck business workflows, in the order they make sense as a product
/// walkthrough: the way in, the morning view, the fleet you set up first, then the trip itself and
/// the money on both sides of it, and finally what the owner reads afterwards. The DemoRunner
/// records this order when no selection is given, so the order here is the running order of the full
/// demo video.
/// </summary>
public static class WorkflowCatalog
{
    public static IReadOnlyList<IWorkflow> All { get; } =
    [
        new SignInWorkflow(),
        new DashboardWorkflow(),
        new MastersWorkflow(),
        new BrowseTripsWorkflow(),
        new TripDetailWorkflow(),
        new RecordExpenseWorkflow(),
        new ApprovalsWorkflow(),
        new MaintenanceWorkflow(),
        new DriverLedgerWorkflow(),
        new ReportsWorkflow(),
        new ActivityWorkflow(),
        new UserAccessWorkflow(),
    ];

    /// <summary>Resolves a workflow by its <see cref="IWorkflow.Key"/>, case-insensitively. Null when unknown.</summary>
    public static IWorkflow? Find(string key)
        => All.FirstOrDefault(w => string.Equals(w.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Resolves a set of keys in catalog order (not the order they were supplied), so a recorded demo
    /// always follows the product's narrative sequence regardless of how the studio listed them.
    /// </summary>
    /// <exception cref="ArgumentException">A key does not match any workflow.</exception>
    public static IReadOnlyList<IWorkflow> Resolve(IEnumerable<string> keys)
    {
        var requested = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);

        var unknown = requested.Where(k => Find(k) is null).ToList();
        if (unknown.Count > 0)
            throw new ArgumentException(
                $"Unknown workflow key(s): {string.Join(", ", unknown)}. " +
                $"Known keys: {string.Join(", ", All.Select(w => w.Key))}.");

        return All.Where(w => requested.Contains(w.Key)).ToList();
    }
}
