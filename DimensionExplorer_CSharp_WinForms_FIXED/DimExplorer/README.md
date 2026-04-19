# Dimension Explorer (C# WinForms)

This is a full C# desktop project inspired by the video **"1D 2D 3D 4D 5D 6D 7D 8D 9D 10D 11D 12D"**.

## What it does

- Lets you explore dimensions 1 through 12
- Renders a rotating **N-dimensional hypercube** projection
- Shows vertex and edge counts live
- Supports:
  - mouse drag to rotate
  - mouse wheel to zoom
  - auto rotation
  - reset view
  - randomized spin
  - optional vertex labels
  - optional XYZ axes

## Files

- `DimensionExplorer.csproj`
- `Program.cs`
- `MainForm.cs`
- `HypercubeModel.cs`

## How to run in Visual Studio

1. Extract the zip.
2. Open `DimensionExplorer.csproj` in Visual Studio 2022/2026.
3. Let Visual Studio restore packages if asked.
4. Press **F5**.

## Framework

- .NET 8
- Windows Forms

## Notes

- This is a **projection-based explorer**, not a full physics simulation.
- 8D through 12D get visually dense very quickly, which is expected.
- On your HP workstation this should be easy to run.
