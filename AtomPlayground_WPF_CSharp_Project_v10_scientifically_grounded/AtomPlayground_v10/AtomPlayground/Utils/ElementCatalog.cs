using System.Windows.Media;
using AtomPlayground.Models;

namespace AtomPlayground.Utils;

public static class ElementCatalog
{
    private static readonly IReadOnlyList<ElementTemplate> _elements = new List<ElementTemplate>
    {
        new() { Symbol = "H",  Name = "Hydrogen", Protons = 1,  DefaultNeutrons = 0,  DefaultElectrons = 1,  ValenceGoal = 1, Electronegativity = 2.20, PrimaryColor = Color.FromRgb(180, 230, 255) },
        new() { Symbol = "He", Name = "Helium",   Protons = 2,  DefaultNeutrons = 2,  DefaultElectrons = 2,  ValenceGoal = 0, Electronegativity = 0.00, PrimaryColor = Color.FromRgb(255, 210, 140), NobleGas = true },
        new() { Symbol = "Li", Name = "Lithium",  Protons = 3,  DefaultNeutrons = 4,  DefaultElectrons = 3,  ValenceGoal = 1, Electronegativity = 0.98, PrimaryColor = Color.FromRgb(235, 150, 170), DonorLike = true },
        new() { Symbol = "C",  Name = "Carbon",   Protons = 6,  DefaultNeutrons = 6,  DefaultElectrons = 6,  ValenceGoal = 4, Electronegativity = 2.55, PrimaryColor = Color.FromRgb(120, 180, 220) },
        new() { Symbol = "N",  Name = "Nitrogen", Protons = 7,  DefaultNeutrons = 7,  DefaultElectrons = 7,  ValenceGoal = 3, Electronegativity = 3.04, PrimaryColor = Color.FromRgb(115, 135, 255) },
        new() { Symbol = "O",  Name = "Oxygen",   Protons = 8,  DefaultNeutrons = 8,  DefaultElectrons = 8,  ValenceGoal = 2, Electronegativity = 3.44, PrimaryColor = Color.FromRgb(255, 110, 110) },
        new() { Symbol = "F",  Name = "Fluorine", Protons = 9,  DefaultNeutrons = 10, DefaultElectrons = 9,  ValenceGoal = 1, Electronegativity = 3.98, PrimaryColor = Color.FromRgb(170, 255, 170) },
        new() { Symbol = "Ne", Name = "Neon",     Protons = 10, DefaultNeutrons = 10, DefaultElectrons = 10, ValenceGoal = 0, Electronegativity = 0.00, PrimaryColor = Color.FromRgb(255, 170, 170), NobleGas = true },
        new() { Symbol = "Na", Name = "Sodium",   Protons = 11, DefaultNeutrons = 12, DefaultElectrons = 11, ValenceGoal = 1, Electronegativity = 0.93, PrimaryColor = Color.FromRgb(210, 210, 120), DonorLike = true },
        new() { Symbol = "Mg", Name = "Magnesium",Protons = 12, DefaultNeutrons = 12, DefaultElectrons = 12, ValenceGoal = 2, Electronegativity = 1.31, PrimaryColor = Color.FromRgb(190, 220, 160), DonorLike = true },
        new() { Symbol = "Si", Name = "Silicon",  Protons = 14, DefaultNeutrons = 14, DefaultElectrons = 14, ValenceGoal = 4, Electronegativity = 1.90, PrimaryColor = Color.FromRgb(170, 200, 160) },
        new() { Symbol = "P",  Name = "Phosphorus",Protons = 15, DefaultNeutrons = 16, DefaultElectrons = 15, ValenceGoal = 3, Electronegativity = 2.19, PrimaryColor = Color.FromRgb(255, 170, 60) },
        new() { Symbol = "S",  Name = "Sulfur",   Protons = 16, DefaultNeutrons = 16, DefaultElectrons = 16, ValenceGoal = 2, Electronegativity = 2.58, PrimaryColor = Color.FromRgb(255, 225, 80) },
        new() { Symbol = "Cl", Name = "Chlorine", Protons = 17, DefaultNeutrons = 18, DefaultElectrons = 17, ValenceGoal = 1, Electronegativity = 3.16, PrimaryColor = Color.FromRgb(120, 255, 120) },
        new() { Symbol = "Ar", Name = "Argon",    Protons = 18, DefaultNeutrons = 22, DefaultElectrons = 18, ValenceGoal = 0, Electronegativity = 0.00, PrimaryColor = Color.FromRgb(180, 160, 255), NobleGas = true },
        new() { Symbol = "Fe", Name = "Iron",     Protons = 26, DefaultNeutrons = 30, DefaultElectrons = 26, ValenceGoal = 2, Electronegativity = 1.83, PrimaryColor = Color.FromRgb(205, 165, 120) },
        new() { Symbol = "Cu", Name = "Copper",   Protons = 29, DefaultNeutrons = 34, DefaultElectrons = 29, ValenceGoal = 2, Electronegativity = 1.90, PrimaryColor = Color.FromRgb(235, 130, 95) },
        new() { Symbol = "Ag", Name = "Silver",   Protons = 47, DefaultNeutrons = 61, DefaultElectrons = 47, ValenceGoal = 1, Electronegativity = 1.93, PrimaryColor = Color.FromRgb(215, 225, 235) },
        new() { Symbol = "Au", Name = "Gold",     Protons = 79, DefaultNeutrons = 118,DefaultElectrons = 79, ValenceGoal = 1, Electronegativity = 2.54, PrimaryColor = Color.FromRgb(255, 205, 80) },
        new() { Symbol = "U",  Name = "Uranium",  Protons = 92, DefaultNeutrons = 146,DefaultElectrons = 92, ValenceGoal = 6, Electronegativity = 1.38, PrimaryColor = Color.FromRgb(130, 255, 170), HeavyFissionCapable = true },
    };

