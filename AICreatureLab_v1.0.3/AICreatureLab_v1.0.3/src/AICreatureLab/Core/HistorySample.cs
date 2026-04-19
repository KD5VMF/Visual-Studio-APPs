namespace AICreatureLab.Core;

internal readonly record struct HistorySample(
    float TimeSeconds,
    int Population,
    float BestScore,
    float AverageEnergy,
    int FoodCount,
    int HazardCount);
