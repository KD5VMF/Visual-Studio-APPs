using System;
using System.Drawing;
using System.Numerics;

namespace AICreatureLab.Core;

internal sealed class Creature
{
    public const int InputCount = 17;
    public const int OutputCount = 4;

    public NeuralNet Brain { get; private set; }
    public Vector2 Position { get; private set; }
    public Vector2 Velocity { get; private set; }
    public float FacingAngle { get; private set; }
    public float Energy { get; private set; }
    public float Health { get; private set; }
    public float AgeSeconds { get; private set; }
    public float ReproductionCooldown { get; private set; }
    public float DistanceTravelled { get; private set; }
    public float MaxEnergySeen { get; private set; }
    public int Generation { get; private set; }
    public int ChildrenProduced { get; private set; }
    public Color BodyColor { get; private set; }
    public bool IsDead => Health <= 0f || Energy <= 0f;

    public float Score => (AgeSeconds * 0.9f) + (ChildrenProduced * 95f) + (DistanceTravelled * 0.035f) + (MaxEnergySeen * 0.15f);

    public Creature(NeuralNet brain, Vector2 position, float angle, int generation, Color color, SimulationConfig config)
    {
        Brain = brain;
        Position = position;
        FacingAngle = angle;
        Generation = generation;
        BodyColor = color;
        Energy = config.InitialEnergy;
        Health = config.InitialHealth;
        MaxEnergySeen = Energy;
        Velocity = Vector2.Zero;
    }

    public void Update(SimulationWorld world, float dt, out Creature? offspring)
    {
        offspring = null;
        AgeSeconds += dt;
        ReproductionCooldown = MathF.Max(0f, ReproductionCooldown - dt);

        var forward = MathUtil.AngleToVector(FacingAngle);

        var nearestFood = world.FindNearestFood(Position);
        var nearestHazard = world.FindNearestHazard(Position);
        var nearestCreature = world.FindNearestCreature(Position, this);

        Span<double> inputs = stackalloc double[InputCount];
        inputs[0] = 1.0;
        inputs[1] = MathUtil.SafeNormalize(Energy, world.Config.MaxEnergy);
        inputs[2] = MathUtil.SafeNormalize(Health, world.Config.MaxHealth);
        inputs[3] = MathUtil.SafeNormalize(AgeSeconds, world.Config.MaxCreatureAgeSeconds);
        inputs[4] = MathUtil.SafeNormalize(Velocity.Length(), 140f);
        FillSenseInputs(inputs, 5, nearestFood.VectorToTarget, world.Config.SensorRange, forward);
        FillSenseInputs(inputs, 8, nearestHazard.VectorToTarget, world.Config.SensorRange, forward);
        FillSenseInputs(inputs, 11, nearestCreature.VectorToTarget, world.Config.SensorRange, forward);
        inputs[14] = ((Position.X / world.Config.WorldWidth) * 2.0) - 1.0;
        inputs[15] = ((Position.Y / world.Config.WorldHeight) * 2.0) - 1.0;
        inputs[16] = (Random.Shared.NextDouble() * 2.0) - 1.0;

        var outputs = Brain.Forward(inputs);
        var turn = (float)outputs[0];
        var thrust = MathF.Max(0f, (float)outputs[1]);
        var reproduceDesire = MathF.Max(0f, (float)outputs[2]);
        var boost = MathF.Max(0f, (float)outputs[3]);

        FacingAngle += turn * world.Config.TurnRate * dt;
        var acceleration = forward * ((world.Config.BaseAcceleration * thrust) + (world.Config.BoostAcceleration * boost));
        Velocity += acceleration * dt;
        Velocity *= world.Config.Drag;

        var oldPosition = Position;
        Position += Velocity * dt;
        Position = new Vector2(
            MathUtil.Wrap(Position.X, world.Config.WorldWidth),
            MathUtil.Wrap(Position.Y, world.Config.WorldHeight));

        DistanceTravelled += world.GetShortestWrappedVector(oldPosition, Position).Length();

        var energyBurn =
            world.Config.BasalEnergyBurnPerSecond
            + (thrust * world.Config.MovementEnergyBurn)
            + (boost * world.Config.BoostEnergyBurn);

        Energy -= energyBurn * dt;

        if (AgeSeconds > world.Config.MaxCreatureAgeSeconds)
        {
            Health -= 18f * dt;
        }

        MaxEnergySeen = MathF.Max(MaxEnergySeen, Energy);

        world.TryConsumeFood(this);
        world.TryHitHazard(this);

        if (!IsDead
            && reproduceDesire > 0.68f
            && AgeSeconds >= world.Config.ReproductionAgeSeconds
            && ReproductionCooldown <= 0f
            && Energy >= world.Config.ReproductionThresholdEnergy
            && world.Creatures.Count < world.Config.MaximumCreatures)
        {
            Energy -= world.Config.ReproductionEnergyCost;
            ReproductionCooldown = world.Config.ReproductionCooldownSeconds;
            ChildrenProduced++;

            var childBrain = Brain.CreateMutatedChild(Random.Shared, world.Config);
            var childAngle = FacingAngle + ((float)Random.Shared.NextDouble() - 0.5f) * 0.85f;
            var offset = MathUtil.AngleToVector(childAngle) * (world.Config.CreatureRadius * 3f);
            var childPos = new Vector2(
                MathUtil.Wrap(Position.X + offset.X, world.Config.WorldWidth),
                MathUtil.Wrap(Position.Y + offset.Y, world.Config.WorldHeight));

            var childColor = MutateColor(BodyColor, Random.Shared);
            offspring = new Creature(childBrain, childPos, childAngle, Generation + 1, childColor, world.Config);
        }
    }

