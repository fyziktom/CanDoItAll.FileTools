namespace CanDoItAll.FileTools.FileInteraction.Core.Tests;

public sealed class ProfileCatalogTests
{
    [Fact]
    public void Resolve_CompetingMatchKinds_UsesDocumentedSpecificityBeforePriority()
    {
        var fallback = Profile("fallback", 1000, extensions: ["*"]);
        var extension = Profile("extension", 500, extensions: [".md"]);
        var wildcard = Profile("wildcard", 100, mediaTypes: ["text/*"]);
        var exact = Profile("exact", -100, mediaTypes: ["text/markdown"]);
        var catalog = new FileInteractionProfileCatalog([fallback, extension, wildcard, exact]);

        var result = catalog.Resolve(Request("notes.md", "text/markdown"));

        Assert.Equal(FileInteractionResolutionStatus.Resolved, result.Status);
        Assert.Same(exact, result.Profile);
        Assert.Equal(FileInteractionMatchKind.MediaTypeExact, Assert.Single(result.Candidates).MatchKind);
    }

    [Fact]
    public void Resolve_ParameterizedMediaType_UsesCanonicalExactMatch()
    {
        var extension = Profile("extension", 100, extensions: [".md"]);
        var exact = Profile("exact", -100, mediaTypes: ["text/markdown"]);

        var result = new FileInteractionProfileCatalog([extension, exact]).Resolve(
            Request("notes.md", "Text/Markdown; charset=UTF-8"));

        Assert.Same(exact, result.Profile);
        Assert.Equal(FileInteractionMatchKind.MediaTypeExact, Assert.Single(result.Candidates).MatchKind);
    }

    [Fact]
    public void Resolve_SameSpecificity_UsesPriority()
    {
        var low = Profile("low", 1, extensions: [".txt"]);
        var high = Profile("high", 2, extensions: [".txt"]);

        var result = new FileInteractionProfileCatalog([low, high]).Resolve(Request("a.txt"));

        Assert.Same(high, result.Profile);
    }

    [Fact]
    public void Resolve_EqualFinalScore_ReturnsExplicitAmbiguity()
    {
        var first = Profile("a", 4, mediaTypes: ["text/plain"]);
        var second = Profile("b", 4, mediaTypes: ["text/plain"]);

        var result = new FileInteractionProfileCatalog([second, first]).Resolve(Request("a.txt", "text/plain"));

        Assert.Equal(FileInteractionResolutionStatus.Ambiguous, result.Status);
        Assert.Null(result.Profile);
        Assert.Equal(["a", "b"], result.Candidates.Select(match => match.Profile.Id));
    }

    [Fact]
    public void Resolve_UnsupportedDiff_DoesNotFallBackToViewProfile()
    {
        var viewOnly = new FileInteractionProfileDescriptor(
            "view",
            FileInteractionCapabilities.View,
            extensions: ["*"]);

        var result = new FileInteractionProfileCatalog([viewOnly]).Resolve(
            Request("a.txt", mode: FileInteractionMode.Diff));

        Assert.Equal(FileInteractionResolutionStatus.Unsupported, result.Status);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void Resolve_MissingRequiredCapability_ReturnsUnsupported()
    {
        var profile = Profile("plain", extensions: [".txt"]);

        var result = new FileInteractionProfileCatalog([profile]).Resolve(
            Request("a.txt"),
            FileInteractionCapabilities.Preview);

        Assert.Equal(FileInteractionResolutionStatus.Unsupported, result.Status);
    }

    [Fact]
    public void Constructor_DuplicateProfileId_Throws()
    {
        var first = Profile("same", extensions: [".txt"]);
        var second = Profile("same", extensions: [".md"]);

        Assert.Throws<ArgumentException>(() => new FileInteractionProfileCatalog([first, second]));
    }

    [Fact]
    public void Constructor_MutableInput_IsDefensivelyCopied()
    {
        var source = new List<FileInteractionProfileDescriptor> { Profile("first", extensions: ["*"]) };
        var catalog = new FileInteractionProfileCatalog(source);

        source.Clear();

        Assert.Single(catalog.Profiles);
    }

    private static FileInteractionProfileDescriptor Profile(
        string id,
        int priority = 0,
        IEnumerable<string>? extensions = null,
        IEnumerable<string>? mediaTypes = null)
        => new(
            id,
            FileInteractionCapabilities.View | FileInteractionCapabilities.Edit | FileInteractionCapabilities.Save,
            extensions,
            mediaTypes,
            priority);

    private static FileInteractionRequest Request(
        string fileName,
        string? mediaType = null,
        FileInteractionMode mode = FileInteractionMode.View)
        => new(new FileReference("test", fileName), fileName, mode, mediaType);
}
