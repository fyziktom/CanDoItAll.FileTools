function Get-FileToolsPackageManifest {
    @(
        [pscustomobject]@{
            Id = 'CanDoItAll.FileTools.Abstractions'
            Project = 'src/CanDoItAll.FileTools.Abstractions/CanDoItAll.FileTools.Abstractions.csproj'
            Assembly = 'CanDoItAll.FileTools.Abstractions'
            ProjectReferences = @()
            PackageReferences = @()
            IsRazorClassLibrary = $false
        }
        [pscustomobject]@{
            Id = 'CanDoItAll.FileTools.Desktop'
            Project = 'src/CanDoItAll.FileTools.Desktop/CanDoItAll.FileTools.Desktop.csproj'
            Assembly = 'CanDoItAll.FileTools.Desktop'
            ProjectReferences = @()
            PackageReferences = @()
            IsRazorClassLibrary = $false
        }
        [pscustomobject]@{
            Id = 'CanDoItAll.FileTools.FileBrowser.Core'
            Project = 'src/CanDoItAll.FileTools.FileBrowser.Core/CanDoItAll.FileTools.FileBrowser.Core.csproj'
            Assembly = 'CanDoItAll.FileTools.FileBrowser.Core'
            ProjectReferences = @('CanDoItAll.FileTools.Abstractions')
            PackageReferences = @()
            IsRazorClassLibrary = $false
        }
        [pscustomobject]@{
            Id = 'CanDoItAll.FileTools.FileBrowser.Components'
            Project = 'src/CanDoItAll.FileTools.FileBrowser.Components/CanDoItAll.FileTools.FileBrowser.Components.csproj'
            Assembly = 'CanDoItAll.FileTools.FileBrowser.Components'
            ProjectReferences = @(
                'CanDoItAll.FileTools.Abstractions'
                'CanDoItAll.FileTools.FileBrowser.Core'
            )
            PackageReferences = @()
            IsRazorClassLibrary = $true
        }
        [pscustomobject]@{
            Id = 'CanDoItAll.FileTools.Providers.FileSystem'
            Project = 'src/CanDoItAll.FileTools.Providers.FileSystem/CanDoItAll.FileTools.Providers.FileSystem.csproj'
            Assembly = 'CanDoItAll.FileTools.Providers.FileSystem'
            ProjectReferences = @('CanDoItAll.FileTools.Abstractions')
            PackageReferences = @()
            IsRazorClassLibrary = $false
        }
        [pscustomobject]@{
            Id = 'CanDoItAll.FileTools.FileInteraction.Core'
            Project = 'src/CanDoItAll.FileTools.FileInteraction.Core/CanDoItAll.FileTools.FileInteraction.Core.csproj'
            Assembly = 'CanDoItAll.FileTools.FileInteraction.Core'
            ProjectReferences = @('CanDoItAll.FileTools.Abstractions')
            PackageReferences = @()
            IsRazorClassLibrary = $false
        }
        [pscustomobject]@{
            Id = 'CanDoItAll.FileTools.FileInteraction.Components'
            Project = 'src/CanDoItAll.FileTools.FileInteraction.Components/CanDoItAll.FileTools.FileInteraction.Components.csproj'
            Assembly = 'CanDoItAll.FileTools.FileInteraction.Components'
            ProjectReferences = @(
                'CanDoItAll.FileTools.Abstractions'
                'CanDoItAll.FileTools.FileInteraction.Core'
            )
            PackageReferences = @()
            IsRazorClassLibrary = $true
        }
        [pscustomobject]@{
            Id = 'CanDoItAll.FileTools.FileInteraction.Markdown'
            Project = 'src/CanDoItAll.FileTools.FileInteraction.Markdown/CanDoItAll.FileTools.FileInteraction.Markdown.csproj'
            Assembly = 'CanDoItAll.FileTools.FileInteraction.Markdown'
            ProjectReferences = @(
                'CanDoItAll.FileTools.Abstractions'
                'CanDoItAll.FileTools.FileInteraction.Core'
                'CanDoItAll.FileTools.FileInteraction.Components'
            )
            PackageReferences = @('Markdig')
            IsRazorClassLibrary = $true
        }
        [pscustomobject]@{
            Id = 'CanDoItAll.FileTools.FileInteraction.Spreadsheet'
            Project = 'src/CanDoItAll.FileTools.FileInteraction.Spreadsheet/CanDoItAll.FileTools.FileInteraction.Spreadsheet.csproj'
            Assembly = 'CanDoItAll.FileTools.FileInteraction.Spreadsheet'
            ProjectReferences = @(
                'CanDoItAll.FileTools.Abstractions'
                'CanDoItAll.FileTools.FileInteraction.Core'
                'CanDoItAll.FileTools.FileInteraction.Components'
            )
            PackageReferences = @('ClosedXML')
            IsRazorClassLibrary = $true
        }
    )
}
