namespace CanDoItAll.FileTools.FileInteraction;

/// <summary>Injectable delay used by save and preview policies.</summary>
public interface IFileInteractionDelay
{
    ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}

/// <summary>Production delay backed by cancellable <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</summary>
public sealed class SystemFileInteractionDelay : IFileInteractionDelay
{
    public static SystemFileInteractionDelay Instance { get; } = new();

    private SystemFileInteractionDelay()
    {
    }

    public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        => new(Task.Delay(delay, cancellationToken));
}
