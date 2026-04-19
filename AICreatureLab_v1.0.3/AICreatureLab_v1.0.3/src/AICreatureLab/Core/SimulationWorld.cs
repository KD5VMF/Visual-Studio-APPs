using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace AICreatureLab.Core;

internal sealed class SimulationWorld
{
    private const int HistoryCapacity = 360;

    private readonly object _foodLock = new();
    private readonly object _hazardLock = new();
    private readonly ParallelOptions _parallelOptions;
    private float _historyTimer;
    private Vector2[]? _foodPositionSnapshot;
    private Vector2[]? _hazardPositionSnapshot;
    private CreatureSenseSnapshot[]? _creatureSnapshot;

        private Random Rng => Random.Shared;
    public SimulationConfig Config { get; }
    public List<Creature> Creatures { get; } = new();
    public List<FoodPellet> Foods { get; } = new();
    public List<HazardOrb> Hazards { get; } = new();
    public List<HistorySample> History { get; } = new();

    public float TimeSeconds { get; private set; }
    public bool DrawSensors { get; set; } = true;
    public float TimeScale { get; set; } = 1.0f;
    public int TotalBirths { get; private set; }
    public int TotalDeaths { get; private set; }
    public int TotalFoodEaten { get; set; }
    public int TotalHazardHits { get; set; }
    public Creature? Champion { get; private set; }
    public float ChampionScore { get; private set; }
    public SavedGenome? LoadedGenome { get; private set; }
    public int WorkerThreadCount => _parallelOptions.MaxDegreeOfParallelism;
    public bool LastUpdateUsedParallel { get; private set; }
    public double LastUpdateMilliseconds { get; private set; }

    public float AverageEnergy => Creatures.Count == 0 ? 0f : Creatures.Average(c => c.Energy);
    public float AverageAge => Creatures.Count == 0 ? 0f : Creatures.Average(c => c.AgeSeconds);
    public int HighestGeneration => Creatures.Count == 0 ? (LoadedGenome?.Generation ?? 0) : Creatures.Max(c => c.Generation);