    public static IReadOnlyList<ElementTemplate> All => _elements;

    public static ElementTemplate GetBySymbol(string symbol)
    {
        var match = _elements.FirstOrDefault(e => e.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
        return match ?? _elements[0];
    }

    public static ElementTemplate GetClosestByProtons(int protons)
    {
        return _elements.OrderBy(e => Math.Abs(e.Protons - protons)).ThenBy(e => e.Protons).First();
    }

    public static AtomState CreateAtom(ElementTemplate template, System.Windows.Point position, System.Windows.Vector velocity)
    {
        return new AtomState
        {
            Symbol = template.Symbol,
            Name = template.Name,
            Protons = template.Protons,
            Neutrons = template.DefaultNeutrons,
            Electrons = template.DefaultElectrons,
            ValenceGoal = template.ValenceGoal,
            Electronegativity = template.Electronegativity,
            PrimaryColor = template.PrimaryColor,
            NobleGas = template.NobleGas,
            DonorLike = template.DonorLike,
            HeavyFissionCapable = template.HeavyFissionCapable,
            Position = position,
            Velocity = velocity,
            RenderRadius = ComputeVisualRadius(template.Protons, template.DefaultNeutrons),
            RotationPhase = Random.Shared.NextDouble() * Math.PI * 2.0,
        };
    }

    public static void RefreshIdentity(AtomState atom)
    {
        var template = GetClosestByProtons(Math.Max(1, atom.Protons));
        atom.Symbol = template.Symbol;
        atom.Name = template.Name;
        atom.ValenceGoal = template.ValenceGoal;
        atom.Electronegativity = template.Electronegativity;
        atom.PrimaryColor = template.PrimaryColor;
        atom.NobleGas = template.NobleGas;
        atom.DonorLike = template.DonorLike;
        atom.HeavyFissionCapable = template.HeavyFissionCapable;
        atom.RenderRadius = ComputeVisualRadius(atom.Protons, atom.Neutrons);
    }

    public static double ComputeVisualRadius(int protons, int neutrons)
    {
        return 14.0 + Math.Sqrt(Math.Max(1, protons + neutrons)) * 1.6;
    }
}
