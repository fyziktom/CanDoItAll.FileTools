namespace CanDoItAll.FileTools.FileInteraction.Components;

/// <summary>Counts the replaced UTF-16 span after excluding a shared prefix and suffix.</summary>
internal static class FileInteractionTextChangeCounter
{
    public static int CountChangedTextUnits(string previous, string current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        var commonPrefix = 0;
        var maximumPrefix = Math.Min(previous.Length, current.Length);
        while (commonPrefix < maximumPrefix
            && previous[commonPrefix] == current[commonPrefix])
        {
            commonPrefix++;
        }

        var previousRemaining = previous.Length - commonPrefix;
        var currentRemaining = current.Length - commonPrefix;
        var commonSuffix = 0;
        var maximumSuffix = Math.Min(previousRemaining, currentRemaining);
        while (commonSuffix < maximumSuffix
            && previous[previous.Length - commonSuffix - 1]
                == current[current.Length - commonSuffix - 1])
        {
            commonSuffix++;
        }

        var replacedPrevious = previousRemaining - commonSuffix;
        var replacedCurrent = currentRemaining - commonSuffix;
        return Math.Max(replacedPrevious, replacedCurrent);
    }
}
