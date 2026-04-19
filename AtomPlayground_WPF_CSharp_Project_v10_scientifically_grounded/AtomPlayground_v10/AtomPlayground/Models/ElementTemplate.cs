using System.Windows.Media;

namespace AtomPlayground.Models;

public sealed class ElementTemplate
{
    public required string Symbol { get; init; }
    public required string Name { get; init; }
    public int Protons { get; init; }
    public int DefaultNeutrons { get; init; }
    public int DefaultElectrons { get; init; }
    public int ValenceGoal { get; init; }
    public double Electronegativity { get; init; }
    public Color PrimaryColor { get; init; }
    public bool NobleGas { get; init; }
    public bool DonorLike { get; init; }
    public bool HeavyFissionCapable { get; init; }

    public override string ToString() => $"{Name} ({Symbol})";
}
