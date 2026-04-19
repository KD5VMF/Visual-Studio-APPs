# Atom Playground

A Windows desktop C# app for experimenting with atoms in a visually rich sandbox.

## What this version does

- Lets the user switch at any time between:
  - **Chemistry** mode
  - **Nuclear** mode
  - **Hybrid** mode
- Supports:
  - spawning atoms and clusters
  - selecting one or more atoms
  - adding or removing electrons, neutrons, and protons
  - smashing selected atoms together
  - watching simplified bond, ionization, fusion, fission, and decay behavior
- Includes:
  - smooth zoom and pan
  - left-click selection
  - Shift+click multi-select
  - drag repositioning
  - optional HUD, shells, bonds, grid, and trails
  - event log and atom info panel

## Build target

This project is a **WPF** desktop app targeting **net8.0-windows**.

## Open and run

1. Open `AtomPlayground.sln` in Visual Studio on Windows.
2. Let NuGet/package restore finish if Visual Studio prompts for anything.
3. Build and run the `AtomPlayground` project.

## Controls

- **Mouse wheel**: zoom
- **Right-drag**: pan
- **Left-click**: select one atom
- **Shift+Left-click**: add/remove from selection
- **Left-drag**: move a selected atom
- **Space**: pause/resume
- **Delete**: delete selected atoms

## Physics notes

This is intentionally a **playable sandbox**, not an exact quantum or nuclear research simulator.

The app uses simplified but grounded rules:

- **Chemistry mode** emphasizes charge, shell visualization, electron transfer, and bond-like interactions.
- **Nuclear mode** emphasizes isotope stability, decay-like behavior, fusion of very light nuclei, and fission/spallation of heavy nuclei.
- **Hybrid mode** allows both families of rules to matter at once.

That gives the user something understandable and interactive while still reacting in ways that feel tied to real atomic ideas.

## Suggested next upgrades

If you want a stronger Version 2, these are the best additions:

1. A richer periodic table panel with categories and search.
2. Explicit isotopes and known stable isotope tables.
3. More detailed reaction products and emitted particles.
4. Better bond rules for molecules and crystal lattices.
5. GPU-accelerated rendering path for heavier scenes.
6. Save/load sandbox scenes.
7. 3D camera mode.



## v4 usability updates

- atoms now bounce inside the visible sandbox so the screen acts as their world
- mouse wheel zoom is enabled and tuned
- fullscreen toggle added in the top bar and F11 / Esc support added
- reset now also recenters and refits the world view


## v10 scientifically grounded edition

- Shows all electrons in shells and all nucleons in each nucleus.
- Adds more reaction channels: charge recombination, isotope exchange, capture-like reactions, alpha-like emission, proton/neutron emission, and richer decay paths.
- Keeps the sandbox interactive and visual.

### Important honesty note

This is not a literal 100% exact atom simulator. A truly exact atomic and nuclear simulator would require quantum mechanics, quantum electrodynamics, quantum chromodynamics, probabilistic state evolution, and massive computation beyond what a real-time WPF sandbox should claim. This project is intentionally **grounded but simplified** so it stays interactive, visual, and understandable.
