using System.Collections.Concurrent;
using System.Drawing;
using System.Runtime.InteropServices;

namespace LifeForgeAccelerated;

public enum Species
{
    Prey,
    Predator
}

public enum KnowledgeKind
{
    Food,
    Danger,
    Water,
    Hunt
}

public enum SelectionKind
{
    None,
    Agent,
    Plant,
    Food,
    Water
}

public readonly struct Vec2
{
    public readonly float X;
    public readonly float Y;

    public Vec2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static Vec2 Zero => new(0f, 0f);
    public float Length => MathF.Sqrt((X * X) + (Y * Y));
    public float LengthSquared => (X * X) + (Y * Y);

    public Vec2 Normalized()
    {
        var len = Length;
        return len <= 0.0001f ? Zero : new Vec2(X / len, Y / len);
    }

    public Vec2 Limit(float max)
    {
        var lenSq = LengthSquared;
        if (lenSq <= (max * max)) return this;
        var len = MathF.Sqrt(lenSq);
        if (len <= 0.0001f) return Zero;
        var s = max / len;
        return new Vec2(X * s, Y * s);
    }

    public Vec2 Perpendicular() => new(-Y, X);
    public float DistanceTo(in Vec2 other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    public static Vec2 FromAngle(float radians) => new(MathF.Cos(radians), MathF.Sin(radians));
    public static float Dot(in Vec2 a, in Vec2 b) => (a.X * b.X) + (a.Y * b.Y);
    public static Vec2 Lerp(in Vec2 a, in Vec2 b, float t) => new(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t));

    public static Vec2 operator +(in Vec2 a, in Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(in Vec2 a, in Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(in Vec2 a, float s) => new(a.X * s, a.Y * s);
    public static Vec2 operator /(in Vec2 a, float s) => new(a.X / s, a.Y / s);
}

public sealed class Brain
{
    public int InputCount { get; }
    public int HiddenCount { get; }
    public int OutputCount { get; }
    public float Plasticity { get; }

    private readonly float[] _w1;
    private readonly float[] _b1;
    private readonly float[] _w2;
    private readonly float[] _b2;
    private readonly float[] _adaptiveBias;

    public Brain(int inputs, int hidden, int outputs, Random rng, float plasticity = 0.12f)
    {
        InputCount = inputs;
        HiddenCount = hidden;
        OutputCount = outputs;
        Plasticity = plasticity;

        _w1 = new float[hidden * inputs];
        _b1 = new float[hidden];
        _w2 = new float[outputs * hidden];
        _b2 = new float[outputs];
        _adaptiveBias = new float[outputs];
        Randomize(rng);
    }

    private Brain(int inputs, int hidden, int outputs, float[] w1, float[] b1, float[] w2, float[] b2, float[] adaptiveBias, float plasticity)
    {
        InputCount = inputs;
        HiddenCount = hidden;
        OutputCount = outputs;
        _w1 = w1;
        _b1 = b1;
        _w2 = w2;
        _b2 = b2;
        _adaptiveBias = adaptiveBias;
        Plasticity = plasticity;
    }

    public void Randomize(Random rng)
    {
        for (var i = 0; i < _w1.Length; i++) _w1[i] = NextSigned(rng);
        for (var i = 0; i < _w2.Length; i++) _w2[i] = NextSigned(rng);
        for (var i = 0; i < _b1.Length; i++) _b1[i] = NextSigned(rng);
        for (var i = 0; i < _b2.Length; i++) _b2[i] = NextSigned(rng);
        Array.Clear(_adaptiveBias);
    }

    public void Evaluate(float[] inputs, float[] hidden, float[] outputs)
    {
        for (var h = 0; h < HiddenCount; h++)
        {
            var sum = _b1[h];
            var offset = h * InputCount;
            for (var i = 0; i < InputCount; i++)
            {
                sum += _w1[offset + i] * inputs[i];
            }
            hidden[h] = MathF.Tanh(sum);
        }

        for (var o = 0; o < OutputCount; o++)
        {
            var sum = _b2[o] + _adaptiveBias[o];
            var offset = o * HiddenCount;
            for (var h = 0; h < HiddenCount; h++)
            {
                sum += _w2[offset + h] * hidden[h];
            }
            outputs[o] = MathF.Tanh(sum);
        }
    }

    public void Reinforce(float[] outputs, float reward)
    {
        if (MathF.Abs(reward) <= 0.0001f) return;
        var scaled = Math.Clamp(reward, -1f, 1f) * Plasticity;
        for (var i = 0; i < Math.Min(OutputCount, outputs.Length); i++)
        {
            _adaptiveBias[i] = Math.Clamp(_adaptiveBias[i] + (outputs[i] * scaled * 0.14f), -1.5f, 1.5f);
        }
    }

    public void Forget(float dt)
    {
        var keep = Math.Clamp(1f - (dt * 0.012f), 0f, 1f);
        for (var i = 0; i < _adaptiveBias.Length; i++) _adaptiveBias[i] *= keep;
    }

    public Brain MutatedClone(Random rng, float sigma)
    {
        var w1 = new float[_w1.Length];
        var b1 = new float[_b1.Length];
        var w2 = new float[_w2.Length];
        var b2 = new float[_b2.Length];
        var adaptive = new float[_adaptiveBias.Length];

        for (var i = 0; i < w1.Length; i++) w1[i] = _w1[i] + (Gaussian(rng) * sigma);
        for (var i = 0; i < w2.Length; i++) w2[i] = _w2[i] + (Gaussian(rng) * sigma);
        for (var i = 0; i < b1.Length; i++) b1[i] = _b1[i] + (Gaussian(rng) * sigma);
        for (var i = 0; i < b2.Length; i++) b2[i] = _b2[i] + (Gaussian(rng) * sigma);
        for (var i = 0; i < adaptive.Length; i++) adaptive[i] = (_adaptiveBias[i] * 0.30f) + (Gaussian(rng) * sigma * 0.20f);

        return new Brain(InputCount, HiddenCount, OutputCount, w1, b1, w2, b2, adaptive, Plasticity);
    }

    private static float NextSigned(Random rng) => (float)((rng.NextDouble() * 2.0) - 1.0);
    private static float Gaussian(Random rng)
    {
        var u1 = 1.0 - rng.NextDouble();
        var u2 = 1.0 - rng.NextDouble();
        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
    }
}

public sealed class AgentTraits
{
    public float MaxSpeed { get; set; }
    public float Acceleration { get; set; }
    public float VisionRange { get; set; }
    public float SocialRange { get; set; }
    public float Metabolism { get; set; }
    public float WaterUse { get; set; }
    public float ReproduceEnergy { get; set; }
    public float MaxAge { get; set; }
    public float Size { get; set; }
    public float MutationSigma { get; set; }
    public float Communication { get; set; }
    public float MemoryStrength { get; set; }
    public float Courage { get; set; }
    public float Curiosity { get; set; }
    public float Cooperation { get; set; }
    public float Lift { get; set; }

    public AgentTraits Clone() => new()
    {
        MaxSpeed = MaxSpeed,
        Acceleration = Acceleration,
        VisionRange = VisionRange,
        SocialRange = SocialRange,
        Metabolism = Metabolism,
        WaterUse = WaterUse,
        ReproduceEnergy = ReproduceEnergy,
        MaxAge = MaxAge,
        Size = Size,
        MutationSigma = MutationSigma,
        Communication = Communication,
        MemoryStrength = MemoryStrength,
        Courage = Courage,
        Curiosity = Curiosity,
        Cooperation = Cooperation,
        Lift = Lift
    };

    public AgentTraits Mutated(Random rng, Species species)
    {
        var t = Clone();
        t.MaxSpeed = Clamp(t.MaxSpeed + Signed(rng, 4f), species == Species.Prey ? 50f : 56f, species == Species.Prey ? 180f : 190f);
        t.Acceleration = Clamp(t.Acceleration + Signed(rng, 6f), 30f, 190f);
        t.VisionRange = Clamp(t.VisionRange + Signed(rng, 10f), 50f, 320f);
        t.SocialRange = Clamp(t.SocialRange + Signed(rng, 8f), 30f, 280f);
        t.Metabolism = Clamp(t.Metabolism + Signed(rng, 0.05f), 0.15f, 2.1f);
        t.WaterUse = Clamp(t.WaterUse + Signed(rng, 0.04f), 0.08f, 1.2f);
        t.ReproduceEnergy = Clamp(t.ReproduceEnergy + Signed(rng, 5f), 35f, 220f);
        t.MaxAge = Clamp(t.MaxAge + Signed(rng, 10f), 60f, 380f);
        t.Size = Clamp(t.Size + Signed(rng, 0.35f), 4.0f, 13.5f);
        t.MutationSigma = Clamp(t.MutationSigma + Signed(rng, 0.01f), 0.02f, 0.20f);
        t.Communication = Clamp(t.Communication + Signed(rng, 0.05f), 0.05f, 1.0f);
        t.MemoryStrength = Clamp(t.MemoryStrength + Signed(rng, 0.05f), 0.05f, 1.0f);
        t.Courage = Clamp(t.Courage + Signed(rng, 0.05f), 0.05f, 1.0f);
        t.Curiosity = Clamp(t.Curiosity + Signed(rng, 0.05f), 0.05f, 1.0f);
        t.Cooperation = Clamp(t.Cooperation + Signed(rng, 0.05f), 0.05f, 1.0f);
        t.Lift = Clamp(t.Lift + Signed(rng, 0.3f), 0.4f, 9.0f);
        return t;
    }

    public static AgentTraits CreateDefault(Species species)
        => species == Species.Prey
            ? new AgentTraits
            {
                MaxSpeed = 105f,
                Acceleration = 92f,
                VisionRange = 135f,
                SocialRange = 96f,
                Metabolism = 0.34f,
                WaterUse = 0.22f,
                ReproduceEnergy = 72f,
                MaxAge = 180f,
                Size = 6.4f,
                MutationSigma = 0.08f,
                Communication = 0.72f,
                MemoryStrength = 0.78f,
                Courage = 0.34f,
                Curiosity = 0.76f,
                Cooperation = 0.76f,
                Lift = 3.0f
            }
            : new AgentTraits
            {
                MaxSpeed = 120f,
                Acceleration = 106f,
                VisionRange = 155f,
                SocialRange = 84f,
                Metabolism = 0.48f,
                WaterUse = 0.30f,
                ReproduceEnergy = 96f,
                MaxAge = 205f,
                Size = 7.8f,
                MutationSigma = 0.08f,
                Communication = 0.54f,
                MemoryStrength = 0.70f,
                Courage = 0.72f,
                Curiosity = 0.55f,
                Cooperation = 0.42f,
                Lift = 4.0f
            };

    private static float Signed(Random rng, float scale) => ((float)rng.NextDouble() * 2f - 1f) * scale;
    private static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
}

public sealed class CultureMemory
{
    public Vec2 FoodDirection { get; private set; } = Vec2.Zero;
    public Vec2 DangerDirection { get; private set; } = Vec2.Zero;
    public Vec2 WaterDirection { get; private set; } = Vec2.Zero;
    public Vec2 HuntDirection { get; private set; } = Vec2.Zero;

    public float FoodStrength { get; private set; }
    public float DangerStrength { get; private set; }
    public float WaterStrength { get; private set; }
    public float HuntStrength { get; private set; }

    public float Cooperation { get; private set; }
    public float Aggression { get; private set; }
    public float Vigilance { get; private set; }
    public int Broadcasts { get; private set; }

    public void Reset()
    {
        FoodDirection = Vec2.Zero;
        DangerDirection = Vec2.Zero;
        WaterDirection = Vec2.Zero;
        HuntDirection = Vec2.Zero;
        FoodStrength = 0f;
        DangerStrength = 0f;
        WaterStrength = 0f;
        HuntStrength = 0f;
        Cooperation = 0f;
        Aggression = 0f;
        Vigilance = 0f;
        Broadcasts = 0;
    }

    public void Decay(float dt)
    {
        var keep = Math.Clamp(1f - (dt * 0.018f), 0f, 1f);
        FoodStrength *= keep;
        DangerStrength *= keep;
        WaterStrength *= keep;
        HuntStrength *= keep;
        Cooperation *= keep;
        Aggression *= keep;
        Vigilance *= keep;
    }

    public void Learn(KnowledgeKind kind, Vec2 direction, float strength, AgentTraits traits)
    {
        direction = direction.LengthSquared <= 0.0001f ? Vec2.Zero : direction.Normalized();
        var blend = 0.12f + (Math.Clamp(strength, 0f, 1f) * 0.24f);

        switch (kind)
        {
            case KnowledgeKind.Food:
                FoodDirection = BlendDirection(FoodDirection, direction, blend);
                FoodStrength = Math.Clamp((FoodStrength * 0.86f) + strength, 0f, 1f);
                break;
            case KnowledgeKind.Danger:
                DangerDirection = BlendDirection(DangerDirection, direction, blend);
                DangerStrength = Math.Clamp((DangerStrength * 0.88f) + strength, 0f, 1f);
                break;
            case KnowledgeKind.Water:
                WaterDirection = BlendDirection(WaterDirection, direction, blend);
                WaterStrength = Math.Clamp((WaterStrength * 0.88f) + strength, 0f, 1f);
                break;
            case KnowledgeKind.Hunt:
                HuntDirection = BlendDirection(HuntDirection, direction, blend);
                HuntStrength = Math.Clamp((HuntStrength * 0.88f) + strength, 0f, 1f);
                break;
        }

        Cooperation = Math.Clamp((Cooperation * 0.88f) + (traits.Cooperation * 0.12f), 0f, 1f);
        Aggression = Math.Clamp((Aggression * 0.90f) + ((traits.Courage + (kind == KnowledgeKind.Hunt ? 0.30f : 0f)) * 0.10f), 0f, 1f);
        Vigilance = Math.Clamp((Vigilance * 0.90f) + ((traits.Courage + (kind == KnowledgeKind.Danger ? 0.36f : 0f)) * 0.12f), 0f, 1f);
    }

    public void RegisterBroadcast() => Broadcasts++;

    private static Vec2 BlendDirection(in Vec2 current, in Vec2 incoming, float blend)
    {
        if (incoming.LengthSquared <= 0.0001f) return current;
        if (current.LengthSquared <= 0.0001f) return incoming;
        return Vec2.Lerp(current, incoming, blend).Normalized();
    }
}

public sealed class WaterSource
{
    public Vec2 Position;
    public float Radius;
    public float Phase;
}

public sealed class Plant
{
    public Vec2 Position;
    public float Radius;
    public float FruitTimer;
    public float SpreadTimer;
    public float Elevation;
}

public sealed class Food
{
    public Vec2 Position;
    public float Radius;
    public float Nutrition;
    public float Age;
    public float LifeSpan;
    public bool IsCarrion;
    public bool IsDead;
}

public sealed class Pulse
{
    public Vec2 Position;
    public float Radius;
    public float Age;
    public float LifeSpan;
    public Color Color;
    public KnowledgeKind Kind;
    public bool IsDead;
}

public sealed class Agent
{
    public int Id;
    public Species Species;
    public Vec2 Position;
    public Vec2 Velocity;
    public float Energy;
    public float Hydration;
    public float Age;
    public int Generation;
    public Brain Brain = null!;
    public AgentTraits Traits = null!;
    public float ReproductionCooldown;
    public float BroadcastCooldown;
    public float Elevation;
    public float Radius;
    public float WanderPhase;
    public float CooperationBias;
    public float CuriosityBias;
    public float CourageBias;
    public float AggressionBias;
    public Vec2 FoodMemory;
    public Vec2 DangerMemory;
    public Vec2 WaterMemory;
    public Vec2 HuntMemory;
    public float FoodMemoryStrength;
    public float DangerMemoryStrength;
    public float WaterMemoryStrength;
    public float HuntMemoryStrength;
    public float[] Inputs = new float[22];
    public float[] Hidden = new float[24];
    public float[] Outputs = new float[8];
    public int TargetFoodIndex = -1;
    public int TargetWaterIndex = -1;
    public int TargetPreyIndex = -1;
    public KnowledgeKind? BroadcastKind;
    public float BroadcastStrength;
    public bool WantsBroadcast;
    public bool IsDead;
    public string LastStatus = "wandering";

    public void Integrate(World world, Vec2 steering, float sprint, float dt)
    {
        var accel = steering.Limit(1f) * Traits.Acceleration * (1f + (0.65f * sprint));
        var drag = world.TerrainDragAt(Position);
        Velocity = (Velocity * drag) + (accel * dt);
        Velocity = Velocity.Limit(Traits.MaxSpeed * (1f + (0.40f * sprint)));
        Position += Velocity * dt;

        var hitWall = false;
        if (Position.X < Radius)
        {
            Position = new Vec2(Radius, Position.Y);
            Velocity = new Vec2(MathF.Abs(Velocity.X) * 0.45f, Velocity.Y);
            hitWall = true;
        }
        else if (Position.X > world.Width - Radius)
        {
            Position = new Vec2(world.Width - Radius, Position.Y);
            Velocity = new Vec2(-MathF.Abs(Velocity.X) * 0.45f, Velocity.Y);
            hitWall = true;
        }

        if (Position.Y < Radius)
        {
            Position = new Vec2(Position.X, Radius);
            Velocity = new Vec2(Velocity.X, MathF.Abs(Velocity.Y) * 0.45f);
            hitWall = true;
        }
        else if (Position.Y > world.Height - Radius)
        {
            Position = new Vec2(Position.X, world.Height - Radius);
            Velocity = new Vec2(Velocity.X, -MathF.Abs(Velocity.Y) * 0.45f);
            hitWall = true;
        }

        var motionCost = Velocity.Length * 0.0065f;
        var terrainPenalty = 0.05f + (world.TerrainHeightAt(Position) * 0.08f);
        Energy -= ((Traits.Metabolism + motionCost + terrainPenalty) * (1f + (0.60f * sprint))) * dt;
        Hydration -= (Traits.WaterUse * (1f + (0.45f * sprint))) * dt;
        Age += dt;
        ReproductionCooldown = MathF.Max(0f, ReproductionCooldown - dt);
        BroadcastCooldown = MathF.Max(0f, BroadcastCooldown - dt);
        Elevation = Traits.Lift + (MathF.Abs(MathF.Sin((Age * 1.5f) + (Generation * 0.25f))) * (0.45f + (Velocity.Length * 0.010f)));
        Radius = Traits.Size;

        FoodMemoryStrength = MathF.Max(0f, FoodMemoryStrength - (dt * 0.028f));
        DangerMemoryStrength = MathF.Max(0f, DangerMemoryStrength - (dt * 0.032f));
        WaterMemoryStrength = MathF.Max(0f, WaterMemoryStrength - (dt * 0.024f));
        HuntMemoryStrength = MathF.Max(0f, HuntMemoryStrength - (dt * 0.026f));
        Brain.Forget(dt);

        if (hitWall)
        {
            Brain.Reinforce(Outputs, -0.03f);
        }

        if (Energy <= 0f || Hydration <= 0f || Age >= Traits.MaxAge)
        {
            IsDead = true;
        }
    }

    public void Remember(KnowledgeKind kind, Vec2 direction, float strength)
    {
        direction = direction.LengthSquared <= 0.0001f ? Vec2.Zero : direction.Normalized();
        var blend = 0.16f + (Traits.MemoryStrength * 0.30f);
        strength = Math.Clamp(strength, 0f, 1f);

        switch (kind)
        {
            case KnowledgeKind.Food:
                FoodMemory = Blend(FoodMemory, direction, blend);
                FoodMemoryStrength = Math.Clamp((FoodMemoryStrength * 0.84f) + strength, 0f, 1f);
                break;
            case KnowledgeKind.Danger:
                DangerMemory = Blend(DangerMemory, direction, blend);
                DangerMemoryStrength = Math.Clamp((DangerMemoryStrength * 0.88f) + strength, 0f, 1f);
                break;
            case KnowledgeKind.Water:
                WaterMemory = Blend(WaterMemory, direction, blend);
                WaterMemoryStrength = Math.Clamp((WaterMemoryStrength * 0.86f) + strength, 0f, 1f);
                break;
            case KnowledgeKind.Hunt:
                HuntMemory = Blend(HuntMemory, direction, blend);
                HuntMemoryStrength = Math.Clamp((HuntMemoryStrength * 0.86f) + strength, 0f, 1f);
                break;
        }
    }

    public IEnumerable<string> DescribeSelection()
    {
        yield return $"{Species} #{Id}  gen {Generation}";
        yield return $"Energy {Energy,6:F1}   Water {Hydration,6:F1}";
        yield return $"Speed  {Velocity.Length,6:F1}   Age   {Age,6:F1}";
        yield return $"Vision {Traits.VisionRange,6:F1}   Social {Traits.SocialRange,6:F1}";
        yield return $"Mut σ  {Traits.MutationSigma,6:F2}   Lift   {Traits.Lift,6:F1}";
        yield return $"Coop   {CooperationBias,6:F2}   Courage {CourageBias,6:F2}";
        yield return $"Curio  {CuriosityBias,6:F2}   Aggro   {AggressionBias,6:F2}";
        yield return $"State  {LastStatus}";
    }

    private static Vec2 Blend(in Vec2 current, in Vec2 incoming, float t)
    {
        if (incoming.LengthSquared <= 0.0001f) return current;
        if (current.LengthSquared <= 0.0001f) return incoming;
        return Vec2.Lerp(current, incoming, t).Normalized();
    }
}

public readonly struct AgentSnapshot
{
    public readonly Vec2 Position;
    public readonly float Radius;
    public readonly bool Alive;

    public AgentSnapshot(Vec2 position, float radius, bool alive)
    {
        Position = position;
        Radius = radius;
        Alive = alive;
    }
}

public readonly struct FoodSnapshot
{
    public readonly Vec2 Position;
    public readonly float Radius;
    public readonly bool Alive;
    public readonly bool IsCarrion;

    public FoodSnapshot(Vec2 position, float radius, bool alive, bool isCarrion)
    {
        Position = position;
        Radius = radius;
        Alive = alive;
        IsCarrion = isCarrion;
    }
}

public readonly struct WaterSnapshot
{
    public readonly Vec2 Position;
    public readonly float Radius;

    public WaterSnapshot(Vec2 position, float radius)
    {
        Position = position;
        Radius = radius;
    }
}

public readonly struct DrawItem
{
    public readonly float X;
    public readonly float Y;
    public readonly float Radius;
    public readonly float RadiusY;
    public readonly float Elevation;
    public readonly float HeadingX;
    public readonly float HeadingY;
    public readonly float SortY;
    public readonly float Alpha;
    public readonly Color Color;
    public readonly int Type;

    public DrawItem(float x, float y, float radius, float radiusY, float elevation, float headingX, float headingY, float sortY, float alpha, Color color, int type)
    {
        X = x;
        Y = y;
        Radius = radius;
        RadiusY = radiusY;
        Elevation = elevation;
        HeadingX = headingX;
        HeadingY = headingY;
        SortY = sortY;
        Alpha = alpha;
        Color = color;
        Type = type;
    }
}

public readonly struct SelectionInfo
{
    public readonly SelectionKind Kind;
    public readonly Agent? Agent;
    public readonly Plant? Plant;
    public readonly Food? Food;
    public readonly WaterSource? Water;

    public SelectionInfo(Agent? agent)
    {
        Kind = agent is null ? SelectionKind.None : SelectionKind.Agent;
        Agent = agent;
        Plant = null;
        Food = null;
        Water = null;
    }

    public SelectionInfo(Plant? plant)
    {
        Kind = plant is null ? SelectionKind.None : SelectionKind.Plant;
        Agent = null;
        Plant = plant;
        Food = null;
        Water = null;
    }

    public SelectionInfo(Food? food)
    {
        Kind = food is null ? SelectionKind.None : SelectionKind.Food;
        Agent = null;
        Plant = null;
        Food = food;
        Water = null;
    }

    public SelectionInfo(WaterSource? water)
    {
        Kind = water is null ? SelectionKind.None : SelectionKind.Water;
        Agent = null;
        Plant = null;
        Food = null;
        Water = water;
    }
}

public sealed class World
{
    private const float FixedStep = 1f / 120f;
    private const int MaxStepsPerAdvance = 64;

    private readonly Random _rng = new(12345);
    private readonly object _spawnLock = new();
    private readonly List<Agent> _newPrey = new();
    private readonly List<Agent> _newPredators = new();
    private readonly List<Pulse> _newPulses = new();
    private AgentSnapshot[] _preySnapshots = Array.Empty<AgentSnapshot>();
    private AgentSnapshot[] _predatorSnapshots = Array.Empty<AgentSnapshot>();
    private FoodSnapshot[] _foodSnapshots = Array.Empty<FoodSnapshot>();
    private WaterSnapshot[] _waterSnapshots = Array.Empty<WaterSnapshot>();
    private float _stepAccumulator;
    private int _nextId = 1;
    private float _historyTimer;

    public int Width { get; private set; }
    public int Height { get; private set; }
    public int WorkerThreads { get; private set; }
    public float SimulatedTime { get; private set; }
    public long StepCounter { get; private set; }
    public CultureMemory PreyCulture { get; } = new();
    public CultureMemory PredatorCulture { get; } = new();

    public List<Agent> Preys { get; } = new();
    public List<Agent> Predators { get; } = new();
    public List<Plant> Plants { get; } = new();
    public List<Food> Foods { get; } = new();
    public List<WaterSource> Waters { get; } = new();
    public List<Pulse> Pulses { get; } = new();
    public List<(float Time, int Prey, int Predator, int Plants, int Food)> History { get; } = new();
    public SelectionInfo Selection { get; private set; }

    public int MaxFood => 520;
    public int MaxPlants => 180;
    public int MaxVisiblePulses => 260;

    public World(int width, int height, int workerThreads)
    {
        Width = width;
        Height = height;
        WorkerThreads = Math.Max(1, workerThreads);
        Reset();
    }

    public void SetWorkerCount(int value)
    {
        WorkerThreads = Math.Max(1, value);
    }

    public void Resize(int width, int height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);

        foreach (var prey in Preys) ClampAgent(prey);
        foreach (var predator in Predators) ClampAgent(predator);
        foreach (var plant in Plants) plant.Position = ClampToWorld(plant.Position, plant.Radius + 4f);
        foreach (var food in Foods) food.Position = ClampToWorld(food.Position, food.Radius + 4f);
        foreach (var water in Waters) water.Position = ClampToWorld(water.Position, water.Radius + 10f);
    }

    public void Reset()
    {
        SimulatedTime = 0f;
        StepCounter = 0;
        _stepAccumulator = 0f;
        _historyTimer = 0f;
        _nextId = 1;
        Selection = new SelectionInfo((Agent?)null);
        PreyCulture.Reset();
        PredatorCulture.Reset();
        Preys.Clear();
        Predators.Clear();
        Plants.Clear();
        Foods.Clear();
        Waters.Clear();
        Pulses.Clear();
        History.Clear();
        _newPrey.Clear();
        _newPredators.Clear();
        _newPulses.Clear();

        for (var i = 0; i < 5; i++)
        {
            Waters.Add(new WaterSource
            {
                Position = RandomPosition(70f),
                Radius = 35f + (float)(_rng.NextDouble() * 48.0),
                Phase = (float)(_rng.NextDouble() * Math.PI * 2.0)
            });
        }

        for (var i = 0; i < 120; i++)
        {
            Plants.Add(CreatePlant(RandomPosition(18f)));
        }

        for (var i = 0; i < 220; i++)
        {
            Foods.Add(CreateFood(RandomPosition(12f), false));
        }

        for (var i = 0; i < 180; i++)
        {
            Preys.Add(CreateAgent(Species.Prey, RandomPosition(10f), 1));
        }

        for (var i = 0; i < 20; i++)
        {
            Predators.Add(CreateAgent(Species.Predator, RandomPosition(10f), 1));
        }
    }

    public void Advance(float realDt, float speed)
    {
        speed = Math.Clamp(speed, 0.05f, 12f);
        _stepAccumulator += realDt * speed;
        var steps = 0;
        while (_stepAccumulator >= FixedStep && steps < MaxStepsPerAdvance)
        {
            Step(FixedStep);
            _stepAccumulator -= FixedStep;
            steps++;
        }

        if (steps >= MaxStepsPerAdvance)
        {
            _stepAccumulator = 0f;
        }
    }

    public float TerrainHeightAt(in Vec2 p)
    {
        var nx = p.X / Math.Max(1f, Width);
        var ny = p.Y / Math.Max(1f, Height);
        var h1 = 0.5f + (0.5f * MathF.Sin((nx * 7.2f) + (ny * 3.4f)));
        var h2 = 0.5f + (0.5f * MathF.Sin((nx * 15.1f) - (ny * 9.7f)));
        return (h1 * 0.62f) + (h2 * 0.38f);
    }

    public float TerrainDragAt(in Vec2 p)
    {
        var height = TerrainHeightAt(p);
        return 0.90f - (height * 0.04f);
    }

    public Vec2 ClampToWorld(in Vec2 value, float margin)
    {
        return new Vec2(
            Math.Clamp(value.X, margin, Width - margin),
            Math.Clamp(value.Y, margin, Height - margin));
    }

    public void SelectAt(float x, float y, float radius)
    {
        var point = new Vec2(x, y);
        float bestDistance = radius;
        Selection = new SelectionInfo((Agent?)null);

        foreach (var prey in Preys)
        {
            var d = prey.Position.DistanceTo(point);
            if (d < bestDistance)
            {
                bestDistance = d;
                Selection = new SelectionInfo(prey);
            }
        }

        foreach (var predator in Predators)
        {
            var d = predator.Position.DistanceTo(point);
            if (d < bestDistance)
            {
                bestDistance = d;
                Selection = new SelectionInfo(predator);
            }
        }

        foreach (var plant in Plants)
        {
            var d = plant.Position.DistanceTo(point);
            if (d < bestDistance)
            {
                bestDistance = d;
                Selection = new SelectionInfo(plant);
            }
        }

        foreach (var food in Foods)
        {
            var d = food.Position.DistanceTo(point);
            if (d < bestDistance)
            {
                bestDistance = d;
                Selection = new SelectionInfo(food);
            }
        }

        foreach (var water in Waters)
        {
            var d = water.Position.DistanceTo(point);
            if (d < bestDistance)
            {
                bestDistance = d;
                Selection = new SelectionInfo(water);
            }
        }
    }

    public void BuildDrawLists(List<DrawItem> shadows, List<DrawItem> bodies, List<DrawItem> rings)
    {
        shadows.Clear();
        bodies.Clear();
        rings.Clear();

        foreach (var water in Waters)
        {
            shadows.Add(new DrawItem(water.Position.X, water.Position.Y + (water.Radius * 0.18f), water.Radius * 0.92f, water.Radius * 0.42f, 0f, 0f, 0f, water.Position.Y, 0.25f, Color.Black, 1));
            bodies.Add(new DrawItem(water.Position.X, water.Position.Y, water.Radius, water.Radius * 0.82f, 0f, 0.25f, -0.95f, water.Position.Y, 0.64f, Color.FromArgb(90, 170, 235), 0));
            var ripple = 0.72f + (0.08f * MathF.Sin((SimulatedTime * 1.4f) + water.Phase));
            rings.Add(new DrawItem(water.Position.X, water.Position.Y, water.Radius * ripple, water.Radius * ripple * 0.82f, 0f, 0f, 0f, water.Position.Y + 1f, 0.32f, Color.FromArgb(180, 230, 255), 2));
        }

        foreach (var plant in Plants)
        {
            shadows.Add(new DrawItem(plant.Position.X, plant.Position.Y + (plant.Radius * 0.45f), plant.Radius * 1.15f, plant.Radius * 0.46f, 0f, 0f, 0f, plant.Position.Y, 0.24f, Color.Black, 1));
            bodies.Add(new DrawItem(plant.Position.X, plant.Position.Y, plant.Radius, plant.Radius, plant.Elevation, -0.45f, -1.0f, plant.Position.Y + plant.Elevation, 1f, Color.FromArgb(86, 198, 103), 0));
            bodies.Add(new DrawItem(plant.Position.X - (plant.Radius * 0.48f), plant.Position.Y + 1f, plant.Radius * 0.68f, plant.Radius * 0.68f, plant.Elevation * 0.85f, -0.25f, -1.0f, plant.Position.Y + plant.Elevation + 0.1f, 1f, Color.FromArgb(61, 162, 77), 0));
            bodies.Add(new DrawItem(plant.Position.X + (plant.Radius * 0.48f), plant.Position.Y + 1f, plant.Radius * 0.68f, plant.Radius * 0.68f, plant.Elevation * 0.82f, -0.15f, -1.0f, plant.Position.Y + plant.Elevation + 0.2f, 1f, Color.FromArgb(98, 212, 117), 0));
        }

        foreach (var food in Foods)
        {
            if (food.IsDead) continue;
            var color = food.IsCarrion ? Color.FromArgb(190, 132, 90) : Color.FromArgb(230, 220, 110);
            shadows.Add(new DrawItem(food.Position.X, food.Position.Y + (food.Radius * 0.30f), food.Radius * 1.1f, food.Radius * 0.46f, 0f, 0f, 0f, food.Position.Y, 0.16f, Color.Black, 1));
            bodies.Add(new DrawItem(food.Position.X, food.Position.Y, food.Radius, food.Radius, 1.0f, -0.35f, -1.0f, food.Position.Y + 1f, 0.95f, color, 0));
        }

        foreach (var prey in Preys)
        {
            if (prey.IsDead) continue;
            var heading = prey.Velocity.LengthSquared <= 0.0001f ? Vec2.FromAngle(prey.WanderPhase) : prey.Velocity.Normalized();
            shadows.Add(new DrawItem(prey.Position.X, prey.Position.Y + (prey.Radius * 0.40f), prey.Radius * 1.18f, prey.Radius * 0.48f, 0f, 0f, 0f, prey.Position.Y, 0.28f, Color.Black, 1));
            bodies.Add(new DrawItem(prey.Position.X, prey.Position.Y, prey.Radius, prey.Radius, prey.Elevation, heading.X, heading.Y, prey.Position.Y + prey.Elevation, 1f, Color.FromArgb(96, 214, 255), 0));
        }

        foreach (var predator in Predators)
        {
            if (predator.IsDead) continue;
            var heading = predator.Velocity.LengthSquared <= 0.0001f ? Vec2.FromAngle(predator.WanderPhase) : predator.Velocity.Normalized();
            shadows.Add(new DrawItem(predator.Position.X, predator.Position.Y + (predator.Radius * 0.42f), predator.Radius * 1.24f, predator.Radius * 0.50f, 0f, 0f, 0f, predator.Position.Y, 0.34f, Color.Black, 1));
            bodies.Add(new DrawItem(predator.Position.X, predator.Position.Y, predator.Radius, predator.Radius, predator.Elevation, heading.X, heading.Y, predator.Position.Y + predator.Elevation, 1f, Color.FromArgb(255, 128, 102), 0));
        }

        foreach (var pulse in Pulses)
        {
            if (pulse.IsDead) continue;
            rings.Add(new DrawItem(pulse.Position.X, pulse.Position.Y, pulse.Radius, pulse.Radius, 0f, 0f, 0f, pulse.Position.Y + 2f, MathF.Max(0f, 1f - (pulse.Age / pulse.LifeSpan)), pulse.Color, 2));
        }

        switch (Selection.Kind)
        {
            case SelectionKind.Agent when Selection.Agent is not null && !Selection.Agent.IsDead:
                var agent = Selection.Agent;
                rings.Add(new DrawItem(agent.Position.X, agent.Position.Y, agent.Radius + 8f, agent.Radius + 8f, agent.Elevation, 0f, 0f, agent.Position.Y + agent.Elevation + 5f, 0.86f, Color.White, 2));
                rings.Add(new DrawItem(agent.Position.X, agent.Position.Y, agent.Traits.VisionRange, agent.Traits.VisionRange, 0f, 0f, 0f, agent.Position.Y + 3f, 0.14f, Color.White, 2));
                break;
            case SelectionKind.Water when Selection.Water is not null:
                rings.Add(new DrawItem(Selection.Water.Position.X, Selection.Water.Position.Y, Selection.Water.Radius + 8f, (Selection.Water.Radius + 8f) * 0.82f, 0f, 0f, 0f, Selection.Water.Position.Y + 3f, 0.86f, Color.White, 2));
                break;
            case SelectionKind.Plant when Selection.Plant is not null:
                rings.Add(new DrawItem(Selection.Plant.Position.X, Selection.Plant.Position.Y, Selection.Plant.Radius + 8f, Selection.Plant.Radius + 8f, Selection.Plant.Elevation, 0f, 0f, Selection.Plant.Position.Y + 3f, 0.86f, Color.White, 2));
                break;
            case SelectionKind.Food when Selection.Food is not null:
                rings.Add(new DrawItem(Selection.Food.Position.X, Selection.Food.Position.Y, Selection.Food.Radius + 8f, Selection.Food.Radius + 8f, 0f, 0f, 0f, Selection.Food.Position.Y + 3f, 0.86f, Color.White, 2));
                break;
        }

        shadows.Sort(static (a, b) => a.SortY.CompareTo(b.SortY));
        bodies.Sort(static (a, b) => a.SortY.CompareTo(b.SortY));
        rings.Sort(static (a, b) => a.SortY.CompareTo(b.SortY));
    }

    private void Step(float dt)
    {
        SimulatedTime += dt;
        StepCounter++;
        _historyTimer += dt;

        PreyCulture.Decay(dt);
        PredatorCulture.Decay(dt);

        foreach (var water in Waters)
        {
            water.Phase += dt * 1.2f;
        }

        for (var i = 0; i < Pulses.Count; i++)
        {
            var pulse = Pulses[i];
            pulse.Age += dt;
            pulse.Radius += 44f * dt;
            if (pulse.Age >= pulse.LifeSpan) pulse.IsDead = true;
        }

        for (var i = 0; i < Foods.Count; i++)
        {
            var food = Foods[i];
            food.Age += dt;
            if (food.Age >= food.LifeSpan) food.IsDead = true;
        }

        for (var i = 0; i < Plants.Count; i++)
        {
            var plant = Plants[i];
            plant.FruitTimer -= dt;
            plant.SpreadTimer -= dt;

            if (plant.FruitTimer <= 0f && Foods.Count < MaxFood)
            {
                plant.FruitTimer = 1.8f + (float)(_rng.NextDouble() * 3.4);
                var angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
                var distance = 8f + (float)(_rng.NextDouble() * 12.0);
                Foods.Add(CreateFood(ClampToWorld(plant.Position + (Vec2.FromAngle(angle) * distance), 8f), false));
            }

            if (plant.SpreadTimer <= 0f && Plants.Count < MaxPlants)
            {
                plant.SpreadTimer = 14f + (float)(_rng.NextDouble() * 18.0);
                var chance = FindNearestWaterDistance(plant.Position, 160f) < 0 ? 0.18f : 0.45f;
                if ((float)_rng.NextDouble() < chance)
                {
                    var angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
                    var distance = 18f + (float)(_rng.NextDouble() * 52.0);
                    var pos = ClampToWorld(plant.Position + (Vec2.FromAngle(angle) * distance), 16f);
                    if (!IsPlantTooClose(pos, 16f)) Plants.Add(CreatePlant(pos));
                }
            }
        }

        EnsureSnapshots();
        FillSnapshots();

        var options = new ParallelOptions { MaxDegreeOfParallelism = WorkerThreads };
        Parallel.For(0, Preys.Count, options, i => UpdatePrey(Preys[i], i, dt));
        Parallel.For(0, Predators.Count, options, i => UpdatePredator(Predators[i], i, dt));

        ResolveResourceUse(dt);
        ResolvePredation();
        ResolveBroadcasts();
        ResolveReproduction();
        ResolveDeaths();
        Cleanup();

        if (_historyTimer >= 0.50f)
        {
            _historyTimer = 0f;
            History.Add((SimulatedTime, Preys.Count, Predators.Count, Plants.Count, Foods.Count));
            if (History.Count > 260) History.RemoveAt(0);
        }
    }

    private void UpdatePrey(Agent agent, int index, float dt)
    {
        if (agent.IsDead) return;

        agent.TargetFoodIndex = -1;
        agent.TargetWaterIndex = -1;
        agent.TargetPreyIndex = -1;
        agent.WantsBroadcast = false;
        agent.BroadcastKind = null;
        agent.BroadcastStrength = 0f;

        var nearestFood = FindNearestFood(agent.Position, agent.Traits.VisionRange, false, out var foodIndex, out var foodDistance, out var foodDirection);
        var nearestWater = FindNearestWater(agent.Position, agent.Traits.VisionRange, out var waterIndex, out var waterDistance, out var waterDirection);
        var nearestPredator = FindNearestPredator(agent.Position, agent.Traits.VisionRange, out var predatorDistance, out var predatorDirection);
        var nearestAlly = FindNearestAlly(_preySnapshots, index, agent.Position, agent.Traits.SocialRange, out var allyDistance, out var allyDirection);

        if (nearestFood) agent.Remember(KnowledgeKind.Food, foodDirection, 1f - Math.Clamp(foodDistance / agent.Traits.VisionRange, 0f, 1f));
        if (nearestWater) agent.Remember(KnowledgeKind.Water, waterDirection, 1f - Math.Clamp(waterDistance / agent.Traits.VisionRange, 0f, 1f));
        if (nearestPredator) agent.Remember(KnowledgeKind.Danger, predatorDirection, 1f - Math.Clamp(predatorDistance / agent.Traits.VisionRange, 0f, 1f));

        FillCommonInputs(agent);
        agent.Inputs[4] = nearestFood ? foodDirection.X : 0f;
        agent.Inputs[5] = nearestFood ? foodDirection.Y : 0f;
        agent.Inputs[6] = nearestFood ? 1f - Math.Clamp(foodDistance / agent.Traits.VisionRange, 0f, 1f) : 0f;
        agent.Inputs[7] = nearestWater ? waterDirection.X : 0f;
        agent.Inputs[8] = nearestWater ? waterDirection.Y : 0f;
        agent.Inputs[9] = nearestWater ? 1f - Math.Clamp(waterDistance / agent.Traits.VisionRange, 0f, 1f) : 0f;
        agent.Inputs[10] = nearestPredator ? predatorDirection.X : 0f;
        agent.Inputs[11] = nearestPredator ? predatorDirection.Y : 0f;
        agent.Inputs[12] = nearestPredator ? 1f - Math.Clamp(predatorDistance / agent.Traits.VisionRange, 0f, 1f) : 0f;
        agent.Inputs[13] = nearestAlly ? allyDirection.X : 0f;
        agent.Inputs[14] = nearestAlly ? allyDirection.Y : 0f;
        agent.Inputs[15] = nearestAlly ? 1f - Math.Clamp(allyDistance / agent.Traits.SocialRange, 0f, 1f) : 0f;
        agent.Inputs[16] = PreyCulture.FoodStrength;
        agent.Inputs[17] = PreyCulture.DangerStrength;
        agent.Inputs[18] = agent.FoodMemoryStrength - agent.DangerMemoryStrength;
        agent.Inputs[19] = agent.WaterMemoryStrength;
        agent.Inputs[20] = (float)Math.Sin(agent.WanderPhase + (SimulatedTime * 0.7f));
        agent.Inputs[21] = (float)Math.Cos(agent.WanderPhase - (SimulatedTime * 0.5f));

        agent.Brain.Evaluate(agent.Inputs, agent.Hidden, agent.Outputs);

        var steer = Vec2.Zero;
        var wander = Vec2.FromAngle(agent.WanderPhase + (SimulatedTime * (0.45f + (agent.CuriosityBias * 0.55f))));
        steer += wander * (0.22f + (agent.CuriosityBias * 0.40f));
        if (nearestFood) steer += foodDirection * (0.75f + MathF.Max(0f, -agent.Outputs[5]) * 0.35f);
        if (nearestWater) steer += waterDirection * (0.65f + ((agent.Hydration < 55f) ? 0.55f : 0.0f));
        if (nearestPredator) steer -= predatorDirection * (1.25f + ((1f - agent.CourageBias) * 0.90f));
        if (nearestAlly) steer += allyDirection * (0.20f + (agent.CooperationBias * 0.38f));
        if (agent.FoodMemoryStrength > 0.02f) steer += agent.FoodMemory * (0.22f + (agent.FoodMemoryStrength * 0.48f));
        if (agent.WaterMemoryStrength > 0.02f) steer += agent.WaterMemory * (0.18f + (agent.WaterMemoryStrength * 0.46f));
        if (agent.DangerMemoryStrength > 0.02f) steer -= agent.DangerMemory * (0.28f + (agent.DangerMemoryStrength * 0.62f));
        if (PreyCulture.FoodStrength > 0.02f) steer += PreyCulture.FoodDirection * (0.16f + (PreyCulture.FoodStrength * 0.34f));
        if (PreyCulture.DangerStrength > 0.02f) steer -= PreyCulture.DangerDirection * (0.20f + (PreyCulture.DangerStrength * 0.42f));
        steer += new Vec2(agent.Outputs[0], agent.Outputs[1]) * 0.50f;

        var sprint = Math.Clamp((agent.Outputs[2] + 1f) * 0.5f, 0f, 1f);
        if (nearestPredator) sprint = MathF.Max(sprint, 0.65f);
        if (agent.Hydration < 28f || agent.Energy < 24f) sprint *= 0.65f;

        agent.LastStatus = nearestPredator ? "evading danger" : nearestFood ? "foraging" : nearestWater ? "seeking water" : "wandering";
        agent.WanderPhase += dt * (0.35f + (agent.CuriosityBias * 0.20f));
        agent.TargetFoodIndex = foodIndex;
        agent.TargetWaterIndex = waterIndex;

        if (nearestFood && agent.BroadcastCooldown <= 0f && agent.Traits.Communication > 0.18f && agent.Outputs[3] > 0.28f)
        {
            agent.WantsBroadcast = true;
            agent.BroadcastKind = KnowledgeKind.Food;
            agent.BroadcastStrength = 0.25f + (agent.Traits.Communication * 0.75f);
        }

        if (nearestPredator && agent.BroadcastCooldown <= 0f && agent.Outputs[4] > -0.15f)
        {
            agent.WantsBroadcast = true;
            agent.BroadcastKind = KnowledgeKind.Danger;
            agent.BroadcastStrength = 0.45f + ((1f - agent.CourageBias) * 0.55f);
        }

        agent.Integrate(this, steer, sprint, dt);
    }

    private void UpdatePredator(Agent agent, int index, float dt)
    {
        if (agent.IsDead) return;

        agent.TargetFoodIndex = -1;
        agent.TargetWaterIndex = -1;
        agent.TargetPreyIndex = -1;
        agent.WantsBroadcast = false;
        agent.BroadcastKind = null;
        agent.BroadcastStrength = 0f;

        var nearestCarrion = FindNearestFood(agent.Position, agent.Traits.VisionRange, true, out var foodIndex, out var foodDistance, out var foodDirection);
        var nearestWater = FindNearestWater(agent.Position, agent.Traits.VisionRange, out var waterIndex, out var waterDistance, out var waterDirection);
        var nearestPrey = FindNearestPrey(agent.Position, agent.Traits.VisionRange, out var preyIndex, out var preyDistance, out var preyDirection);
        var nearestAlly = FindNearestAlly(_predatorSnapshots, index, agent.Position, agent.Traits.SocialRange, out var allyDistance, out var allyDirection);

        if (nearestCarrion) agent.Remember(KnowledgeKind.Food, foodDirection, 1f - Math.Clamp(foodDistance / agent.Traits.VisionRange, 0f, 1f));
        if (nearestWater) agent.Remember(KnowledgeKind.Water, waterDirection, 1f - Math.Clamp(waterDistance / agent.Traits.VisionRange, 0f, 1f));
        if (nearestPrey) agent.Remember(KnowledgeKind.Hunt, preyDirection, 1f - Math.Clamp(preyDistance / agent.Traits.VisionRange, 0f, 1f));

        FillCommonInputs(agent);
        agent.Inputs[4] = nearestPrey ? preyDirection.X : 0f;
        agent.Inputs[5] = nearestPrey ? preyDirection.Y : 0f;
        agent.Inputs[6] = nearestPrey ? 1f - Math.Clamp(preyDistance / agent.Traits.VisionRange, 0f, 1f) : 0f;
        agent.Inputs[7] = nearestWater ? waterDirection.X : 0f;
        agent.Inputs[8] = nearestWater ? waterDirection.Y : 0f;
        agent.Inputs[9] = nearestWater ? 1f - Math.Clamp(waterDistance / agent.Traits.VisionRange, 0f, 1f) : 0f;
        agent.Inputs[10] = nearestCarrion ? foodDirection.X : 0f;
        agent.Inputs[11] = nearestCarrion ? foodDirection.Y : 0f;
        agent.Inputs[12] = nearestCarrion ? 1f - Math.Clamp(foodDistance / agent.Traits.VisionRange, 0f, 1f) : 0f;
        agent.Inputs[13] = nearestAlly ? allyDirection.X : 0f;
        agent.Inputs[14] = nearestAlly ? allyDirection.Y : 0f;
        agent.Inputs[15] = nearestAlly ? 1f - Math.Clamp(allyDistance / agent.Traits.SocialRange, 0f, 1f) : 0f;
        agent.Inputs[16] = PredatorCulture.HuntStrength;
        agent.Inputs[17] = PredatorCulture.WaterStrength;
        agent.Inputs[18] = agent.HuntMemoryStrength;
        agent.Inputs[19] = agent.WaterMemoryStrength;
        agent.Inputs[20] = (float)Math.Sin(agent.WanderPhase + (SimulatedTime * 0.8f));
        agent.Inputs[21] = (float)Math.Cos(agent.WanderPhase - (SimulatedTime * 0.45f));

        agent.Brain.Evaluate(agent.Inputs, agent.Hidden, agent.Outputs);

        var steer = Vec2.Zero;
        var wander = Vec2.FromAngle(agent.WanderPhase + (SimulatedTime * (0.35f + (agent.CuriosityBias * 0.35f))));
        steer += wander * (0.18f + (agent.CuriosityBias * 0.24f));
        if (nearestPrey) steer += preyDirection * (0.90f + (agent.AggressionBias * 0.50f));
        if (nearestCarrion && !nearestPrey) steer += foodDirection * 0.46f;
        if (nearestWater) steer += waterDirection * (0.55f + ((agent.Hydration < 52f) ? 0.55f : 0.0f));
        if (nearestAlly) steer -= allyDirection * 0.08f;
        if (agent.HuntMemoryStrength > 0.02f) steer += agent.HuntMemory * (0.18f + (agent.HuntMemoryStrength * 0.52f));
        if (PredatorCulture.HuntStrength > 0.02f) steer += PredatorCulture.HuntDirection * (0.14f + (PredatorCulture.HuntStrength * 0.34f));
        if (PredatorCulture.WaterStrength > 0.02f) steer += PredatorCulture.WaterDirection * (0.10f + (PredatorCulture.WaterStrength * 0.28f));
        steer += new Vec2(agent.Outputs[0], agent.Outputs[1]) * 0.40f;

        var sprint = Math.Clamp((agent.Outputs[2] + 1f) * 0.5f, 0f, 1f);
        if (nearestPrey) sprint = MathF.Max(sprint, 0.58f);
        if (agent.Hydration < 28f) sprint *= 0.62f;

        agent.LastStatus = nearestPrey ? "hunting" : nearestWater ? "heading to water" : nearestCarrion ? "scavenging" : "patrolling";
        agent.WanderPhase += dt * (0.28f + (agent.CuriosityBias * 0.16f));
        agent.TargetPreyIndex = preyIndex;
        agent.TargetFoodIndex = foodIndex;
        agent.TargetWaterIndex = waterIndex;

        if (nearestPrey && agent.BroadcastCooldown <= 0f && agent.Traits.Communication > 0.18f && agent.Outputs[3] > -0.10f)
        {
            agent.WantsBroadcast = true;
            agent.BroadcastKind = KnowledgeKind.Hunt;
            agent.BroadcastStrength = 0.25f + (agent.Traits.Communication * 0.75f);
        }

        if (nearestWater && agent.BroadcastCooldown <= 0f && agent.Hydration < 40f && agent.Outputs[4] > 0.25f)
        {
            agent.WantsBroadcast = true;
            agent.BroadcastKind = KnowledgeKind.Water;
            agent.BroadcastStrength = 0.22f + (agent.Traits.Communication * 0.65f);
        }

        agent.Integrate(this, steer, sprint, dt);
    }

    private void FillCommonInputs(Agent agent)
    {
        Array.Clear(agent.Inputs);
        agent.Inputs[0] = Math.Clamp(agent.Energy / agent.Traits.ReproduceEnergy, 0f, 2f) - 1f;
        agent.Inputs[1] = Math.Clamp(agent.Hydration / 100f, 0f, 1f) * 2f - 1f;
        agent.Inputs[2] = Math.Clamp(agent.Age / agent.Traits.MaxAge, 0f, 1f) * 2f - 1f;
        agent.Inputs[3] = Math.Clamp(agent.Velocity.Length / agent.Traits.MaxSpeed, 0f, 1f) * 2f - 1f;
    }

    private void ResolveResourceUse(float dt)
    {
        foreach (var prey in Preys)
        {
            if (prey.IsDead) continue;

            if (prey.TargetFoodIndex >= 0 && prey.TargetFoodIndex < Foods.Count)
            {
                var food = Foods[prey.TargetFoodIndex];
                if (!food.IsDead)
                {
                    var distance = prey.Position.DistanceTo(food.Position);
                    if (distance <= prey.Radius + food.Radius + 2f)
                    {
                        food.IsDead = true;
                        prey.Energy += food.Nutrition;
                        prey.Hydration = MathF.Min(100f, prey.Hydration + (food.IsCarrion ? 1.5f : 4.5f));
                        prey.Brain.Reinforce(prey.Outputs, 0.34f);
                        PreyCulture.Learn(KnowledgeKind.Food, food.Position - prey.Position, 0.22f, prey.Traits);
                    }
                }
            }

            if (prey.TargetWaterIndex >= 0 && prey.TargetWaterIndex < Waters.Count)
            {
                var water = Waters[prey.TargetWaterIndex];
                var distance = prey.Position.DistanceTo(water.Position);
                if (distance <= water.Radius * 0.95f)
                {
                    prey.Hydration = MathF.Min(100f, prey.Hydration + (28f * dt));
                    prey.Brain.Reinforce(prey.Outputs, 0.10f);
                    PreyCulture.Learn(KnowledgeKind.Water, water.Position - prey.Position, 0.08f, prey.Traits);
                }
            }
        }

        foreach (var predator in Predators)
        {
            if (predator.IsDead) continue;

            if (predator.TargetFoodIndex >= 0 && predator.TargetFoodIndex < Foods.Count)
            {
                var food = Foods[predator.TargetFoodIndex];
                if (!food.IsDead)
                {
                    var distance = predator.Position.DistanceTo(food.Position);
                    if (distance <= predator.Radius + food.Radius + 2f && food.IsCarrion)
                    {
                        food.IsDead = true;
                        predator.Energy += food.Nutrition * 0.85f;
                        predator.Hydration = MathF.Min(100f, predator.Hydration + 2f);
                        predator.Brain.Reinforce(predator.Outputs, 0.24f);
                    }
                }
            }

            if (predator.TargetWaterIndex >= 0 && predator.TargetWaterIndex < Waters.Count)
            {
                var water = Waters[predator.TargetWaterIndex];
                var distance = predator.Position.DistanceTo(water.Position);
                if (distance <= water.Radius * 0.95f)
                {
                    predator.Hydration = MathF.Min(100f, predator.Hydration + (24f * dt));
                    predator.Brain.Reinforce(predator.Outputs, 0.08f);
                    PredatorCulture.Learn(KnowledgeKind.Water, water.Position - predator.Position, 0.06f, predator.Traits);
                }
            }
        }
    }

    private void ResolvePredation()
    {
        foreach (var predator in Predators)
        {
            if (predator.IsDead || predator.TargetPreyIndex < 0 || predator.TargetPreyIndex >= Preys.Count) continue;
            var prey = Preys[predator.TargetPreyIndex];
            if (prey.IsDead) continue;

            var distance = predator.Position.DistanceTo(prey.Position);
            if (distance <= predator.Radius + prey.Radius + 2.5f)
            {
                prey.IsDead = true;
                predator.Energy += MathF.Max(18f, prey.Energy * 0.75f);
                predator.Hydration = MathF.Min(100f, predator.Hydration + 6f);
                predator.Brain.Reinforce(predator.Outputs, 0.48f);
                PredatorCulture.Learn(KnowledgeKind.Hunt, prey.Position - predator.Position, 0.30f, predator.Traits);
                prey.Brain.Reinforce(prey.Outputs, -0.60f);
            }
        }
    }

    private void ResolveBroadcasts()
    {
        foreach (var prey in Preys)
        {
            if (!prey.WantsBroadcast || prey.BroadcastKind is null || prey.BroadcastCooldown > 0f || prey.IsDead) continue;
            Broadcast(prey, Preys, PreyCulture, prey.BroadcastKind.Value, prey.BroadcastStrength, Color.FromArgb(140, 225, 255));
        }

        foreach (var predator in Predators)
        {
            if (!predator.WantsBroadcast || predator.BroadcastKind is null || predator.BroadcastCooldown > 0f || predator.IsDead) continue;
            Broadcast(predator, Predators, PredatorCulture, predator.BroadcastKind.Value, predator.BroadcastStrength, Color.FromArgb(255, 178, 120));
        }
    }

    private void ResolveReproduction()
    {
        foreach (var prey in Preys)
        {
            if (prey.IsDead) continue;
            if (prey.Energy >= prey.Traits.ReproduceEnergy && prey.ReproductionCooldown <= 0f && (float)_rng.NextDouble() < 0.005f)
            {
                prey.Energy *= 0.58f;
                prey.ReproductionCooldown = 8.0f + (float)(_rng.NextDouble() * 4.0);
                lock (_spawnLock)
                {
                    _newPrey.Add(SpawnChild(prey));
                }
            }
        }

        foreach (var predator in Predators)
        {
            if (predator.IsDead) continue;
            if (predator.Energy >= predator.Traits.ReproduceEnergy && predator.ReproductionCooldown <= 0f && (float)_rng.NextDouble() < 0.0034f)
            {
                predator.Energy *= 0.56f;
                predator.ReproductionCooldown = 10.0f + (float)(_rng.NextDouble() * 6.0);
                lock (_spawnLock)
                {
                    _newPredators.Add(SpawnChild(predator));
                }
            }
        }

        if (_newPrey.Count > 0)
        {
            Preys.AddRange(_newPrey);
            _newPrey.Clear();
        }

        if (_newPredators.Count > 0)
        {
            Predators.AddRange(_newPredators);
            _newPredators.Clear();
        }
    }

    private void ResolveDeaths()
    {
        foreach (var prey in Preys)
        {
            if (prey.IsDead)
            {
                Foods.Add(new Food
                {
                    Position = prey.Position,
                    Radius = Math.Max(3f, prey.Radius * 0.75f),
                    Nutrition = 18f + (prey.Energy * 0.25f),
                    Age = 0f,
                    LifeSpan = 22f,
                    IsCarrion = true,
                    IsDead = false
                });
            }
        }

        foreach (var predator in Predators)
        {
            if (predator.IsDead)
            {
                Foods.Add(new Food
                {
                    Position = predator.Position,
                    Radius = Math.Max(4f, predator.Radius * 0.82f),
                    Nutrition = 24f + (predator.Energy * 0.25f),
                    Age = 0f,
                    LifeSpan = 26f,
                    IsCarrion = true,
                    IsDead = false
                });
            }
        }
    }

    private void Cleanup()
    {
        Preys.RemoveAll(static p => p.IsDead);
        Predators.RemoveAll(static p => p.IsDead);
        Foods.RemoveAll(static f => f.IsDead);
        Pulses.RemoveAll(static p => p.IsDead);
        if (Pulses.Count > MaxVisiblePulses) Pulses.RemoveRange(0, Pulses.Count - MaxVisiblePulses);

        while (Foods.Count < 140)
        {
            Foods.Add(CreateFood(RandomPosition(12f), false));
        }

        if (Selection.Kind == SelectionKind.Agent && Selection.Agent is not null && Selection.Agent.IsDead)
        {
            Selection = new SelectionInfo((Agent?)null);
        }
    }

    private Agent SpawnChild(Agent parent)
    {
        var offset = Vec2.FromAngle((float)(_rng.NextDouble() * Math.PI * 2.0f)) * (parent.Radius + 10f + (float)(_rng.NextDouble() * 10.0));
        var traits = parent.Traits.Mutated(_rng, parent.Species);
        var color = parent.Species == Species.Prey ? Color.FromArgb(96, 214, 255) : Color.FromArgb(255, 128, 102);
        var child = new Agent
        {
            Id = _nextId++,
            Species = parent.Species,
            Position = ClampToWorld(parent.Position + offset, traits.Size + 2f),
            Velocity = Vec2.FromAngle((float)(_rng.NextDouble() * Math.PI * 2.0f)) * (10f + (float)(_rng.NextDouble() * 16.0)),
            Energy = traits.ReproduceEnergy * (parent.Species == Species.Prey ? 0.55f : 0.50f),
            Hydration = 72f + (float)(_rng.NextDouble() * 20f),
            Age = 0f,
            Generation = parent.Generation + 1,
            Brain = parent.Brain.MutatedClone(_rng, traits.MutationSigma),
            Traits = traits,
            ReproductionCooldown = 6f,
            BroadcastCooldown = 0.5f,
            Radius = traits.Size,
            Elevation = traits.Lift,
            WanderPhase = (float)(_rng.NextDouble() * Math.PI * 2.0),
            CooperationBias = Math.Clamp(parent.CooperationBias + Signed(0.06f), 0f, 1f),
            CuriosityBias = Math.Clamp(parent.CuriosityBias + Signed(0.06f), 0f, 1f),
            CourageBias = Math.Clamp(parent.CourageBias + Signed(0.06f), 0f, 1f),
            AggressionBias = Math.Clamp(parent.AggressionBias + Signed(0.06f), 0f, 1f),
            LastStatus = "newborn"
        };

        child.FoodMemory = parent.FoodMemory;
        child.DangerMemory = parent.DangerMemory;
        child.WaterMemory = parent.WaterMemory;
        child.HuntMemory = parent.HuntMemory;
        child.FoodMemoryStrength = parent.FoodMemoryStrength * 0.40f;
        child.DangerMemoryStrength = parent.DangerMemoryStrength * 0.40f;
        child.WaterMemoryStrength = parent.WaterMemoryStrength * 0.40f;
        child.HuntMemoryStrength = parent.HuntMemoryStrength * 0.40f;
        return child;
    }

    private void Broadcast(Agent source, List<Agent> speciesList, CultureMemory culture, KnowledgeKind kind, float strength, Color color)
    {
        culture.RegisterBroadcast();
        culture.Learn(kind, TargetDirectionFromMemory(source, kind), strength * 0.30f, source.Traits);

        _newPulses.Add(new Pulse
        {
            Position = source.Position,
            Radius = source.Traits.SocialRange * 0.18f,
            Age = 0f,
            LifeSpan = 0.7f + (strength * 0.6f),
            Color = color,
            Kind = kind,
            IsDead = false
        });

        var range = source.Traits.SocialRange * (0.70f + (source.Traits.Communication * 0.55f));
        var dir = TargetDirectionFromMemory(source, kind);

        for (var i = 0; i < speciesList.Count; i++)
        {
            var other = speciesList[i];
            if (ReferenceEquals(other, source) || other.IsDead) continue;
            var distance = other.Position.DistanceTo(source.Position);
            if (distance <= range)
            {
                other.Remember(kind, dir, strength * (0.55f + (other.Traits.Communication * 0.30f)));
            }
        }

        source.BroadcastCooldown = 1.0f + (1.2f * (1f - source.Traits.Communication));
        source.Brain.Reinforce(source.Outputs, 0.08f);
        Pulses.AddRange(_newPulses);
        _newPulses.Clear();
    }

    private Vec2 TargetDirectionFromMemory(Agent agent, KnowledgeKind kind)
    {
        return kind switch
        {
            KnowledgeKind.Food => agent.FoodMemory,
            KnowledgeKind.Danger => agent.DangerMemory,
            KnowledgeKind.Water => agent.WaterMemory,
            KnowledgeKind.Hunt => agent.HuntMemory,
            _ => Vec2.Zero
        };
    }

    private void FillSnapshots()
    {
        for (var i = 0; i < Preys.Count; i++) _preySnapshots[i] = new AgentSnapshot(Preys[i].Position, Preys[i].Radius, !Preys[i].IsDead);
        for (var i = 0; i < Predators.Count; i++) _predatorSnapshots[i] = new AgentSnapshot(Predators[i].Position, Predators[i].Radius, !Predators[i].IsDead);
        for (var i = 0; i < Foods.Count; i++) _foodSnapshots[i] = new FoodSnapshot(Foods[i].Position, Foods[i].Radius, !Foods[i].IsDead, Foods[i].IsCarrion);
        for (var i = 0; i < Waters.Count; i++) _waterSnapshots[i] = new WaterSnapshot(Waters[i].Position, Waters[i].Radius);
    }

    private void EnsureSnapshots()
    {
        if (_preySnapshots.Length < Preys.Count) _preySnapshots = new AgentSnapshot[Preys.Count];
        if (_predatorSnapshots.Length < Predators.Count) _predatorSnapshots = new AgentSnapshot[Predators.Count];
        if (_foodSnapshots.Length < Foods.Count) _foodSnapshots = new FoodSnapshot[Foods.Count];
        if (_waterSnapshots.Length < Waters.Count) _waterSnapshots = new WaterSnapshot[Waters.Count];
    }

    private bool FindNearestFood(Vec2 position, float vision, bool carrionOnly, out int bestIndex, out float bestDistance, out Vec2 bestDirection)
    {
        bestIndex = -1;
        bestDistance = float.MaxValue;
        bestDirection = Vec2.Zero;

        for (var i = 0; i < Foods.Count; i++)
        {
            var snap = _foodSnapshots[i];
            if (!snap.Alive) continue;
            if (carrionOnly && !snap.IsCarrion) continue;
            var delta = snap.Position - position;
            var distance = delta.Length;
            if (distance < vision && distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
                bestDirection = distance <= 0.0001f ? Vec2.Zero : delta / distance;
            }
        }

        return bestIndex >= 0;
    }

    private bool FindNearestWater(Vec2 position, float vision, out int bestIndex, out float bestDistance, out Vec2 bestDirection)
    {
        bestIndex = -1;
        bestDistance = float.MaxValue;
        bestDirection = Vec2.Zero;

        for (var i = 0; i < Waters.Count; i++)
        {
            var snap = _waterSnapshots[i];
            var delta = snap.Position - position;
            var distance = delta.Length - snap.Radius;
            if (distance < vision && distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
                var len = (snap.Position - position).Length;
                bestDirection = len <= 0.0001f ? Vec2.Zero : (snap.Position - position) / len;
            }
        }

        return bestIndex >= 0;
    }

    private bool FindNearestPrey(Vec2 position, float vision, out int bestIndex, out float bestDistance, out Vec2 bestDirection)
    {
        bestIndex = -1;
        bestDistance = float.MaxValue;
        bestDirection = Vec2.Zero;

        for (var i = 0; i < Preys.Count; i++)
        {
            var snap = _preySnapshots[i];
            if (!snap.Alive) continue;
            var delta = snap.Position - position;
            var distance = delta.Length;
            if (distance < vision && distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
                bestDirection = distance <= 0.0001f ? Vec2.Zero : delta / distance;
            }
        }

        return bestIndex >= 0;
    }

    private bool FindNearestPredator(Vec2 position, float vision, out float bestDistance, out Vec2 bestDirection)
    {
        bestDistance = float.MaxValue;
        bestDirection = Vec2.Zero;
        var found = false;

        for (var i = 0; i < Predators.Count; i++)
        {
            var snap = _predatorSnapshots[i];
            if (!snap.Alive) continue;
            var delta = snap.Position - position;
            var distance = delta.Length;
            if (distance < vision && distance < bestDistance)
            {
                bestDistance = distance;
                bestDirection = distance <= 0.0001f ? Vec2.Zero : delta / distance;
                found = true;
            }
        }

        return found;
    }

    private static bool FindNearestAlly(AgentSnapshot[] snapshots, int selfIndex, Vec2 position, float range, out float bestDistance, out Vec2 bestDirection)
    {
        bestDistance = float.MaxValue;
        bestDirection = Vec2.Zero;
        var found = false;

        for (var i = 0; i < snapshots.Length; i++)
        {
            if (i == selfIndex) continue;
            var snap = snapshots[i];
            if (!snap.Alive) continue;
            var delta = snap.Position - position;
            var distance = delta.Length;
            if (distance < range && distance < bestDistance)
            {
                bestDistance = distance;
                bestDirection = distance <= 0.0001f ? Vec2.Zero : delta / distance;
                found = true;
            }
        }

        return found;
    }

    private float FindNearestWaterDistance(Vec2 position, float vision)
    {
        var found = FindNearestWater(position, vision, out _, out var bestDistance, out _);
        return found ? bestDistance : -1f;
    }

    private bool IsPlantTooClose(Vec2 position, float distance)
    {
        for (var i = 0; i < Plants.Count; i++)
        {
            if (Plants[i].Position.DistanceTo(position) < distance) return true;
        }
        return false;
    }

    private Agent CreateAgent(Species species, Vec2 position, int generation)
    {
        var traits = AgentTraits.CreateDefault(species);
        return new Agent
        {
            Id = _nextId++,
            Species = species,
            Position = position,
            Velocity = Vec2.FromAngle((float)(_rng.NextDouble() * Math.PI * 2.0)) * (12f + (float)(_rng.NextDouble() * 24.0)),
            Energy = traits.ReproduceEnergy * (species == Species.Prey ? 0.72f : 0.64f),
            Hydration = 72f + (float)(_rng.NextDouble() * 24.0),
            Age = 0f,
            Generation = generation,
            Brain = new Brain(22, 24, 8, _rng),
            Traits = traits,
            ReproductionCooldown = 4f,
            BroadcastCooldown = 0.5f,
            Radius = traits.Size,
            Elevation = traits.Lift,
            WanderPhase = (float)(_rng.NextDouble() * Math.PI * 2.0),
            CooperationBias = traits.Cooperation,
            CuriosityBias = traits.Curiosity,
            CourageBias = traits.Courage,
            AggressionBias = species == Species.Predator ? Math.Clamp(traits.Courage + 0.18f, 0f, 1f) : Math.Clamp(traits.Courage - 0.08f, 0f, 1f),
            LastStatus = "settling"
        };
    }

    private Plant CreatePlant(Vec2 position)
    {
        return new Plant
        {
            Position = position,
            Radius = 7.2f + (float)(_rng.NextDouble() * 2.0),
            Elevation = 1.2f + (float)(_rng.NextDouble() * 1.5),
            FruitTimer = 1.0f + (float)(_rng.NextDouble() * 3.0),
            SpreadTimer = 12.0f + (float)(_rng.NextDouble() * 18.0)
        };
    }

    private Food CreateFood(Vec2 position, bool carrion)
    {
        return new Food
        {
            Position = position,
            Radius = carrion ? 4.2f + (float)(_rng.NextDouble() * 2.2) : 2.6f + (float)(_rng.NextDouble() * 1.8),
            Nutrition = carrion ? 18f + (float)(_rng.NextDouble() * 12.0) : 12f + (float)(_rng.NextDouble() * 8.0),
            Age = 0f,
            LifeSpan = carrion ? 24f : 45f,
            IsCarrion = carrion,
            IsDead = false
        };
    }

    private Vec2 RandomPosition(float margin)
    {
        var spanX = MathF.Max(1f, Width - (margin * 2f));
        var spanY = MathF.Max(1f, Height - (margin * 2f));
        return new Vec2(
            margin + ((float)_rng.NextDouble() * spanX),
            margin + ((float)_rng.NextDouble() * spanY));
    }

    private void ClampAgent(Agent agent)
    {
        agent.Position = ClampToWorld(agent.Position, agent.Radius + 2f);
    }

    private float Signed(float scale) => (((float)_rng.NextDouble() * 2f) - 1f) * scale;
}
