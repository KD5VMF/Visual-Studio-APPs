namespace AtomPlayground.Models;

public sealed class AtomBond
{
    public required AtomState A { get; init; }
    public required AtomState B { get; init; }
    public int Order { get; init; } = 1;
    public bool Ionic { get; init; }
}
