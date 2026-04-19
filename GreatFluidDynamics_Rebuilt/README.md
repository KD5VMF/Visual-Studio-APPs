# Great Fluid Dynamics Rebuilt

This is the cleaned-up single-project rebuild.

## What it is
A faster and cleaner 2D incompressible fluid demo in C# WinForms:
- semi-Lagrangian advection
- pressure projection for incompressibility
- buoyancy
- vorticity confinement
- obstacle painting
- render modes for dye, velocity, pressure, and divergence

## Important honesty
This rebuild is **not** a true GPU compute solver yet.
It is:
- a CPU fluid solver
- an optimized flat-array renderer
- a single EXE project with no startup-project confusion

That means it should already look cleaner and run much better than the earlier multi-project/GDI-heavy package, but it still will not fully exploit the RTX 3060s for CFD compute.

## Controls
- Left mouse: inject dye + force
- Right mouse: paint obstacle
- Shift + Right mouse: erase obstacle
- Mouse wheel: adjust vorticity confinement
- Space: pause/resume
- C: clear
- F: fullscreen
- H: toggle shading
- 1/2/3/4: render mode
- +/-: thread count

## Open
Open `GreatFluidDynamics.Rebuilt.sln` in Visual Studio 2022+ and run.

## Suggested first settings
- Grid: 384 x 216
- Threads: about half your logical cores first
- Pressure iterations: 18 to 24

On memory-bandwidth-heavy fluid solvers, maxing threads is not always faster.
