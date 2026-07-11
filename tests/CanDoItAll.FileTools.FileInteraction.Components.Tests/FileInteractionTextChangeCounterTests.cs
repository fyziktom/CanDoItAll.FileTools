using CanDoItAll.FileTools.FileInteraction.Components;

namespace CanDoItAll.FileTools.FileInteraction.Components.Tests;

public sealed class FileInteractionTextChangeCounterTests
{
    [Theory]
    [InlineData("same", "same", 0)]
    [InlineData("ac", "abc", 1)]
    [InlineData("abc", "ac", 1)]
    [InlineData("abc", "axc", 1)]
    [InlineData("abc", "axyc", 2)]
    [InlineData("", "\ud83d\ude00", 2)]
    public void CountChangedTextUnits_ExcludesSharedPrefixAndSuffix(
        string previous,
        string current,
        int expected)
        => Assert.Equal(
            expected,
            FileInteractionTextChangeCounter.CountChangedTextUnits(previous, current));
}
