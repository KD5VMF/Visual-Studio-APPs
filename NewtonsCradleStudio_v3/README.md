# Newton's Cradle Studio

A polished WPF desktop Newton's cradle for Windows with mouse pull-and-release interaction.

## What it does

- Click and hold any ball.
- Pull it back.
- Release it to watch the momentum transfer through the cradle.
- Fast drags carry launch speed into the release.
- Slow drags behave like a calm hold-and-drop.

## Controls

- **Reset**: restores the cradle to rest.
- **Pause / Resume**: freezes or resumes the simulation.
- **Ideal transfer**: enables near-lossless energy transfer.
- **Energy loss**: used when ideal transfer is turned off.
- **Ball count**: 3 to 7 balls.
- **Time scale**: slows down or speeds up the simulation.

## Build

1. Open `NewtonsCradleStudio/NewtonsCradleStudio.csproj` in Visual Studio.
2. Let NuGet/package restore complete if prompted.
3. Press **F5**.

Target framework: **.NET 8 Windows (WPF)**

## Notes on the physics

This project uses a tuned pendulum + collision solver meant to feel clean and satisfying.
It is **near-ideal** rather than laboratory-grade rigid-body simulation.

The important parts are:

- each bob is constrained to a pendulum arc
- collisions transfer velocity along the line of impact
- overlap is corrected repeatedly per frame for stable contact
- drag release can inject tangential launch velocity

That gives the classic Newton's cradle behavior while keeping the app smooth and interactive.


Update:
- Added Fullscreen button and F11/Esc shortcuts.
- Improved drag release handling so the ball releases even if WPF misses the normal mouse-up path.
