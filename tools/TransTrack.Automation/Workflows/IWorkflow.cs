namespace TransTrack.Automation.Workflows;

/// <summary>
/// One end-to-end business journey through TransTruck, written once and consumed twice: the UAT
/// suite runs it headless and asserts it completes, and the DemoRunner runs it headed and records
/// it. The verification steps live inside the workflow rather than in the tests, so the footage is
/// only ever produced from a journey that actually passed its own checks.
/// </summary>
public interface IWorkflow
{
    /// <summary>
    /// Stable token used on the DemoRunner command line (<c>--workflow BookTrip</c>) and as the
    /// segment key the Content Automation Studio pairs narration and video against. Changing one
    /// breaks that pairing, so treat these as an external contract.
    /// </summary>
    string Key { get; }

    /// <summary>Human-readable name, used for the on-screen title card and the studio's catalog.</summary>
    string DisplayName { get; }

    /// <summary>Which part of the product this belongs to, shown under the title card.</summary>
    string Module { get; }

    /// <summary>Who the segment is aimed at — drives the tone of the generated narration.</summary>
    string TargetAudience { get; }

    /// <summary>One sentence on what the workflow accomplishes for the transport business.</summary>
    string BusinessPurpose { get; }

    /// <summary>
    /// Runs the journey from the dashboard, leaving the app on the workflow's closing screen. The
    /// caller has already signed in; a workflow must not assume any other prior state, so that any
    /// subset of workflows can be selected and run in any order — and in either viewport.
    /// </summary>
    Task RunAsync(WorkflowContext context);
}
