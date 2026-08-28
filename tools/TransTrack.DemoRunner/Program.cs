using System.Diagnostics;
using System.Text.Json;
using TransTrack.Automation;
using TransTrack.Automation.Workflows;
using TransTrack.DemoRunner;

// Recording front end for the UAT workflows. Every segment it produces is the same IWorkflow the
// acceptance suite runs, so the footage can only ever show a journey that passes its own checks.
//
// Screen capture itself is left to OBS (driven by the Content Automation Studio): this process is
// responsible only for putting the right thing on screen, at a followable pace, with captions, and
// for writing the manifest the narration step reads afterwards.

DemoOptions options;
try
{
    options = DemoOptions.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

if (options.ShowHelp)
{
    Console.WriteLine(DemoOptions.Usage);
    return 0;
}

// Answered before anything is started: the studio calls --list to build its catalog, and it must not
// pay for a dev server and a browser to read a static list.
if (options.ListOnly)
{
    foreach (var w in WorkflowCatalog.All)
        Console.WriteLine($"{w.Key,-22} {w.DisplayName,-36} {w.Module}");
    return 0;
}

IReadOnlyList<IWorkflow> workflows;
try
{
    workflows = options.WorkflowKeys.Count == 0
        ? WorkflowCatalog.All
        : WorkflowCatalog.Resolve(options.WorkflowKeys);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

var automationOptions = AutomationOptions.FromEnvironment(options.RunMode, options.Viewport) with
{
    RunMode = options.RunMode,
    Viewport = options.Viewport,
};

Console.WriteLine($"TransTruck Demo Runner — {workflows.Count} workflow(s), viewport={options.Viewport}, mode={options.RunMode}");
Console.WriteLine();

await using var api = await ApiServer.StartAsync(automationOptions, Console.WriteLine);
await new DemoDataSeeder(automationOptions.ApiBaseUrl, Console.WriteLine).SeedAsync();
await using var web = await WebDevServer.StartAsync(automationOptions, Console.WriteLine);

var stopwatch = Stopwatch.StartNew();

await using var session = await TransTruckSession.StartAsync(automationOptions, Console.WriteLine);
await session.LoginAsync();

var results = new List<WorkflowRunResult>();
var offsets = new List<TimeSpan>();

foreach (var workflow in workflows)
{
    Console.WriteLine();

    // Captured before the segment runs: the manifest's offset is where this segment starts in the
    // continuous recording, which is what lets the narration step cut audio against video without
    // re-deriving anything from the file itself.
    offsets.Add(stopwatch.Elapsed);

    var result = await WorkflowRunner.RunAsync(session, workflow, Console.WriteLine);
    results.Add(result);

    // A failed segment is reported and skipped rather than aborting: a twelve-segment recording
    // session is expensive to restart, and the manifest marks which segments are unusable.
    if (!result.Succeeded && !ReferenceEquals(workflow, workflows[^1]))
        Console.WriteLine("Continuing with the next workflow.");
}

stopwatch.Stop();

var manifestPath = options.ManifestPath
    ?? Path.Combine(RepoPaths.ArtifactsDir, $"demo-manifest-{options.Viewport.ToString().ToLowerInvariant()}.json");

WriteManifest(manifestPath, workflows, results, offsets, options, session.Capture, stopwatch.Elapsed);

var passed = results.Count(r => r.Succeeded);
var failed = results.Count - passed;

Console.WriteLine();
Console.WriteLine($"Done in {stopwatch.Elapsed.TotalSeconds:0.0}s — {passed} succeeded, {failed} failed.");
Console.WriteLine($"Capture: {session.Capture}");
Console.WriteLine($"Manifest: {manifestPath}");

foreach (var failure in results.Where(r => !r.Succeeded))
    Console.WriteLine($"  FAILED {failure.Key}: {failure.FailureMessage}");

return failed == 0 ? 0 : 1;

/// <summary>
/// Records what was captured, segment by segment: the business metadata the narration is written
/// from, the narration beats actually spoken, where each segment starts in the recording and how
/// long it ran. This is the hand-off to the Content Automation Studio's narration and caption steps —
/// it should not have to re-derive any of this from the video.
/// </summary>
static void WriteManifest(
    string path,
    IReadOnlyList<IWorkflow> workflows,
    IReadOnlyList<WorkflowRunResult> results,
    IReadOnlyList<TimeSpan> offsets,
    DemoOptions options,
    CaptureSize capture,
    TimeSpan totalDuration)
{
    var manifest = new
    {
        product = "TransTruck",
        productDisplayName = "LorryOwner — fleet and trip management",
        recordedAtUtc = DateTime.UtcNow,
        runMode = options.RunMode.ToString(),
        viewport = options.Viewport.ToString(),
        capture = new
        {
            pageWidth = capture.PageWidth,
            pageHeight = capture.PageHeight,
            deviceScaleFactor = capture.DeviceScaleFactor,
            windowWidth = capture.WindowWidth,
            windowHeight = capture.WindowHeight,
        },
        totalDurationSeconds = Math.Round(totalDuration.TotalSeconds, 1),
        segments = workflows.Select((workflow, i) => new
        {
            key = workflow.Key,
            displayName = workflow.DisplayName,
            module = workflow.Module,
            targetAudience = workflow.TargetAudience,
            businessPurpose = workflow.BusinessPurpose,
            viewport = options.Viewport.ToString(),
            startOffsetSeconds = Math.Round(offsets[i].TotalSeconds, 1),
            durationSeconds = Math.Round(results[i].Duration.TotalSeconds, 1),
            succeeded = results[i].Succeeded,
            narrationSteps = results[i].NarrationSteps,
            failureMessage = results[i].FailureMessage,
            screenshot = results[i].ScreenshotPath,
        }),
    };

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    File.WriteAllText(path, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
}
