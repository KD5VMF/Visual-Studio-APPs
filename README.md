# C# Simulations, Sandboxes, and Visual Experiments

This repository is a collection of Windows desktop C# projects focused on interactive simulation, scientific-style visualization, evolutionary systems, physics toys, and graphical experiments.

Most of the projects are designed to be opened directly in **Visual Studio** and run as standalone desktop apps. Across the repo you will find a mix of:

- **Windows Forms** applications
- **WPF** desktop applications
- **OpenTK / OpenGL** accelerated visual projects
- simulation-heavy experiments built for modern Windows PCs

## What this repo includes

### AI Creature Lab
A higher-workload creature evolution simulator with neural-net creatures, champion save/load support, and a parallel update path designed to use many logical CPU threads.

### Atom Playground
A visually rich atomic sandbox where you can spawn atoms and clusters, manipulate protons/neutrons/electrons, switch between chemistry, nuclear, and hybrid modes, and observe simplified but grounded reactions.

### Dimension Explorer
A dimension and hypercube viewer that lets you explore 1D through 12D using rotating projection-based hypercube rendering with zoom, drag rotation, auto-rotation, and optional labels/axes.

### Great Fluid Dynamics Rebuilt
A cleaner 2D incompressible fluid simulation project featuring advection, pressure projection, buoyancy, vorticity confinement, multiple render modes, and interactive dye/force injection. The v2 build also adds an automatic swirl-driven startup mode for a more attractive first launch.

### Helix Solar Show
A fullscreen OpenTK/OpenGL space animation that turns the solar system into a cinematic helical motion display, with shaded 3D spheres, orbital trails, automatic camera movement, and screensaver-like presentation.

### LifeForge Accelerated
An accelerated life/evolution-style simulation using OpenTK for rendering, multithreaded world updates, fullscreen support, HUD textures, and adjustable simulation speed and worker-thread count.

### Newton's Cradle Studio
A polished Newton's cradle desktop physics toy with click-and-drag pull/release interaction, adjustable ball count, time scale, and tuned near-ideal momentum transfer behavior.

### PredatorPreyEvolutionCS
A predator-prey evolution simulation with plants, food, prey, predators, simple per-agent neural networks, mutation, reproduction, live history graphs, and click-to-inspect behavior.

## General setup

Most folders in this repository contain their own solution or project file and a local README with project-specific notes.

In general, the workflow is:

1. Open the relevant folder.
2. Open the `.sln` or `.csproj` file in **Visual Studio 2022 or 2026**.
3. Allow package restore if prompted.
4. Build and run.

## Common technologies used

Depending on the project, you will see combinations of:

- **.NET 8**
- **Windows Forms**
- **WPF**
- **OpenTK / OpenGL**

## Good starting points

If you are new to the repo, these are good first launches:

- **Atom Playground** for hands-on sandbox interaction
- **Newton's Cradle Studio** for a clean physics demo
- **Dimension Explorer** for math/visual exploration
- **Great Fluid Dynamics Rebuilt v2** for fluid-style motion and visuals
- **Helix Solar Show** for a fullscreen visual showcase

## Notes

- These projects aim to be **interactive, visually interesting, and understandable**, not always laboratory-grade scientific solvers.
- Some simulations are intentionally **grounded but simplified** so they stay responsive and enjoyable in real time.
- Several projects are especially well suited to stronger desktop hardware, including high-core-count CPUs and modern GPUs.

## Repository purpose

The overall goal of this repo is to collect a wide variety of C# experiments that mix:

- simulation
- visualization
- learning/evolution systems
- interactive physics
- science-inspired sandboxes
- visually impressive desktop rendering

If you want project-specific controls, limitations, or upgrade ideas, open the README inside that project's folder.
