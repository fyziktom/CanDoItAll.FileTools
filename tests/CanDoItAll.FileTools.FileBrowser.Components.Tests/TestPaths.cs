namespace CanDoItAll.FileTools.FileBrowser.Components.Tests;

internal static class TestPaths
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string ComponentsSource
        => Path.Combine(RepositoryRoot, "src", "CanDoItAll.FileTools.FileBrowser.Components");

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.FileTools.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The FileTools repository root could not be located.");
    }
}
