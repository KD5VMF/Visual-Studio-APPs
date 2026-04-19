using System.Drawing.Drawing2D;

namespace PredatorPreyEvolutionCS;

public enum Species
{
    Prey,
    Predator
}

public readonly struct Vec2
{
    public readonly double X;
    public readonly double Y;

    public Vec2(double x, double y)
    {
        X = x;
        Y = y;
    }

    public static Vec2 Zero => new(0, 0);

    public double Length => Math.Sqrt((X * X) + (Y * Y));
    public double LengthSquared => (X * X) + (Y * Y);

    public Vec2 Normalized()
    {
        var len = Length;
        return len <= 1e-9 ? Zero : new Vec2(X / len, Y / len);
    }

    public Vec2 Limit(double max)
    {
        var len = Length;
        if (len <= max || len <= 1e-9) return this;
        var scale = max / len;
        return new Vec2(X * scale, Y * scale);
    }

    public double DistanceTo(Vec2 other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    public static Vec2 FromAngle(double radians)
        => new(Math.Cos(radians), Math.Sin(radians));

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public static Vec2 operator /(Vec2 a, double s) => new(a.X / s, a.Y / s);
}

public sealed class Brain
{
    public int InputCount { get; }
    public int HiddenCount { get; }
    public int OutputCount { get; }

    private readonly double[,] _w1;
    private readonly double[] _b1;
    private readonly double[,] _w2;
    private readonly double[] _b2;

    public Brain(int inputs, int hidden, int outputs, Random rng)
    {
        InputCount = inputs;
        HiddenCount = hidden;
        OutputCount = outputs;

        _w1 = new double[hidden, inputs];
        _b1 = new double[hidden];
        _w2 = new double[outputs, hidden];
        _b2 = new double[outputs];

        Randomize(rng);
    }

    private Brain(int inputs, int hidden, int outputs, double[,] w1, double[] b1, double[,] w2, double[] b2)
    {
        InputCount = inputs;
        HiddenCount = hidden;
        OutputCount = outputs;
        _w1 = w1;
        _b1 = b1;
        _w2 = w2;
        _b2 = b2;
    }

    public void Randomize(Random rng)
    {
        for (var h = 0; h < HiddenCount; h++)
        {
            _b1[h] = NextSigned(rng);
            for (var i = 0; i < InputCount; i++)
            {
                _w1[h, i] = NextSigned(rng);
            }
        }

        for (var o = 0; o < OutputCount; o++)
        {
            _b2[o] = NextSigned(rng);
            for (var h = 0; h < HiddenCount; h++)
            {
                _w2[o, h] = NextSigned(rng);
            }
        }
    }

    public double[] Evaluate(ReadOnlySpan<double> inputs)
    {
        if (inputs.Length != InputCount)
        {
            throw new ArgumentException($"Expected {InputCount} inputs but received {inputs.Length}.");
        }

        var hidden = new double[HiddenCount];
        for (var h = 0; h < HiddenCount; h++)
        {
            var sum = _b1[h];
            for (var i = 0; i < InputCount; i++)
            {
                sum += _w1[h, i] * inputs[i];
            }

            hidden[h] = Math.Tanh(sum);
        }

        var output = new double[OutputCount];
        for (var o = 0; o < OutputCount; o++)
        {
            var sum = _b2[o];
            for (var h = 0; h < HiddenCount; h++)
            {
                sum += _w2[o, h] * hidden[h];
            }

            output[o] = Math.Tanh(sum);
        }

        return output;
    }

    public Brain MutatedClone(Random rng, double sigma)
    {
        var w1 = new double[HiddenCount, InputCount];
        var b1 = new double[HiddenCount];
        var w2 = new double[OutputCount, HiddenCount];
        var b2 = new double[OutputCount];

        for (var h = 0; h < HiddenCount; h++)
        {
            b1[h] = _b1[h] + Gaussian(rng) * sigma;
            for (var i = 0; i < InputCount; i++)
            {
                w1[h, i] = _w1[h, i] + Gaussian(rng) * sigma;
            }
        }

        for (var o = 0; o < OutputCount; o++)
        {
            b2[o] = _b2[o] + Gaussian(rng) * sigma;
            for (var h = 0; h < HiddenCount; h++)
            {
                w2[o, h] = _w2[o, h] + Gaussian(rng) * sigma;
            }
        }

        return new Brain(InputCount, HiddenCount, OutputCount, w1, b1, w2, b2);
    }

    private static double NextSigned(Random rng) => (rng.NextDouble() * 2.0) - 1.0;

    private static double Gaussian(Random rng)
    {
        var u1 = 1.0 - rng.NextDouble();
        var u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}

public sealed class AgentTraits
{
    public double MaxSpeed { get; set; }
    public double Acceleration { get; set; }
    public double VisionRange { get; set; }
    public double Metabolism { get; set; }
    public double ReproduceEnergy { get; set; }
    public double MaxAge { get; set; }
    public double Size { get; set; }
    public double MutationSigma { get; set; }

    public AgentTraits Clone() => new()
    {
        MaxSpeed = MaxSpeed,
        Acceleration = Acceleration,
        VisionRange = VisionRange,
        Metabolism = Metabolism,
        ReproduceEnergy = ReproduceEnergy,
        MaxAge = MaxAge,
        Size = Size,
        MutationSigma = MutationSigma
    };

    public AgentTraits Mutated(Random rng, Species species)
    {
        var t = Clone();

        t.MaxSpeed = Clamp(t.MaxSpeed + Signed(rng, 0.15), species == Species.Prey ? 40 : 42, species == Species.Prey ? 150 : 170);
        t.Acceleration = Clamp(t.Acceleration + Signed(rng, 6.0), 20, 160);
        t.VisionRange = Clamp(t.VisionRange + Signed(rng, 10.0), 40, 260);
        t.Metabolism = Clamp(t.Metabolism + Signed(rng, 0.05), 0.15, 2.0);
        t.ReproduceEnergy = Clamp(t.ReproduceEnergy + Signed(rng, 4.0), 30, 180);
        t.MaxAge = Clamp(t.MaxAge + Signed(rng, 6.0), 40, 260);
        t.Size = Clamp(t.Size + Signed(rng, 0.35), 3.5, 11);
        t.MutationSigma = Clamp(t.MutationSigma + Signed(rng, 0.01), 0.03, 0.25);

        return t;
    }

    public static AgentTraits CreateDefault(Species species) => species switch
    {
        Species.Prey => new AgentTraits
        {
            MaxSpeed = 92,
            Acceleration = 85,
            VisionRange = 120,
            Metabolism = 0.34,
            ReproduceEnergy = 62,
            MaxAge = 125,
            Size = 5.3,
            MutationSigma = 0.08
        },
        Species.Predator => new AgentTraits
        {
            MaxSpeed = 108,
            Acceleration = 98,
            VisionRange = 145,
            Metabolism = 0.52,
            ReproduceEnergy = 90,
            MaxAge = 145,
            Size = 7.2,
            MutationSigma = 0.08
        },
        _ => throw new ArgumentOutOfRangeException(nameof(species))
    };

    private static double Signed(Random rng, double scale) => ((rng.NextDouble() * 2.0) - 1.0) * scale;
    private static double Clamp(double v, double min, double max) => Math.Max(min, Math.Min(max, v));
}

public abstract class WorldObject
{
    public Vec2 Position { get; set; }
    public double Radius { get; set; }
    public bool IsDead { get; set; }

    public abstract void Update(World world, double dt);
    public abstract void Draw(Graphics g);
}

public sealed class Food : WorldObject
{
    public double Nutrition { get; set; }
    public double Age { get; set; }
    public double LifeSpan { get; set; }

    public Food(Vec2 position, double nutrition, double radius = 3.0)
    {
        Position = position;
        Nutrition = nutrition;
        Radius = radius;
        LifeSpan = 45.0;
    }

    public override void Update(World world, double dt)
    {
        Age += dt;
        if (Age >= LifeSpan)
        {
            IsDead = true;
        }
    }

    public override void Draw(Graphics g)
    {
        var fade = Math.Max(0.20, 1.0 - (Age / LifeSpan));
        var alpha = (int)(fade * 255.0);
        using var fill = new SolidBrush(Color.FromArgb(alpha, 255, 213, 90));
        using var outline = new Pen(Color.FromArgb(alpha, 120, 80, 20), 1f);
        var rect = new RectangleF((float)(Position.X - Radius), (float)(Position.Y - Radius), (float)(Radius * 2), (float)(Radius * 2));
        g.FillEllipse(fill, rect);
        g.DrawEllipse(outline, rect);
    }
}

public sealed class Plant : WorldObject
{
    private double _fruitTimer;
    private double _spreadTimer;

    public Plant(Vec2 position, Random rng)
    {
        Position = position;
        Radius = 7;
        _fruitTimer = 1.5 + (rng.NextDouble() * 4.0);
        _spreadTimer = 12.0 + (rng.NextDouble() * 20.0);
    }

    public override void Update(World world, double dt)
    {
        _fruitTimer -= dt;
        _spreadTimer -= dt;

        if (_fruitTimer <= 0)
        {
            _fruitTimer = 2.0 + (world.Rng.NextDouble() * 5.0);
            if (world.Foods.Count < world.MaxFood)
            {
                var angle = world.Rng.NextDouble() * Math.PI * 2.0;
                var distance = 8.0 + (world.Rng.NextDouble() * 10.0);
                var pos = Position + (Vec2.FromAngle(angle) * distance);
                pos = world.ClampToWorld(pos, 6);
                world.Foods.Add(new Food(pos, 16 + (world.Rng.NextDouble() * 8.0), 2.8 + (world.Rng.NextDouble() * 1.8)));
            }
        }

        if (_spreadTimer <= 0 && world.Plants.Count < world.MaxPlants)
        {
            _spreadTimer = 16.0 + (world.Rng.NextDouble() * 18.0);
            if (world.Rng.NextDouble() < 0.40)
            {
                var angle = world.Rng.NextDouble() * Math.PI * 2.0;
                var distance = 20.0 + (world.Rng.NextDouble() * 55.0);
                var pos = Position + (Vec2.FromAngle(angle) * distance);
                pos = world.ClampToWorld(pos, 10);
                if (!world.IsPlantTooClose(pos, 12))
                {
                    world.Plants.Add(new Plant(pos, world.Rng));
                }
            }
        }
    }

    public override void Draw(Graphics g)
    {
        var rect = new RectangleF((float)(Position.X - Radius), (float)(Position.Y - Radius), (float)(Radius * 2), (float)(Radius * 2));
        using var fill = new SolidBrush(Color.FromArgb(44, 176, 89));
        using var outline = new Pen(Color.FromArgb(16, 76, 40), 1.5f);
        g.FillEllipse(fill, rect);
        g.DrawEllipse(outline, rect);

        using var core = new SolidBrush(Color.FromArgb(102, 214, 120));
        var inner = Radius * 0.45f;
        g.FillEllipse(core, (float)(Position.X - inner), (float)(Position.Y - inner), (float)(inner * 2), (float)(inner * 2));
    }
}

public abstract class Agent : WorldObject
{
    public Species Species { get; }
    public Vec2 Velocity { get; set; }
    public double Energy { get; set; }
    public double Age { get; set; }
    public int Generation { get; set; }
    public Brain Brain { get; set; }
    public AgentTraits Traits { get; set; }
    public double ReproductionCooldown { get; set; }
    public Color BodyColor { get; set; }

    protected Agent(Species species, Vec2 position, Brain brain, AgentTraits traits, Color color)
    {
        Species = species;
        Position = position;
        Brain = brain;
        Traits = traits;
        BodyColor = color;
        Radius = traits.Size;
        Energy = traits.ReproduceEnergy * 0.72;
    }

    protected void IntegrateMovement(Vec2 steering, double sprint, World world, double dt)
    {
        var accel = steering.Limit(1.0) * Traits.Acceleration * (1.0 + (0.65 * sprint));
        Velocity = (Velocity + (accel * dt)).Limit(Traits.MaxSpeed * (1.0 + (0.45 * sprint)));
        Position += Velocity * dt;

        var hit = false;
        if (Position.X < Radius)
        {
            Position = new Vec2(Radius, Position.Y);
            Velocity = new Vec2(Math.Abs(Velocity.X) * 0.5, Velocity.Y);
            hit = true;
        }
        else if (Position.X > world.Width - Radius)
        {
            Position = new Vec2(world.Width - Radius, Position.Y);
            Velocity = new Vec2(-Math.Abs(Velocity.X) * 0.5, Velocity.Y);
            hit = true;
        }

        if (Position.Y < Radius)
        {
            Position = new Vec2(Position.X, Radius);
            Velocity = new Vec2(Velocity.X, Math.Abs(Velocity.Y) * 0.5);
            hit = true;
        }
        else if (Position.Y > world.Height - Radius)
        {
            Position = new Vec2(Position.X, world.Height - Radius);
            Velocity = new Vec2(Velocity.X, -Math.Abs(Velocity.Y) * 0.5);
            hit = true;
        }

        if (hit)
        {
            Velocity *= 0.95;
        }

        var motionCost = Velocity.Length * 0.0065;
        Energy -= ((Traits.Metabolism + motionCost) * (1.0 + (0.75 * sprint))) * dt;
        Age += dt;
        ReproductionCooldown = Math.Max(0, ReproductionCooldown - dt);
        Radius = Traits.Size;
    }

    protected static Color MutatedColor(Color baseColor, Random rng)
    {
        int Mutate(int v)
        {
            var delta = (int)Math.Round(((rng.NextDouble() * 2.0) - 1.0) * 14.0);
            return Math.Clamp(v + delta, 24, 255);
        }

        return Color.FromArgb(Mutate(baseColor.R), Mutate(baseColor.G), Mutate(baseColor.B));
    }

    protected static double Clamp01(double value) => Math.Max(0, Math.Min(1, value));

    protected (double dx, double dy, double proximity) Sense(WorldObject? target, double vision)
    {
        if (target is null)
        {
            return (0, 0, 0);
        }

        var delta = target.Position - Position;
        var dist = Math.Max(1e-6, delta.Length);
        var dir = delta / dist;
        var proximity = Math.Max(0, 1.0 - (dist / vision));
        return (dir.X, dir.Y, proximity);
    }

    protected void Die(World world)
    {
        if (IsDead) return;
        IsDead = true;
        world.SpawnCarrion(Position, Energy, Species == Species.Predator ? 4 : 3);
    }

    public override void Draw(Graphics g)
    {
        using var fill = new SolidBrush(BodyColor);
        using var outline = new Pen(Color.FromArgb(24, 24, 24), 1.4f);

        var rect = new RectangleF((float)(Position.X - Radius), (float)(Position.Y - Radius), (float)(Radius * 2), (float)(Radius * 2));
        g.FillEllipse(fill, rect);
        g.DrawEllipse(outline, rect);

        var heading = Velocity.Length > 1e-3 ? Velocity.Normalized() : Vec2.FromAngle(0);
        var nose = Position + (heading * (Radius + 5));
        using var pen = new Pen(Color.WhiteSmoke, 1.4f);
        g.DrawLine(pen, (float)Position.X, (float)Position.Y, (float)nose.X, (float)nose.Y);

        if (Species == Species.Predator)
        {
            using var eye = new SolidBrush(Color.FromArgb(255, 236, 236));
            var off = new Vec2(-heading.Y, heading.X) * Math.Max(1.2, Radius * 0.25);
            g.FillEllipse(eye, (float)(Position.X + off.X - 1.5), (float)(Position.Y + off.Y - 1.5), 3, 3);
            g.FillEllipse(eye, (float)(Position.X - off.X - 1.5), (float)(Position.Y - off.Y - 1.5), 3, 3);
        }
    }
}

public sealed class Prey : Agent
{
    public Prey(Vec2 position, Brain brain, AgentTraits traits, Color color)
        : base(Species.Prey, position, brain, traits, color)
    {
    }

    public override void Update(World world, double dt)
    {
        if (IsDead) return;

        var nearestFood = world.FindNearestFood(Position, Traits.VisionRange);
        var nearestPredator = world.FindNearestPredator(Position, Traits.VisionRange, null);
        var nearestPeer = world.FindNearestPrey(Position, Traits.VisionRange, this);

        var foodSense = Sense(nearestFood, Traits.VisionRange);
        var predatorSense = Sense(nearestPredator, Traits.VisionRange);
        var peerSense = Sense(nearestPeer, Traits.VisionRange);

        var inputs = new double[]
        {
            Clamp01(Energy / Traits.ReproduceEnergy),
            Clamp01(Age / Traits.MaxAge),
            Velocity.X / Math.Max(1.0, Traits.MaxSpeed),
            Velocity.Y / Math.Max(1.0, Traits.MaxSpeed),
            foodSense.dx,
            foodSense.dy,
            foodSense.proximity,
            predatorSense.dx,
            predatorSense.dy,
            predatorSense.proximity,
            peerSense.dx,
            peerSense.dy,
            peerSense.proximity,
            (Math.Sin((Age * 0.8) + (Generation * 0.23)) + 1.0) * 0.5
        };

        var output = Brain.Evaluate(inputs);
        var steering = new Vec2(output[0], output[1]);

        if (predatorSense.proximity > 0.01)
        {
            steering += new Vec2(-predatorSense.dx, -predatorSense.dy) * (1.2 + predatorSense.proximity);
        }

        if (foodSense.proximity > 0.01)
        {
            steering += new Vec2(foodSense.dx, foodSense.dy) * (0.6 + foodSense.proximity);
        }

        if (peerSense.proximity > 0.25)
        {
            steering += new Vec2(peerSense.dx, peerSense.dy) * 0.15;
        }

        var sprint = Clamp01((output[2] + 1.0) * 0.5);
        IntegrateMovement(steering, sprint, world, dt);

        foreach (var food in world.Foods)
        {
            if (food.IsDead) continue;
            var reach = Radius + food.Radius + 1.5;
            if ((food.Position - Position).LengthSquared <= (reach * reach))
            {
                food.IsDead = true;
                Energy += food.Nutrition;
                break;
            }
        }

        if (Energy > Traits.ReproduceEnergy && ReproductionCooldown <= 0 && output[3] > 0.25)
        {
            Energy *= 0.56;
            ReproductionCooldown = 3.0 + (world.Rng.NextDouble() * 3.5);

            var child = new Prey(
                world.JitteredPosition(Position, 16),
                Brain.MutatedClone(world.Rng, Traits.MutationSigma),
                Traits.Mutated(world.Rng, Species.Prey),
                MutatedColor(BodyColor, world.Rng));

            child.Generation = Generation + 1;
            child.Energy = Math.Max(22, Energy * 0.82);
            child.Velocity = Vec2.FromAngle(world.Rng.NextDouble() * Math.PI * 2.0) * (20 + (world.Rng.NextDouble() * 20));
            world.NewPrey.Add(child);
        }

        if (Energy <= 0 || Age >= Traits.MaxAge)
        {
            Die(world);
        }
    }
}

public sealed class Predator : Agent
{
    public Predator(Vec2 position, Brain brain, AgentTraits traits, Color color)
        : base(Species.Predator, position, brain, traits, color)
    {
        Energy = traits.ReproduceEnergy * 0.62;
    }

    public override void Update(World world, double dt)
    {
        if (IsDead) return;

        var nearestPrey = world.FindNearestPrey(Position, Traits.VisionRange, null);
        var nearestPredator = world.FindNearestPredator(Position, Traits.VisionRange, this);

        var preySense = Sense(nearestPrey, Traits.VisionRange);
        var predatorSense = Sense(nearestPredator, Traits.VisionRange);

        var inputs = new double[]
        {
            Clamp01(Energy / Traits.ReproduceEnergy),
            Clamp01(Age / Traits.MaxAge),
            Velocity.X / Math.Max(1.0, Traits.MaxSpeed),
            Velocity.Y / Math.Max(1.0, Traits.MaxSpeed),
            preySense.dx,
            preySense.dy,
            preySense.proximity,
            predatorSense.dx,
            predatorSense.dy,
            predatorSense.proximity,
            nearestPrey is null ? 0.0 : Clamp01((nearestPrey.Energy / Math.Max(1.0, nearestPrey.Traits.ReproduceEnergy))),
            Math.Cos((Age * 0.55) + (Generation * 0.31)),
            Math.Sin((Age * 0.34) + (Generation * 0.19)),
            world.Foods.Count / (double)Math.Max(1, world.MaxFood)
        };

        var output = Brain.Evaluate(inputs);
        var steering = new Vec2(output[0], output[1]);

        if (preySense.proximity > 0.01)
        {
            steering += new Vec2(preySense.dx, preySense.dy) * (0.85 + (preySense.proximity * 1.45));
        }

        if (predatorSense.proximity > 0.35)
        {
            steering += new Vec2(-predatorSense.dx, -predatorSense.dy) * 0.35;
        }

        var sprint = Clamp01((output[2] + 1.0) * 0.5);
        IntegrateMovement(steering, sprint, world, dt);

        foreach (var prey in world.Preys)
        {
            if (prey.IsDead) continue;
            var reach = Radius + prey.Radius + 1.5;
            if ((prey.Position - Position).LengthSquared <= (reach * reach))
            {
                prey.IsDead = true;
                world.SpawnCarrion(prey.Position, prey.Energy * 0.30, 2);
                Energy += Math.Max(16, prey.Energy * 0.92);
                break;
            }
        }

        if (Energy > Traits.ReproduceEnergy && ReproductionCooldown <= 0 && output[3] > 0.10)
        {
            Energy *= 0.53;
            ReproductionCooldown = 4.5 + (world.Rng.NextDouble() * 5.0);

            var child = new Predator(
                world.JitteredPosition(Position, 18),
                Brain.MutatedClone(world.Rng, Traits.MutationSigma),
                Traits.Mutated(world.Rng, Species.Predator),
                MutatedColor(BodyColor, world.Rng));

            child.Generation = Generation + 1;
            child.Energy = Math.Max(26, Energy * 0.78);
            child.Velocity = Vec2.FromAngle(world.Rng.NextDouble() * Math.PI * 2.0) * (24 + (world.Rng.NextDouble() * 24));
            world.NewPredators.Add(child);
        }

        if (Energy <= 0 || Age >= Traits.MaxAge)
        {
            Die(world);
        }
    }
}

public sealed class HistoryPoint
{
    public double Time { get; set; }
    public int PreyCount { get; set; }
    public int PredatorCount { get; set; }
    public int PlantCount { get; set; }
    public int FoodCount { get; set; }
}

public sealed class World
{
    public int Width { get; private set; }
    public int Height { get; private set; }

    public Random Rng { get; }
    public List<Plant> Plants { get; } = new();
    public List<Food> Foods { get; } = new();
    public List<Prey> Preys { get; } = new();
    public List<Predator> Predators { get; } = new();

    public List<Prey> NewPrey { get; } = new();
    public List<Predator> NewPredators { get; } = new();
    public List<HistoryPoint> History { get; } = new();

    public int MaxFood { get; set; } = 1200;
    public int MaxPlants { get; set; } = 260;
    public double SimulatedTime { get; private set; }
    public int StepCounter { get; private set; }
    public WorldObject? SelectedObject { get; private set; }

    private double _historyTimer;

    public World(int width, int height, int? seed = null)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        Rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public void Resize(int width, int height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
    }

    public void Reset(int preyCount, int predatorCount, int plantCount)
    {
        Plants.Clear();
        Foods.Clear();
        Preys.Clear();
        Predators.Clear();
        NewPrey.Clear();
        NewPredators.Clear();
        History.Clear();
        SelectedObject = null;
        SimulatedTime = 0;
        StepCounter = 0;
        _historyTimer = 0;

        for (var i = 0; i < plantCount; i++)
        {
            Plants.Add(new Plant(RandomPosition(12), Rng));
        }

        for (var i = 0; i < preyCount; i++)
        {
            var traits = AgentTraits.CreateDefault(Species.Prey);
            var brain = new Brain(14, 10, 4, Rng);
            var prey = new Prey(RandomPosition(16), brain, traits, RandomPreyColor());
            prey.Generation = 1;
            prey.Velocity = Vec2.FromAngle(Rng.NextDouble() * Math.PI * 2.0) * (10 + (Rng.NextDouble() * 20));
            prey.Energy = 34 + (Rng.NextDouble() * 30);
            Preys.Add(prey);
        }

        for (var i = 0; i < predatorCount; i++)
        {
            var traits = AgentTraits.CreateDefault(Species.Predator);
            var brain = new Brain(14, 10, 4, Rng);
            var predator = new Predator(RandomPosition(20), brain, traits, RandomPredatorColor());
            predator.Generation = 1;
            predator.Velocity = Vec2.FromAngle(Rng.NextDouble() * Math.PI * 2.0) * (10 + (Rng.NextDouble() * 20));
            predator.Energy = 54 + (Rng.NextDouble() * 28);
            Predators.Add(predator);
        }

        for (var i = 0; i < plantCount * 2; i++)
        {
            Foods.Add(new Food(RandomPosition(10), 14 + (Rng.NextDouble() * 10), 2.5 + (Rng.NextDouble() * 2.0)));
        }
    }

    public void Update(double dt)
    {
        SimulatedTime += dt;
        StepCounter++;
        _historyTimer += dt;

        foreach (var plant in Plants.ToArray())
        {
            plant.Update(this, dt);
        }

        foreach (var food in Foods.ToArray())
        {
            food.Update(this, dt);
        }

        foreach (var prey in Preys.ToArray())
        {
            prey.Update(this, dt);
        }

        foreach (var predator in Predators.ToArray())
        {
            predator.Update(this, dt);
        }

        if (NewPrey.Count > 0)
        {
            Preys.AddRange(NewPrey);
            NewPrey.Clear();
        }

        if (NewPredators.Count > 0)
        {
            Predators.AddRange(NewPredators);
            NewPredators.Clear();
        }

        Foods.RemoveAll(f => f.IsDead);
        Preys.RemoveAll(p => p.IsDead);
        Predators.RemoveAll(p => p.IsDead);

        if (SelectedObject is not null && SelectedObject.IsDead)
        {
            SelectedObject = null;
        }

        if (_historyTimer >= 0.50)
        {
            _historyTimer = 0;
            History.Add(new HistoryPoint
            {
                Time = SimulatedTime,
                PreyCount = Preys.Count,
                PredatorCount = Predators.Count,
                PlantCount = Plants.Count,
                FoodCount = Foods.Count
            });

            if (History.Count > 240)
            {
                History.RemoveAt(0);
            }
        }
    }

    public void Draw(Graphics g, Rectangle bounds)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var background = new SolidBrush(Color.FromArgb(14, 18, 28));
        g.FillRectangle(background, bounds);

        using var vignetteBrush = new LinearGradientBrush(bounds, Color.FromArgb(8, 10, 18), Color.FromArgb(18, 22, 34), 90f);
        g.FillRectangle(vignetteBrush, bounds);

        foreach (var plant in Plants)
        {
            plant.Draw(g);
        }

        foreach (var food in Foods)
        {
            food.Draw(g);
        }

        foreach (var prey in Preys)
        {
            prey.Draw(g);
        }

        foreach (var predator in Predators)
        {
            predator.Draw(g);
        }

        if (SelectedObject is not null && !SelectedObject.IsDead)
        {
            using var pen = new Pen(Color.FromArgb(210, 255, 255, 255), 1.6f) { DashStyle = DashStyle.Dash };
            var glow = SelectedObject.Radius + 7;
            g.DrawEllipse(pen, (float)(SelectedObject.Position.X - glow), (float)(SelectedObject.Position.Y - glow), (float)(glow * 2), (float)(glow * 2));

            if (SelectedObject is Agent selectedAgent)
            {
                using var visionPen = new Pen(Color.FromArgb(55, 255, 255, 255), 1f);
                var vr = selectedAgent.Traits.VisionRange;
                g.DrawEllipse(visionPen, (float)(selectedAgent.Position.X - vr), (float)(selectedAgent.Position.Y - vr), (float)(vr * 2), (float)(vr * 2));
            }
        }

        DrawHud(g, bounds);
    }

    private void DrawHud(Graphics g, Rectangle bounds)
    {
        var hudWidth = Math.Min(360, Math.Max(280, bounds.Width / 4));
        var hudRect = new Rectangle(bounds.Right - hudWidth - 12, bounds.Top + 12, hudWidth, bounds.Height - 24);

        using var hudFill = new SolidBrush(Color.FromArgb(145, 8, 11, 18));
        using var hudOutline = new Pen(Color.FromArgb(95, 255, 255, 255), 1f);
        g.FillRoundedRectangle(hudFill, hudRect, 18);
        g.DrawRoundedRectangle(hudOutline, hudRect, 18);

        using var titleFont = new Font("Segoe UI", 14f, FontStyle.Bold);
        using var bodyFont = new Font("Consolas", 10.5f, FontStyle.Regular);
        using var smallFont = new Font("Segoe UI", 9f, FontStyle.Regular);
        using var white = new SolidBrush(Color.WhiteSmoke);
        using var soft = new SolidBrush(Color.FromArgb(220, 224, 231));

        var x = hudRect.Left + 18;
        var y = hudRect.Top + 16;

        g.DrawString("Predator / Prey Evolution", titleFont, white, x, y);
        y += 34;

        var avgPreyGen = Preys.Count == 0 ? 0.0 : Preys.Average(p => p.Generation);
        var avgPredGen = Predators.Count == 0 ? 0.0 : Predators.Average(p => p.Generation);

        var stats = string.Join(Environment.NewLine, new[]
        {
            $"Time         : {FormatTime(SimulatedTime)}",
            $"Steps        : {StepCounter:N0}",
            $"Plants       : {Plants.Count,4}",
            $"Food         : {Foods.Count,4}",
            $"Prey         : {Preys.Count,4}",
            $"Predators    : {Predators.Count,4}",
            $"Avg prey gen : {avgPreyGen,4:F1}",
            $"Avg pred gen : {avgPredGen,4:F1}"
        });

        g.DrawString(stats, bodyFont, soft, x, y);
        y += 152;

        var chartRect = new Rectangle(hudRect.Left + 18, y, hudRect.Width - 36, 160);
        DrawPopulationChart(g, chartRect);
        y += 176;

        g.DrawString("Selected", titleFont, white, x, y);
        y += 28;

        if (SelectedObject is Agent agent)
        {
            var details = string.Join(Environment.NewLine, new[]
            {
                $"Species      : {agent.Species}",
                $"Generation   : {agent.Generation}",
                $"Energy       : {agent.Energy:F1}",
                $"Age          : {agent.Age:F1} / {agent.Traits.MaxAge:F0}",
                $"Speed        : {agent.Velocity.Length:F1}",
                $"Max speed    : {agent.Traits.MaxSpeed:F1}",
                $"Accel        : {agent.Traits.Acceleration:F1}",
                $"Vision       : {agent.Traits.VisionRange:F1}",
                $"Metabolism   : {agent.Traits.Metabolism:F2}",
                $"Repro @      : {agent.Traits.ReproduceEnergy:F1}",
                $"Mutation σ   : {agent.Traits.MutationSigma:F2}"
            });
            g.DrawString(details, bodyFont, soft, x, y);
        }
        else if (SelectedObject is Plant)
        {
            g.DrawString("Plant\nProduces food pellets over time\nand can spread to nearby ground.", smallFont, soft, x, y);
        }
        else if (SelectedObject is Food)
        {
            g.DrawString("Food pellet\nPrey consumes this for energy.\nDead agents become carrion food too.", smallFont, soft, x, y);
        }
        else
        {
            g.DrawString("Click an agent or object in the world\nto inspect its current traits.", smallFont, soft, x, y);
        }
    }

    private void DrawPopulationChart(Graphics g, Rectangle rect)
    {
        using var fill = new SolidBrush(Color.FromArgb(110, 18, 25, 35));
        using var outline = new Pen(Color.FromArgb(90, 255, 255, 255), 1f);
        g.FillRoundedRectangle(fill, rect, 14);
        g.DrawRoundedRectangle(outline, rect, 14);

        using var font = new Font("Segoe UI", 9f, FontStyle.Regular);
        using var labelBrush = new SolidBrush(Color.Gainsboro);
        g.DrawString("Population history", font, labelBrush, rect.Left + 10, rect.Top + 8);

        var plot = Rectangle.Inflate(rect, -12, -28);
        if (History.Count < 2)
        {
            g.DrawString("Collecting samples...", font, labelBrush, plot.Left + 10, plot.Top + 20);
            return;
        }

        var maxY = Math.Max(10, History.Max(h => Math.Max(Math.Max(h.PreyCount, h.PredatorCount), Math.Max(h.PlantCount, h.FoodCount))));
        var preyPoints = new PointF[History.Count];
        var predPoints = new PointF[History.Count];
        var plantPoints = new PointF[History.Count];
        var foodPoints = new PointF[History.Count];

        for (var i = 0; i < History.Count; i++)
        {
            var t = i / (float)Math.Max(1, History.Count - 1);
            float px = plot.Left + (plot.Width * t);

            preyPoints[i] = new PointF(px, plot.Bottom - ((float)History[i].PreyCount / maxY * plot.Height));
            predPoints[i] = new PointF(px, plot.Bottom - ((float)History[i].PredatorCount / maxY * plot.Height));
            plantPoints[i] = new PointF(px, plot.Bottom - ((float)History[i].PlantCount / maxY * plot.Height));
            foodPoints[i] = new PointF(px, plot.Bottom - ((float)History[i].FoodCount / maxY * plot.Height));
        }

        using var grid = new Pen(Color.FromArgb(28, 255, 255, 255), 1f);
        for (var i = 0; i <= 4; i++)
        {
            var yy = plot.Top + (plot.Height * (i / 4f));
            g.DrawLine(grid, plot.Left, yy, plot.Right, yy);
        }

        using var preyPen = new Pen(Color.FromArgb(90, 190, 255), 2f);
        using var predPen = new Pen(Color.FromArgb(255, 92, 92), 2f);
        using var plantPen = new Pen(Color.FromArgb(66, 214, 110), 2f);
        using var foodPen = new Pen(Color.FromArgb(255, 216, 110), 1.6f);

        g.DrawLines(plantPen, plantPoints);
        g.DrawLines(foodPen, foodPoints);
        g.DrawLines(preyPen, preyPoints);
        g.DrawLines(predPen, predPoints);

        var legendY = rect.Bottom - 20;
        DrawLegend(g, rect.Left + 12, legendY, Color.FromArgb(66, 214, 110), "Plants");
        DrawLegend(g, rect.Left + 88, legendY, Color.FromArgb(255, 216, 110), "Food");
        DrawLegend(g, rect.Left + 154, legendY, Color.FromArgb(90, 190, 255), "Prey");
        DrawLegend(g, rect.Left + 214, legendY, Color.FromArgb(255, 92, 92), "Predators");
    }

    private static void DrawLegend(Graphics g, int x, int y, Color color, string text)
    {
        using var brush = new SolidBrush(color);
        using var font = new Font("Segoe UI", 8f, FontStyle.Regular);
        using var textBrush = new SolidBrush(Color.Gainsboro);
        g.FillRectangle(brush, x, y + 5, 12, 3);
        g.DrawString(text, font, textBrush, x + 16, y);
    }

    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalHours >= 1) return ts.ToString(@"hh\:mm\:ss");
        return ts.ToString(@"mm\:ss");
    }

    public void SelectNearest(Point location, double maxDistance)
    {
        var pos = new Vec2(location.X, location.Y);
        WorldObject? best = null;
        var bestDist = maxDistance;

        foreach (var obj in EnumerateObjects())
        {
            if (obj.IsDead) continue;
            var dist = obj.Position.DistanceTo(pos);
            if (dist <= bestDist)
            {
                best = obj;
                bestDist = dist;
            }
        }

        SelectedObject = best;
    }

    public IEnumerable<WorldObject> EnumerateObjects()
    {
        foreach (var plant in Plants) yield return plant;
        foreach (var food in Foods) yield return food;
        foreach (var prey in Preys) yield return prey;
        foreach (var predator in Predators) yield return predator;
    }

    public Prey? FindNearestPrey(Vec2 position, double maxDistance, Prey? except)
    {
        Prey? best = null;
        var bestDist = maxDistance;

        foreach (var prey in Preys)
        {
            if (prey.IsDead || ReferenceEquals(prey, except)) continue;
            var dist = prey.Position.DistanceTo(position);
            if (dist < bestDist)
            {
                best = prey;
                bestDist = dist;
            }
        }

        return best;
    }

    public Predator? FindNearestPredator(Vec2 position, double maxDistance, Predator? except)
    {
        Predator? best = null;
        var bestDist = maxDistance;

        foreach (var predator in Predators)
        {
            if (predator.IsDead || ReferenceEquals(predator, except)) continue;
            var dist = predator.Position.DistanceTo(position);
            if (dist < bestDist)
            {
                best = predator;
                bestDist = dist;
            }
        }

        return best;
    }

    public Food? FindNearestFood(Vec2 position, double maxDistance)
    {
        Food? best = null;
        var bestDist = maxDistance;

        foreach (var food in Foods)
        {
            if (food.IsDead) continue;
            var dist = food.Position.DistanceTo(position);
            if (dist < bestDist)
            {
                best = food;
                bestDist = dist;
            }
        }

        return best;
    }

    public void SpawnCarrion(Vec2 position, double remainingEnergy, int pieces)
    {
        pieces = Math.Max(1, pieces);
        var nutrition = Math.Max(5.0, remainingEnergy / pieces);

        for (var i = 0; i < pieces; i++)
        {
            if (Foods.Count >= MaxFood) break;
            var angle = Rng.NextDouble() * Math.PI * 2.0;
            var dist = 4 + (Rng.NextDouble() * 10);
            var pos = ClampToWorld(position + (Vec2.FromAngle(angle) * dist), 4);
            Foods.Add(new Food(pos, nutrition * (0.65 + (Rng.NextDouble() * 0.45)), 2.6 + (Rng.NextDouble() * 1.4))
            {
                LifeSpan = 28 + (Rng.NextDouble() * 18)
            });
        }
    }

    public bool IsPlantTooClose(Vec2 pos, double minDistance)
    {
        foreach (var plant in Plants)
        {
            if ((plant.Position - pos).LengthSquared < (minDistance * minDistance))
            {
                return true;
            }
        }

        return false;
    }

    public Vec2 RandomPosition(double margin)
    {
        var x = margin + (Rng.NextDouble() * Math.Max(1.0, Width - (margin * 2.0)));
        var y = margin + (Rng.NextDouble() * Math.Max(1.0, Height - (margin * 2.0)));
        return new Vec2(x, y);
    }

    public Vec2 JitteredPosition(Vec2 origin, double radius)
    {
        var angle = Rng.NextDouble() * Math.PI * 2.0;
        var dist = Rng.NextDouble() * radius;
        return ClampToWorld(origin + (Vec2.FromAngle(angle) * dist), 6);
    }

    public Vec2 ClampToWorld(Vec2 pos, double margin)
    {
        return new Vec2(
            Math.Clamp(pos.X, margin, Width - margin),
            Math.Clamp(pos.Y, margin, Height - margin));
    }

    private Color RandomPreyColor()
        => Color.FromArgb(70 + Rng.Next(50), 150 + Rng.Next(80), 220 + Rng.Next(35));

    private Color RandomPredatorColor()
        => Color.FromArgb(220 + Rng.Next(35), 70 + Rng.Next(60), 70 + Rng.Next(50));
}

public static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle bounds, int radius)
    {
        using var path = RoundedRectangle(bounds, radius);
        g.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics g, Pen pen, Rectangle bounds, int radius)
    {
        using var path = RoundedRectangle(bounds, radius);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        var path = new GraphicsPath();

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
    }
}
