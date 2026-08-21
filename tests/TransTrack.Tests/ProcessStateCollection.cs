namespace TransTrack.Tests;

/// <summary>
/// Test classes that mutate process-wide state — environment variables and
/// AppConfig's cached settings — share this collection so xUnit runs them one
/// at a time rather than in parallel.
///
/// Without it they pass alone and fail together: DocumentTests points
/// TRANSTRUCKWEB_VEHICLEDOCS at a temp folder, AppConfigTests asserts on the
/// path that same variable feeds, and whichever ran second saw the other's
/// value. That is a flaky suite, not a flaky product.
/// </summary>
[CollectionDefinition(Name)]
public class ProcessStateCollection
{
    public const string Name = "process-state";
}
