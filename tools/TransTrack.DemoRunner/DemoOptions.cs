using TransTrack.Automation;

namespace TransTrack.DemoRunner;

/// <summary>The parsed command line.</summary>
public sealed record DemoOptions(
    IReadOnlyList<string> WorkflowKeys,
    RunMode RunMode,
    Viewport Viewport,
    string? ManifestPath,
    bool ListOnly,
    bool ShowHelp)
{
    /// <summary>
    /// Parses the arguments the Content Automation Studio sends (<c>--workflow A B C</c>,
    /// <c>--viewport mobile</c>) plus the few switches a person recording by hand needs. Unknown
    /// arguments are an error rather than a silent skip — a mistyped workflow name should stop the
    /// run, not quietly record the wrong thing.
    /// </summary>
    public static DemoOptions Parse(string[] args)
    {
        var workflows = new List<string>();
        var runMode = RunMode.Demo;
        var viewport = Viewport.Desktop;
        string? manifestPath = null;
        var listOnly = false;
        var showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "--workflow" or "-w":
                    // Consumes every following value until the next switch, so the studio's
                    // "--workflow A B C" form works as well as a single name.
                    while (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                        workflows.Add(args[++i]);
                    break;

                case "--mode" or "-m":
                    runMode = ParseEnum<RunMode>(NextValue(args, ref i, arg));
                    break;

                case "--viewport" or "-v":
                    viewport = ParseEnum<Viewport>(NextValue(args, ref i, arg));
                    break;

                case "--manifest":
                    manifestPath = NextValue(args, ref i, arg);
                    break;

                case "--list" or "-l":
                    listOnly = true;
                    break;

                case "--help" or "-h" or "-?":
                    showHelp = true;
                    break;

                default:
                    throw new ArgumentException($"Unrecognised argument '{arg}'. Run with --help for usage.");
            }
        }

        return new DemoOptions(workflows, runMode, viewport, manifestPath, listOnly, showHelp);
    }

    private static string NextValue(string[] args, ref int index, string argumentName)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"'{argumentName}' needs a value.");

        return args[++index];
    }

    private static TEnum ParseEnum<TEnum>(string value) where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed))
            return parsed;

        throw new ArgumentException(
            $"'{value}' is not a valid {typeof(TEnum).Name}. Valid values: {string.Join(", ", Enum.GetNames<TEnum>()).ToLowerInvariant()}.");
    }

    public const string Usage = """
        TransTruck Demo Runner - replays the UAT workflows on camera.

        Screen capture is OBS's job. This process only puts the right thing on screen, at a
        followable pace, with captions, and writes the manifest the narration step reads afterwards.

        Usage:
          dotnet run --project tools/TransTrack.DemoRunner -- [options]

        Options:
          -w, --workflow <key...>  Workflows to run, by key. Omit to run the whole catalog in order.
          -v, --viewport <name>    desktop (default) | mobile
                                   One viewport per run, so desktop and mobile videos come out as
                                   separate files. mobile emulates a real phone - touch, user agent
                                   and device pixel ratio - not just a narrow window.
          -m, --mode <mode>        demo (default) | userguide | test
                                   demo      - flowing pace, one caption per beat. Product demos.
                                   userguide - slower, numbered step captions. How-to videos.
                                   test      - headless, no pacing. Same journey, no footage.
              --manifest <path>    Where to write the run manifest
                                   (default: artifacts/uat/demo-manifest-<viewport>.json).
          -l, --list               List the workflow catalog and exit.
          -h, --help               Show this help.

        Exit codes:
          0  every requested workflow succeeded
          1  at least one workflow failed
          2  the command line could not be parsed

        Examples:
          dotnet run --project tools/TransTrack.DemoRunner -- --list
          dotnet run --project tools/TransTrack.DemoRunner -- --workflow Dashboard BrowseTrips
          dotnet run --project tools/TransTrack.DemoRunner -- --viewport mobile --workflow Dashboard
          dotnet run --project tools/TransTrack.DemoRunner -- --mode userguide --viewport mobile
        """;
}
