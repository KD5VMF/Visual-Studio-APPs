namespace AtomPlayground.Models;

public sealed class WorldEvent
{
    public required DateTime Timestamp { get; init; }
    public required string Message { get; init; }

    public override string ToString() => $"[{Timestamp:HH:mm:ss}] {Message}";
}
