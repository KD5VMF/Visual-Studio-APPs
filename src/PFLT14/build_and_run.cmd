@echo off
setlocal EnableExtensions
title PFLT14 - pfSense Live Telemetry Rev14

REM Robust launcher: works even if this .cmd is launched from Explorer, PowerShell, or a nested extracted folder.
set "ROOT=%~dp0"
set "PROJECT="

if exist "%ROOT%PFLT14.csproj" set "PROJECT=%ROOT%PFLT14.csproj"
if not defined PROJECT if exist "%ROOT%PFLT14.csproj" set "PROJECT=%ROOT%PFLT14.csproj"

if not defined PROJECT (
  for /f "delims=" %%F in ('dir /b /s "%ROOT%*.csproj" 2^>nul') do (
    set "PROJECT=%%F"
    goto :foundProject
  )
)

:foundProject
if not defined PROJECT (
  echo ERROR: Could not find PFLT14.csproj under:
  echo %ROOT%
  echo.
  echo Make sure you extracted the whole ZIP first, then run this file from the extracted folder.
  pause
  exit /b 1
)

for %%I in ("%PROJECT%") do set "PROJDIR=%%~dpI"
cd /d "%PROJDIR%" || (
  echo ERROR: Could not change to project folder:
  echo %PROJDIR%
  pause
  exit /b 1
)

echo Project found:
echo %PROJECT%
echo.
echo Building pfSense Live Telemetry Rev14 PFLT14...
dotnet restore "%PROJECT%"
if errorlevel 1 pause & exit /b 1
dotnet build "%PROJECT%" -c Release
if errorlevel 1 pause & exit /b 1
echo.
echo Starting PFLT14...
dotnet run --project "%PROJECT%" -c Release
pause
