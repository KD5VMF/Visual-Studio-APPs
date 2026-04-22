# BioGenesis X: Emergent Life Simulator

BioGenesis X is a predator-prey evolution sandbox focused on emergent behavior, survival pressure, inherited traits, and shared species learning.

## Core idea

This simulation is built around one question: what happens when life must choose between surviving longer, reproducing now, or investing in itself for a stronger future?

Both predator and prey are meant to behave as if the **current world matters**. They should not casually drift into extinction just because a reset is possible. Population pressure, resource pressure, and long-term learned behavior all push the ecosystem toward preserving the living world when possible.

## Current feature set

- GPU-rendered OpenGL world using **OpenTK**
- predator and prey species with persistent shared training memory
- real-time population-aware reproduction pressure
- self-investment behavior where agents can strengthen themselves instead of reproducing immediately
- inherited individual trait growth such as improved sensing and movement potential
- elder highlighting so the oldest living predator and prey stand out
- male/female variants for both species with distinct colors and mate-based reproduction
- one-button hide/show UI mode for clean observation of the world
- larger navigable world with zoom, pan, and creature selection
- simple creature visuals intended to support future richer designs
- terrain cover and plant areas that support prey hiding and predator ambush behavior
- multithreaded world updates using `Parallel.For`

## Intended behavior direction

BioGenesis X is meant to evolve toward:

- emergent survival behavior instead of fixed scripted instincts
- continual shared model updates during life, not only at death
- species that react to population collapse as a real danger
- long-running simulations where strange stable behaviors can emerge over days or weeks
- lineages that pass down learned advantages and reinforced tendencies

## Controls

- **Pause/Resume** button or **Space**
- **Reset** button or **R**
- **Fullscreen** button or **F11**
- **Esc** leaves fullscreen
- **Help** button or **H**
- **Hide UI** button or **Tab**
- **- / +** change simulation speed
- **[ / ]** change worker thread count
- mouse wheel zooms in and out
- right mouse drag pans the camera
- click a creature to inspect it
- **F** focuses the camera on the selected creature

## Build

Open `BioGenesisX.csproj` in Visual Studio 2022 or later, restore NuGet packages, then build and run.

## Notes

- This package is delivered as a renamed, source-patched project.
- A real .NET build was **not** run inside the container because the .NET SDK is not installed here.
- If Visual Studio shows any compile errors, send the exact messages and they can be patched directly.

## Naming

Project name: **BioGenesis X**  
Subtitle: **Emergent Life Simulator**


## Persistence

- Training stays live in memory during the run.
- SSD checkpoint writes happen every 10 minutes.
- Immediate disk saves still happen on shutdown, resets, extinction events, and recovery/collapse milestones.


## New in v15

- Both species now have male/female variants with distinct colors: prey use green/purple, predators use red/magenta.
- Reproduction is now mate-based instead of purely self-triggered. Adults must find an opposite-sex partner nearby.
- Offspring now blend both parents' inherited traits and mix both parents' neural weights before mutation.
- Reproduction pressure is stronger, birth energy thresholds are lower, and self-trade-ins cost more so lineages are passed on sooner.
- Predator courage/aggression tuning was raised, while prey aggression learning was reduced so prey behave less strangely.
- The full HUD can now be hidden with one button for a clean watch-only mode, and brought back with the same button or Tab.
