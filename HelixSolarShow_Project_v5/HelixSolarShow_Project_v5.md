# Helix Solar Show

A standalone C# / OpenTK fullscreen space animation for Windows 11.

This build is intentionally simple:
- no GUI panels
- no user controls on screen
- automatic cinematic camera
- helical solar-system motion for a fun, hypnotic "watch forever" display
- hidden **Esc** key to exit if needed

## What it does

- The Sun moves forward through space.
- Planets and selected moons orbit while the system advances, creating long helical trails.
- The camera drifts automatically around the system forever.
- Bodies are shaded as real 3D spheres with lighting.

## Open in Visual Studio

Open:
- `HelixSolarShow.sln`

Then:
1. Let NuGet restore packages.
2. Build **Release**.
3. Run.

## Notes

- Target: `.NET 8` on Windows.
- Rendering: `OpenTK 4.9.4` using modern OpenGL.
- Hidden fallback control: **Esc** closes the program.
- Crash log path: next to the EXE as `helix-show-log.txt`

## Publish

Use the included script:
- `PUBLISH_WIN64.ps1`

## Run helper

Double-click:
- `RUN_HELIX_SOLAR_SHOW.bat`


Exit keys: `Esc` or `Q`


## v4 changes

- Camera never locks to a moon; moon showcases are framed around the parent planet.
- Dynamic automatic speed profile now ranges from slow readable moments to very fast outer-system sweeps.
- Wider Sun-centered shots make Jupiter and Saturn motion around the Sun more obvious.
- Longer trail history for a richer continuous screensaver feel.


## HD graphics revision

This revision increases sphere detail, MSAA, atmospheric glow, sun corona billboards, a richer nebula background, denser stars, and stronger procedural planet shading while keeping the same automatic helix-show behavior.
