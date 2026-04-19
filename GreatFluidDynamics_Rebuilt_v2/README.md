# Great Fluid Dynamics Rebuilt v2

This version keeps the cleaner single-project layout and adds an **Auto Run** mode so the simulation starts with flowing swirls automatically.

## What it is
A faster and cleaner 2D incompressible fluid demo in C# WinForms:
- semi-Lagrangian advection
- pressure projection for incompressibility
- buoyancy
- vorticity confinement
- obstacle painting
- render modes for dye, velocity, pressure, and divergence
- automatic swirl injectors for a pretty startup scene

## Important honesty
This rebuild is **not** a true GPU compute solver yet.
It is:
- a CPU fluid solver
- an optimized flat-array renderer
- a single EXE project with no startup-project confusion

That means it looks better and is easier to use, but it still does not use the RTX 3060s for the actual CFD solve.

## New in this version
- default startup grid changed to **256 x 144**
- **Auto Run** starts enabled
- new **Auto Run** toggle button in the toolbar
- press **A** to toggle Auto Run from the keyboard
- HUD now shows whether Auto Run is on or off

## Controls
- Left mouse: inject dye + force
- Right mouse: paint obstacle
- Shift + Right mouse: erase obstacle
- Mouse wheel: adjust vorticity confinement
- Space: pause/resume
- A: toggle auto run
- C: clear
- F: fullscreen
- H: toggle shading
- 1/2/3/4: render mode
- +/-: thread count

## Open
Open `GreatFluidDynamics.Rebuilt.sln` in Visual Studio 2022+ and run.

## Suggested first settings
- Grid: 256 x 144
- Threads: up to 48 on your HP workstation
- Pressure iterations: 18 to 24
