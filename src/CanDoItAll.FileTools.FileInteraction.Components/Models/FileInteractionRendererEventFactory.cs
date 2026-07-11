using Microsoft.AspNetCore.Components;

namespace CanDoItAll.FileTools.FileInteraction.Components;

/// <summary>Creates renderer callbacks that capture the runtime generation they are allowed to mutate.</summary>
internal static class FileInteractionRendererEventFactory
{
    public static EventCallback<string> CreateTextChanged(
        object receiver,
        FileInteractionMode mode,
        FileInteractionRendererDescriptor? renderer,
        FileInteractionEditingRuntime? runtime,
        int generation,
        Func<string, FileInteractionEditingRuntime, int, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(handler);
        return mode == FileInteractionMode.Edit
            && renderer?.ContentKind == FileInteractionContentKind.Text
            && runtime is not null
                ? EventCallback.Factory.Create<string>(
                    receiver,
                    text => handler(text, runtime, generation))
                : default;
    }

    public static EventCallback<FileInteractionContentChange> CreateContentChanged(
        object receiver,
        FileInteractionMode mode,
        FileInteractionEditingRuntime? runtime,
        int generation,
        Func<FileInteractionContentChange, FileInteractionEditingRuntime, int, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(handler);
        return mode == FileInteractionMode.Edit && runtime is not null
            ? EventCallback.Factory.Create<FileInteractionContentChange>(
                receiver,
                change => handler(change, runtime, generation))
            : default;
    }
}
