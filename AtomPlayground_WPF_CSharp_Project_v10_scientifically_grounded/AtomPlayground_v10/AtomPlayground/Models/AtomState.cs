using System.Windows;
using System.Windows.Media;

namespace AtomPlayground.Models;

public sealed class AtomState
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Symbol { get; set; } = "H";
    public string Name { get; set; } = "Hydrogen";
    public Color PrimaryColor { get; set; } = Colors.LightBlue;
    public int Protons { get; set; }
    public int Neutrons { get; set; }
    public int Electrons { get; set; }
    public int ValenceGoal { get; set; }
    public double Electronegativity { get; set; }
    public bool NobleGas { get; set; }
    public bool DonorLike { get; set; }
    public bool HeavyFissionCapable { get; set; }
    public Point Position { get; set; }
    public Vector Velocity { get; set; }
    public bool Selected { get; set; }
    public double RenderRadius { get; set; } = 18;
    public double RotationPhase { get; set; }
    public Queue<Point> Trail { get; } = new();

    public int Charge => Protons - Electrons;
    public int MassNumber => Protons + Neutrons;

    public AtomState CloneShallow()
    {
        return (AtomState)MemberwiseClone();
    }

    public void PushTrailPoint(Point point, int maxPoints)
    {
        Trail.Enqueue(point);
        while (Trail.Count > maxPoints)
        {
            Trail.Dequeue();
        }
    }

    public override string ToString() => $"{Name} ({Symbol})";
}
