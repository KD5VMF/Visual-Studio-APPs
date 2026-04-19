# LifeForge Accelerated

This is a full rewrite of the prior WinForms/GDI version.

## What changed

- OpenGL renderer through **OpenTK** instead of WinForms/GDI drawing
- multithreaded world updates using `Parallel.For`
- HUD and text are rendered into a texture and refreshed periodically instead of every frame, which should stop the visible text flashing/shimmering
- fullscreen button in the UI, plus **F11** and **Esc**
- worker thread count can be adjusted with **[** and **]**
- simulation speed can be adjusted with **-** and **+**

## Controls

- **Pause/Resume** button or **Space**
- **Reset** button or **R**
- **Fullscreen** button or **F11**
- **Esc** leaves fullscreen
- **Help** button or **H**
- **- / +** change simulation speed
- **[ / ]** change worker thread count
- Click a creature or object to inspect it

## Important note about your two RTX 3060 cards

This build uses one GPU for the actual rendering window.
That is normal for a single OpenGL windowed app on Windows.
A future step could move the neural-network/inference part to a compute path so the second GPU can do useful work too, but that is a different architecture step.

## Build

Open the `.csproj` in Visual Studio 2022 or later and restore NuGet packages.
Then build and run normally.

## Honest note

This source was written carefully but not compiled inside the container because the container does not have the .NET SDK installed.
If Visual Studio reports any compile error at all, send the exact error text and it can be patched directly.


Patch notes for fix2:
- Explicit MousePosition float casts for OpenTK API compatibility.
- PixelFormat aliases retained to avoid System.Drawing/OpenGL ambiguity.
- RenderFrequency assignment remains removed.


Patch notes for final button/window fix:
- Switched HUD, world sizing, and overlay rebuilds to the window client area instead of the outer window size.
- Added more robust mouse click handling so UI buttons still work even if the mouse Y origin is reported from the bottom.
- Fullscreen restore now saves and restores the client size used by the simulation.
