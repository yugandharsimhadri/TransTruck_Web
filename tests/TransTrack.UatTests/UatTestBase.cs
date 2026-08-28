using System.Text;
using TransTrack.Automation;
using TransTrack.Automation.Workflows;
using Xunit.Abstractions;

namespace TransTrack.UatTests;

/// <summary>
/// Base for the acceptance classes. Each test gets its own browser session, runs one workflow from
/// <see cref="WorkflowCatalog"/> in one viewport, and passes only if every verification inside that
/// workflow held.
///
/// The scenarios deliberately live in TransTrack.Automation rather than here: the DemoRunner replays
/// exactly these objects to produce the demo and user-guide videos, so what the videos show and what
/// the UAT signs off are the same journey by construction, not by anyone remembering to keep two
/// scripts in step.
/// </summary>
[Collection(UatCollection.Name)]
public abstract class UatTestBase(UatFixture fixture, ITestOutputHelper output)
{
    /// <summary>
    /// Every workflow is asserted in both viewports, and this is the data behind that
    /// <c>[Theory]</c>. A theory rather than two test classes on purpose: the Content Automation
    /// Studio derives its module list by grouping test names by declaring class and stripping the
    /// <c>UatTests</c> suffix, so a class per viewport would invent modules called "Trip Lifecycle
    /// Desktop" and "Trip Lifecycle Mobile" that no one would recognise. One class per module, two
    /// cases per test, and the viewport shows up in the case name where it belongs.
    /// </summary>
    public static TheoryData<Viewport> BothViewports => new() { Viewport.Desktop, Viewport.Mobile };

    /// <summary>
    /// Runs the named workflow end to end in one viewport and asserts it completed. On failure the
    /// narration steps that did run are attached to the assertion message, so the report names the
    /// business step that broke rather than just a locator.
    /// </summary>
    protected async Task RunWorkflowAsync(string workflowKey, Viewport viewport)
    {
        var workflow = WorkflowCatalog.Find(workflowKey)
            ?? throw new InvalidOperationException($"No workflow named '{workflowKey}' in the catalog.");

        var options = fixture.Options with { Viewport = viewport };

        await using var session = await TransTruckSession.StartAsync(options, output.WriteLine);
        await session.LoginAsync();

        var result = await WorkflowRunner.RunAsync(session, workflow, output.WriteLine);

        Assert.True(result.Succeeded, BuildFailureMessage(workflow, result));
    }

    private static string BuildFailureMessage(IWorkflow workflow, WorkflowRunResult result)
    {
        var message = new StringBuilder()
            .AppendLine($"UAT scenario '{workflow.DisplayName}' ({workflow.Key}) failed on {result.Viewport}.")
            .AppendLine($"Purpose: {workflow.BusinessPurpose}")
            .AppendLine()
            .AppendLine($"Failed after {result.NarrationSteps.Count} step(s):");

        foreach (var (step, index) in result.NarrationSteps.Select((s, i) => (s, i + 1)))
            message.AppendLine($"  {index,2}. {step}");

        message.AppendLine().AppendLine($"Reason: {result.FailureMessage}");

        if (result.ScreenshotPath is not null)
            message.AppendLine($"Screenshot: {result.ScreenshotPath}");

        return message.ToString();
    }
}
