# Helix Solar Show HD

A fullscreen C# / OpenTK solar-system helix visualizer for Windows 11, tuned as a fun long-running display piece rather than a scientific ephemeris viewer.

## What this repo contains

- Full Visual Studio solution
- .NET 8 Windows desktop project
- OpenTK 4.9.4 rendering path
- HD visual pass with denser stars, richer shading, atmospheric glow, and stronger sun corona effects
- Automatic forever-running cinematic show
- Exit keys: `Q` / `Esc`
- Crash logging to `helix-show-log.txt`
- Publish and run helper scripts
- GitHub Actions workflow for Windows build validation

## Repo layout

```text
HelixSolarShow.sln
src/HelixSolarShow/
  HelixSolarShow.csproj
  Program.cs
  CrashReporter.cs
  ShaderProgram.cs
  PrimitiveMeshes.cs
  HelixShowWindow.cs
RUN_HELIX_SOLAR_SHOW.bat
PUBLISH_WIN64.ps1
docs/screenshots/
```

## Open in Visual Studio

1. Open `HelixSolarShow.sln`
2. Let NuGet restore packages
3. Build `Release`
4. Run

## Quick run from terminal

```powershell
dotnet run --project .\src\HelixSolarShow\HelixSolarShow.csproj -c Release
```

Or double-click:

- `RUN_HELIX_SOLAR_SHOW.bat`

## Publish

```powershell
.\PUBLISH_WIN64.ps1
```

## Controls

- `Q` = Quit
- `Esc` = Quit

There are no on-screen controls by design. This version is meant to be dropped in and watched.

## Notes

- Target framework: `net8.0-windows`
- Rendering: modern OpenGL via OpenTK
- Intended use: cinematic / screensaver-like visualization
- Not a strict real-world orbital mechanics simulator

## Suggested GitHub upload flow

```powershell
git init
git add .
git commit -m "Initial commit: Helix Solar Show HD"
git branch -M main
git remote add origin https://github.com/YOURNAME/HelixSolarShow-HD.git
git push -u origin main
```

## Screenshots

See:

- `docs/screenshots/showcase_01.jpg`
- `docs/screenshots/showcase_02.jpg`

## Honest note

This repo package was assembled from the last working project tree in the workspace. I did not compile-test it in this environment because the container does not have `dotnet` installed.
