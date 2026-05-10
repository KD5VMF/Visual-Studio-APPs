@echo off
setlocal
cd /d "%~dp0"
echo Building AGWatch REV12...
dotnet clean AGWatch\AGWatch.csproj -c Release
dotnet build AGWatch\AGWatch.csproj -c Release
if errorlevel 1 (
  echo.
  echo Build failed.
  pause
  exit /b 1
)
echo.
echo Starting AGWatch REV12...
start "" "AGWatch\bin\Release\net8.0-windows\AGWatch_REV12.exe"
pause
