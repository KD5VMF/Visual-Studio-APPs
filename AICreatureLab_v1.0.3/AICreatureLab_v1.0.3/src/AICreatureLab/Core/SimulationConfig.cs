namespace AICreatureLab.Core;

internal sealed class SimulationConfig
{
    public float WorldWidth { get; set; } = 3000f;
    public float WorldHeight { get; set; } = 1900f;

    public int InitialCreatures { get; set; } = 160;
    public int MinimumCreatures { get; set; } = 96;
    public int MaximumCreatures { get; set; } = 420;

    public int TargetFoodCount { get; set; } = 650;
    public int TargetHazardCount { get; set; } = 150;

    public int ParallelThresholdCreatures { get; set; } = 48;

    public float MaxCreatureAgeSeconds { get; set; } = 180f;
    public float InitialEnergy { get; set; } = 85f;
    public float InitialHealth { get; set; } = 100f;
    public float MaxEnergy { get; set; } = 180f;
    public float MaxHealth { get; set; } = 100f;

    public float FoodEnergy { get; set; } = 36f;
    public float FoodHealAmount { get; set; } = 4f;
    public float HazardDamage { get; set; } = 25f;

    public float BasalEnergyBurnPerSecond { get; set; } = 2.4f;
    public float MovementEnergyBurn { get; set; } = 2.2f;
    public float BoostEnergyBurn { get; set; } = 4.0f;

    public float BaseAcceleration { get; set; } = 74f;
    public float BoostAcceleration { get; set; } = 155f;
    public float Drag { get; set; } = 0.975f;
    public float TurnRate { get; set; } = 2.8f;

    public float CreatureRadius { get; set; } = 11f;
    public float FoodRadius { get; set; } = 5f;
    public float HazardRadius { get; set; } = 9f;
    public float SensorRange { get; set; } = 320f;

    public float ReproductionAgeSeconds { get; set; } = 11f;
    public float ReproductionEnergyCost { get; set; } = 42f;
    public float ReproductionThresholdEnergy { get; set; } = 122f;
    public float ReproductionCooldownSeconds { get; set; } = 8f;

    public double MutationStrength { get; set; } = 0.18;
    public double MutationChance { get; set; } = 0.08;
    public double BigMutationChance { get; set; } = 0.02;
}