    public void ApplySavedChampionStyle(NeuralNet brain, int generation, Color color, SimulationConfig config)
    {
        Brain = brain;
        Generation = generation;
        BodyColor = color;
        Energy = config.InitialEnergy;
        Health = config.InitialHealth;
        MaxEnergySeen = Energy;
        DistanceTravelled = 0f;
        AgeSeconds = 0f;
        ReproductionCooldown = 0f;
        ChildrenProduced = 0;
        Velocity = Vector2.Zero;
    }

    public void AddEnergy(float amount, float maxEnergy)
    {
        Energy = MathF.Min(maxEnergy, Energy + amount);
        MaxEnergySeen = MathF.Max(MaxEnergySeen, Energy);
    }

    public void AddHealth(float amount, float maxHealth) => Health = MathF.Min(maxHealth, Health + amount);

    public void Damage(float amount) => Health -= amount;

    public void Nudge(Vector2 direction, float amount)
    {
        if (direction.LengthSquared() > 0.001f)
        {
            direction = Vector2.Normalize(direction);
        }

        Velocity += direction * amount;
    }

    private static void FillSenseInputs(Span<double> buffer, int startIndex, Vector2 vectorToTarget, float maxRange, Vector2 forward)
    {
        var distance = vectorToTarget.Length();
        if (distance <= 0.0001f || distance > maxRange)
        {
            buffer[startIndex] = 0.0;
            buffer[startIndex + 1] = 0.0;
            buffer[startIndex + 2] = 0.0;
            return;
        }

        var direction = Vector2.Normalize(vectorToTarget);
        buffer[startIndex] = MathUtil.MapDistanceScore(distance, maxRange);
        buffer[startIndex + 1] = Vector2.Dot(forward, direction);
        buffer[startIndex + 2] = MathUtil.SignedAngle(forward, direction) / Math.PI;
    }

    private static Color MutateColor(Color source, Random random)
    {
        int Mutate(int value)
        {
            var offset = random.Next(-12, 13);
            return (int)MathUtil.Clamp(value + offset, 50, 255);
        }

        return Color.FromArgb(255, Mutate(source.R), Mutate(source.G), Mutate(source.B));
    }
}
