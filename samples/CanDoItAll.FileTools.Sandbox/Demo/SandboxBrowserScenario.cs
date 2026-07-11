namespace CanDoItAll.FileTools.Sandbox.Demo;

public enum SandboxBrowserScenario
{
    Healthy,
    Empty,
    PartialWarning,
    RetryableError,
    LiveFileSystem
}

public static class SandboxScenarioCatalog
{
    public static string GetTestId(SandboxBrowserScenario scenario)
        => scenario switch
        {
            SandboxBrowserScenario.Healthy => "healthy",
            SandboxBrowserScenario.Empty => "empty",
            SandboxBrowserScenario.PartialWarning => "partial-warning",
            SandboxBrowserScenario.RetryableError => "retryable-error",
            SandboxBrowserScenario.LiveFileSystem => "live-filesystem",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

    public static string GetLabel(SandboxBrowserScenario scenario)
        => scenario switch
        {
            SandboxBrowserScenario.Healthy => "Healthy",
            SandboxBrowserScenario.Empty => "Empty",
            SandboxBrowserScenario.PartialWarning => "Partial warning",
            SandboxBrowserScenario.RetryableError => "Retryable error",
            SandboxBrowserScenario.LiveFileSystem => "Live filesystem",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

    public static string GetShortDescription(SandboxBrowserScenario scenario)
        => scenario switch
        {
            SandboxBrowserScenario.Healthy => "3 sources · paging",
            SandboxBrowserScenario.Empty => "0 child items",
            SandboxBrowserScenario.PartialWarning => "usable partial page",
            SandboxBrowserScenario.RetryableError => "fails once · retry",
            SandboxBrowserScenario.LiveFileSystem => "fresh local reads",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

    public static string GetEyebrow(SandboxBrowserScenario scenario)
        => scenario switch
        {
            SandboxBrowserScenario.Healthy => "Nominal / multi-source",
            SandboxBrowserScenario.Empty => "Boundary / no content",
            SandboxBrowserScenario.PartialWarning => "Degraded / usable",
            SandboxBrowserScenario.RetryableError => "Failure / recoverable",
            SandboxBrowserScenario.LiveFileSystem => "Adapter / always current",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

    public static string GetTitle(SandboxBrowserScenario scenario)
        => scenario switch
        {
            SandboxBrowserScenario.Healthy => "Project and shared files",
            SandboxBrowserScenario.Empty => "Empty project folder",
            SandboxBrowserScenario.PartialWarning => "Partial provider response",
            SandboxBrowserScenario.RetryableError => "Source needs a retry",
            SandboxBrowserScenario.LiveFileSystem => "Sandbox filesystem root",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

    public static string GetDescription(SandboxBrowserScenario scenario)
        => scenario switch
        {
            SandboxBrowserScenario.Healthy => "Switch sources, open folders, search, select items, and load additional pages.",
            SandboxBrowserScenario.Empty => "Verifies the explicit empty state without removing navigation context.",
            SandboxBrowserScenario.PartialWarning => "Files remain available while the provider reports one skipped entry.",
            SandboxBrowserScenario.RetryableError => "The first browse fails safely; Retry replays the request and recovers.",
            SandboxBrowserScenario.LiveFileSystem => "A root-confined temporary folder is enumerated fresh whenever the browser refreshes.",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

    public static string GetStatus(SandboxBrowserScenario scenario)
        => scenario switch
        {
            SandboxBrowserScenario.Healthy => "Ready · page size 8",
            SandboxBrowserScenario.Empty => "Ready · complete empty page",
            SandboxBrowserScenario.PartialWarning => "Attention · partial completeness",
            SandboxBrowserScenario.RetryableError => "Recovery path enabled",
            SandboxBrowserScenario.LiveFileSystem => "No provider cache",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
}