    public SimulationWorld(SimulationConfig config)
    {
        Config = config;
        _parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount)
        };

        ResetWithRandomPopulation();
    }


    public FoodPellet[] GetFoodSnapshot()
    {
        lock (_foodLock)
        {
            return Foods.ToArray();
        }
    }

    public HazardOrb[] GetHazardSnapshot()
    {
        lock (_hazardLock)
        {
            return Hazards.ToArray();
        }
    }

    public Creature[] GetCreatureSnapshot() => Creatures.ToArray();

    public HistorySample[] GetHistorySnapshot() => History.ToArray();

    public void ResetWithRandomPopulation()
    {
        TimeSeconds = 0f;
        TotalBirths = 0;
        TotalDeaths = 0;
        TotalFoodEaten = 0;
        TotalHazardHits = 0;
        Champion = null;
        ChampionScore = 0f;
        History.Clear();
        _historyTimer = 0f;
        Foods.Clear();
        Hazards.Clear();
        Creatures.Clear();
        LoadedGenome = null;

        for (var i = 0; i < Config.InitialCreatures; i++)
        {
            Creatures.Add(CreateRandomCreature(0));
        }

        TopUpResources();
        AddHistorySample(force: true);
    }

    public void ResetFromLoadedChampion(SavedGenome genome)
    {
        LoadedGenome = genome;
        TimeSeconds = 0f;
        TotalBirths = 0;
        TotalDeaths = 0;
        TotalFoodEaten = 0;
        TotalHazardHits = 0;
        Champion = null;
        ChampionScore = 0f;
        History.Clear();
        _historyTimer = 0f;
        Foods.Clear();
        Hazards.Clear();
        Creatures.Clear();

        var championBrain = NeuralNet.FromSavedGenome(genome);
        var championColor = Color.FromArgb(genome.BodyColorArgb);

        for (var i = 0; i < Config.InitialCreatures; i++)
        {
            var brain = i == 0
                ? championBrain.Clone()
                : championBrain.CreateMutatedChild(Rng, Config);

            var generation = i == 0 ? genome.Generation : genome.Generation + 1;
            var color = i == 0 ? championColor : MutateColor(championColor);

            Creatures.Add(new Creature(
                brain,
                RandomPoint(),
                (float)(Rng.NextDouble() * Math.PI * 2.0),
                generation,
                color,
                Config));
        }

        TopUpResources();
        AddHistorySample(force: true);
    }

    public void Update(float realDt)
    {
        var dt = realDt * TimeScale;
        if (dt <= 0f)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        TimeSeconds += dt;
        TopUpResources();

        var creatureCount = Creatures.Count;
        var newbornSlots = new Creature?[creatureCount];

        Volatile.Write(ref _foodPositionSnapshot, GetFoodSnapshot().Select(f => f.Position).ToArray());
        Volatile.Write(ref _hazardPositionSnapshot, GetHazardSnapshot().Select(h => h.Position).ToArray());
        Volatile.Write(ref _creatureSnapshot, GetCreatureSnapshot().Select(c => new CreatureSenseSnapshot(c, c.Position)).ToArray());

        var useParallel = creatureCount >= Config.ParallelThresholdCreatures && WorkerThreadCount > 1;
        LastUpdateUsedParallel = useParallel;

        try
        {
            if (useParallel)
            {
                Parallel.For(0, creatureCount, _parallelOptions, i =>
                {
                    Creatures[i].Update(this, dt, out newbornSlots[i]);
                });
            }
            else
            {
                for (var i = 0; i < creatureCount; i++)
                {
                    Creatures[i].Update(this, dt, out newbornSlots[i]);
                }
            }
        }
        finally
        {
            Volatile.Write(ref _foodPositionSnapshot, null);
            Volatile.Write(ref _hazardPositionSnapshot, null);
            Volatile.Write(ref _creatureSnapshot, null);
        }

        var newborns = new List<Creature>();
        for (var i = 0; i < creatureCount; i++)
        {
            var creature = Creatures[i];
            ConsiderChampion(creature);

            if (newbornSlots[i] is not null)
            {
                newborns.Add(newbornSlots[i]!);
            }
        }

        if (newborns.Count > 0)
        {
            var availableSlots = Math.Max(0, Config.MaximumCreatures - Creatures.Count);
            if (newborns.Count > availableSlots)
            {
                newborns.RemoveRange(availableSlots, newborns.Count - availableSlots);
            }

            if (newborns.Count > 0)
            {
                Creatures.AddRange(newborns);
                TotalBirths += newborns.Count;
            }
        }

        for (var i = Creatures.Count - 1; i >= 0; i--)
        {
            if (!Creatures[i].IsDead)
            {
                continue;
            }

            ConsiderChampion(Creatures[i]);
            Creatures.RemoveAt(i);
            TotalDeaths++;
        }

        if (Creatures.Count < Config.MinimumCreatures)
        {
            BackfillPopulation();
        }

        AddHistorySample(force: false);

        stopwatch.Stop();
        LastUpdateMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
    }

    public (Vector2 VectorToTarget, float DistanceSquared) FindNearestFood(Vector2 position)
    {
        var snapshot = Volatile.Read(ref _foodPositionSnapshot);
        if (snapshot is not null)
        {
            return FindNearestFromPositions(position, snapshot);
        }

        lock (_foodLock)
        {
            if (Foods.Count == 0)
            {
                return (Vector2.Zero, float.MaxValue);
            }

            var positions = Foods.Select(f => f.Position).ToArray();
            return FindNearestFromPositions(position, positions);
        }
    }

    public (Vector2 VectorToTarget, float DistanceSquared) FindNearestHazard(Vector2 position)
    {
        var snapshot = Volatile.Read(ref _hazardPositionSnapshot);
        if (snapshot is not null)
        {
            return FindNearestFromPositions(position, snapshot);
        }

        lock (_hazardLock)
        {
            if (Hazards.Count == 0)
            {
                return (Vector2.Zero, float.MaxValue);
            }

            var positions = Hazards.Select(h => h.Position).ToArray();
            return FindNearestFromPositions(position, positions);
        }
    }

    public (Vector2 VectorToTarget, float DistanceSquared) FindNearestCreature(Vector2 position, Creature self)
    {
        var snapshot = Volatile.Read(ref _creatureSnapshot);
        var bestVector = Vector2.Zero;
        var bestDistSq = float.MaxValue;

        if (snapshot is not null)
        {
            for (var i = 0; i < snapshot.Length; i++)
            {
                if (ReferenceEquals(snapshot[i].Creature, self))
                {
                    continue;
                }

                var delta = ShortestWrappedVector(position, snapshot[i].Position);
                var distSq = delta.LengthSquared();
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestVector = delta;
                }
            }

            return (bestVector, bestDistSq);
        }

        foreach (var creature in Creatures)
        {
            if (ReferenceEquals(creature, self))
            {
                continue;
            }

            var delta = ShortestWrappedVector(position, creature.Position);
            var distSq = delta.LengthSquared();
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestVector = delta;
            }
        }

        return (bestVector, bestDistSq);
    }

    public SavedGenome? SaveChampionGenome()
    {
        if (Champion is null)
        {
            return null;
        }

        return Champion.Brain.ToSavedGenome(
            Champion.Generation,
            Champion.BodyColor.ToArgb(),
            $"Saved from {AICreatureLab.AppInfo.DisplayName} at simulation t={TimeSeconds:0.0}s with score {ChampionScore:0.00}");
    }

    public void LoadGenome(SavedGenome genome) => ResetFromLoadedChampion(genome);

    public Vector2 GetShortestWrappedVector(Vector2 from, Vector2 to) => ShortestWrappedVector(from, to);

    public bool TryConsumeFood(Creature creature)
    {
        var eatRadiusSq = (Config.CreatureRadius + Config.FoodRadius) * (Config.CreatureRadius + Config.FoodRadius);

        lock (_foodLock)
        {
            for (var i = Foods.Count - 1; i >= 0; i--)
            {
                var delta = ShortestWrappedVector(creature.Position, Foods[i].Position);
                if (delta.LengthSquared() > eatRadiusSq)
                {
                    continue;
                }

                creature.AddEnergy(Config.FoodEnergy, Config.MaxEnergy);
                creature.AddHealth(Config.FoodHealAmount, Config.MaxHealth);
                Foods.RemoveAt(i);
                TotalFoodEaten++;
                return true;
            }
        }

        return false;
    }

    public bool TryHitHazard(Creature creature)
    {
        var hitRadiusSq = (Config.CreatureRadius + Config.HazardRadius) * (Config.CreatureRadius + Config.HazardRadius);

        lock (_hazardLock)
        {
            for (var i = Hazards.Count - 1; i >= 0; i--)
            {
                var delta = ShortestWrappedVector(Hazards[i].Position, creature.Position);
                if (delta.LengthSquared() > hitRadiusSq)
                {
                    continue;
                }

                creature.Damage(Config.HazardDamage);
                creature.Nudge(delta, 75f);
                creature.AddEnergy(-12f, Config.MaxEnergy);
                Hazards.RemoveAt(i);
                TotalHazardHits++;
                return true;
            }
        }

        return false;
    }

    private void BackfillPopulation()
    {
        while (Creatures.Count < Config.MinimumCreatures)
        {
            if (Champion is not null)
            {
                var brain = Champion.Brain.CreateMutatedChild(Rng, Config);
                var color = MutateColor(Champion.BodyColor);

                Creatures.Add(new Creature(
                    brain,
                    RandomPoint(),
                    (float)(Rng.NextDouble() * Math.PI * 2.0),
                    Champion.Generation + 1,
                    color,
                    Config));
            }
            else if (LoadedGenome is not null)
            {
                var brain = NeuralNet.FromSavedGenome(LoadedGenome).CreateMutatedChild(Rng, Config);
                Creatures.Add(new Creature(
                    brain,
                    RandomPoint(),
                    (float)(Rng.NextDouble() * Math.PI * 2.0),
                    LoadedGenome.Generation + 1,
                    MutateColor(Color.FromArgb(LoadedGenome.BodyColorArgb)),
                    Config));
            }
            else
            {
                Creatures.Add(CreateRandomCreature(0));
            }
        }
    }

    private void TopUpResources()
    {
        while (Foods.Count < Config.TargetFoodCount)
        {
            Foods.Add(new FoodPellet { Position = RandomPoint() });
        }

        while (Hazards.Count < Config.TargetHazardCount)
        {
            Hazards.Add(new HazardOrb { Position = RandomPoint() });
        }
    }

    private Creature CreateRandomCreature(int generation)
    {
        var color = Color.FromArgb(
            255,
            Rng.Next(80, 250),
            Rng.Next(80, 250),
            Rng.Next(80, 250));

        var brain = NeuralNet.CreateRandom(Rng, Creature.InputCount, 24, 24, Creature.OutputCount);
        return new Creature(brain, RandomPoint(), (float)(Rng.NextDouble() * Math.PI * 2.0), generation, color, Config);
    }

    private Vector2 RandomPoint() =>
        new(
            (float)(Rng.NextDouble() * Config.WorldWidth),
            (float)(Rng.NextDouble() * Config.WorldHeight));

    private void ConsiderChampion(Creature creature)
    {
        if (creature.Score <= ChampionScore)
        {
            return;
        }

        ChampionScore = creature.Score;
        Champion = creature;
    }

    private void AddHistorySample(bool force)
    {
        _historyTimer += force ? 999f : TimeScale * (1f / 60f);
        if (!force && _historyTimer < 0.45f)
        {
            return;
        }

        _historyTimer = 0f;

        History.Add(new HistorySample(
            TimeSeconds,
            Creatures.Count,
            ChampionScore,
            AverageEnergy,
            Foods.Count,
            Hazards.Count));

        while (History.Count > HistoryCapacity)
        {
            History.RemoveAt(0);
        }
    }

    private (Vector2 VectorToTarget, float DistanceSquared) FindNearestFromPositions(Vector2 position, IReadOnlyList<Vector2> positions)
    {
        var bestVector = Vector2.Zero;
        var bestDistSq = float.MaxValue;

        for (var i = 0; i < positions.Count; i++)
        {
            var delta = ShortestWrappedVector(position, positions[i]);
            var distSq = delta.LengthSquared();
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestVector = delta;
            }
        }

        return (bestVector, bestDistSq);
    }

    private Vector2 ShortestWrappedVector(Vector2 from, Vector2 to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;

        if (MathF.Abs(dx) > Config.WorldWidth / 2f)
        {
            dx -= MathF.Sign(dx) * Config.WorldWidth;
        }

        if (MathF.Abs(dy) > Config.WorldHeight / 2f)
        {
            dy -= MathF.Sign(dy) * Config.WorldHeight;
        }

        return new Vector2(dx, dy);
    }

    private Color MutateColor(Color source)
    {
        int Mutate(int value)
        {
            var offset = Rng.Next(-8, 9);
            return (int)MathUtil.Clamp(value + offset, 55, 255);
        }

        return Color.FromArgb(255, Mutate(source.R), Mutate(source.G), Mutate(source.B));
    }

    private readonly record struct CreatureSenseSnapshot(Creature Creature, Vector2 Position);
}
