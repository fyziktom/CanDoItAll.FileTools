# CanDoItAll.FileTools.FileInteraction.Markdown

Optional Markdown viewing and editing surfaces for `FileInteraction`.

```csharp
var composition = new FileInteractionComponentBuilder()
    .AddBuiltIns()
    .AddMarkdown()
    .Build();
```

`AddMarkdown` contributes a higher-priority `.md`, `.markdown`, and `text/markdown` profile. It
uses the base text editor and bounded history factory, while its Markdown viewer is also used by
the debounced split preview.

The viewer deliberately has no navigation or remote-fetch authority. Raw HTML is disabled and
all Markdown links, autolinks, and images render as inert labels without `href` or `src`. A host
that wants trusted link navigation should expose it as a separate, explicit host-owned action.
