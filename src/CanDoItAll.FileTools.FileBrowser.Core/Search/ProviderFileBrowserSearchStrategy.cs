namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Delegates full-source search to an indexed or provider-native implementation.</summary>
public sealed class ProviderFileBrowserSearchStrategy : IFileBrowserSearchStrategy
{
    public string Id => "provider-native";

    public FileBrowserSearchScope Scope => FileBrowserSearchScope.Provider;

    public bool CanSearch(IFileBrowserProvider provider)
        => provider is IFileBrowserSearchProvider
            && provider.Descriptor.Supports(FileBrowserSourceCapabilities.NativeSearch)
            && provider.Descriptor.SupportedSearchScopes.Contains(Scope);

    public async ValueTask<FileBrowserSearchPage> SearchAsync(
        FileBrowserSearchStrategyContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Provider is not IFileBrowserSearchProvider provider)
        {
            throw new FileBrowserProviderException(new FileBrowserError(
                FileBrowserErrorCode.Unsupported,
                "This source does not implement native search."));
        }

        var page = await provider.SearchAsync(context.Request, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        FileBrowserProviderResponseValidator.ValidateSearchPage(context.Request, page);
        return page;
    }
}

