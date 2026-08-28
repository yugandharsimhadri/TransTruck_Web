namespace TransTrack.Automation;

/// <summary>
/// Locates the repository layout at runtime. The UAT and DemoRunner binaries both sit several levels
/// deep under bin/, and the Content Automation Studio launches them from its own working directory,
/// so nothing here can rely on Environment.CurrentDirectory — the repo root is found by walking up
/// until the solution file appears.
/// </summary>
public static class RepoPaths
{
    private const string SolutionFileName = "TransTruck.Web.slnx";

    private static readonly Lazy<string> RootLazy = new(FindRoot);

    /// <summary>Absolute path to the repository root (the folder holding TransTruck.Web.slnx).</summary>
    public static string Root => RootLazy.Value;

    /// <summary>Absolute path to web/transtrack-web, the Next.js client.</summary>
    public static string WebProject => Path.Combine(Root, "web", "transtrack-web");

    /// <summary>Absolute path to the API project, started fresh against a throwaway database.</summary>
    public static string ApiProject => Path.Combine(Root, "src", "TransTrack.Api", "TransTrack.Api.csproj");

    /// <summary>Where screenshots, manifests and the throwaway database are written.</summary>
    public static string ArtifactsDir => Path.Combine(Root, "artifacts", "uat");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate '{SolutionFileName}' walking up from '{AppContext.BaseDirectory}'. " +
            "Set TRANSTRUCK_UAT_WEB_PATH to the absolute path of web/transtrack-web to bypass repo discovery.");
    }
}
