namespace CanDoItAll.FileTools.FileInteraction.Core.Tests;

public sealed class FileInteractionCoreBuilderTests
{
    [Fact]
    public async Task Build_ProducesIndependentProfileAndHistoryCatalogs()
    {
        var profile = new FileInteractionProfileDescriptor(
            "text",
            FileInteractionCapabilities.View
                | FileInteractionCapabilities.Edit
                | FileInteractionCapabilities.Save
                | FileInteractionCapabilities.Undo
                | FileInteractionCapabilities.Redo,
            extensions: [".txt"],
            history: new FileHistoryOptions(10, 1_000));
        var builder = new FileInteractionCoreBuilder()
            .AddProfile(profile)
            .AddHistoryFactory(new BoundedTextHistoryProviderFactory());

        var composition = builder.Build();
        builder.AddProfile(new FileInteractionProfileDescriptor(
            "other",
            FileInteractionCapabilities.View,
            extensions: [".other"]));

        Assert.Same(profile, composition.Profiles.Resolve(
            new FileInteractionRequest(FileEditSessionTests.File(), "file.txt", FileInteractionMode.Edit)).Profile);
        Assert.Single(composition.Profiles.Profiles);
        await using var history = await composition.HistoryProviders.CreateAsync(
            profile,
            new FileInteractionRequest(FileEditSessionTests.File(), "file.txt", FileInteractionMode.Edit));
        Assert.IsType<BoundedTextHistoryProvider>(history);
    }

    [Fact]
    public void AddProfile_Null_ThrowsBeforeBuild()
    {
        var builder = new FileInteractionCoreBuilder();

        Assert.Throws<ArgumentNullException>(() =>
        {
            builder.AddProfile(null!);
        });
    }
}
