# PredatorPreyEvolutionCS

A standalone C# / WinForms predator-prey evolution simulation inspired by the YouTube video:

- **Evolving AIs - More Complex Environment** by Pezzza's Work

The video describes a simulation with predators, prey, plants, and food where agents try to survive and reproduce.

## What this C# version includes

- Plants that generate food and spread over time
- Prey agents that:
  - seek food
  - avoid predators
  - reproduce when they have enough energy
- Predator agents that:
  - hunt prey
  - reproduce when they have enough energy
- Tiny per-agent neural networks
- Mutation of both brain weights and body traits across generations
- Live population history graph
- Click-to-inspect selected agent stats

## Build

Open `PredatorPreyEvolutionCS.csproj` in Visual Studio 2022/2026 on Windows and run it.

Target:
- .NET 8
- Windows Forms
- No external NuGet packages required

## Notes

This is a clean C# interpretation of the video's core ideas, not an exact clone of the original creator's internal source code.

If you want the next version, the best upgrades would be:

1. species-specific vision cones instead of omnidirectional sensing
2. egg / mate mechanics instead of asexual reproduction
3. terrain, water, obstacles, and day/night cycles
4. larger worlds with spatial partitioning for higher agent counts
5. save/load snapshots and replay mode
