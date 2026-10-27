namespace CanDoItAll.FileTools.Sandbox.Components;

/// <summary>
/// Describes the optional sibling <c>CanDoItAll.Components</c> checkout wired into the sandbox as direct project
/// references. When the sibling repository is absent the sandbox still builds and falls back to inert seams.
/// </summary>
public static class SandboxComponentsSupport
{
#if CANDOITALL_COMPONENTS
    public const bool IsAvailable = true;

    public static Type? HeadAssetsComponentType => typeof(SandboxComponentsHeadAssets);

    public static Type? MermaidDiagramComponentType => typeof(SandboxMermaidDiagram);
#else
    public const bool IsAvailable = false;

    public static Type? HeadAssetsComponentType => null;

    public static Type? MermaidDiagramComponentType => null;
#endif
}
