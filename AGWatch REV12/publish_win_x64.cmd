@echo off
setlocal
cd /d "%~dp0"

echo Publishing AGWatch REV12 for Windows x64...
if exist publish\win-x64 rmdir /s /q publish\win-x64

dotnet publish AGWatch\AGWatch.csproj -c Release -r win-x64 --self-contained false -o publish\win-x64
if errorlevel 1 (
  echo.
  echo Publish failed.
  pause
  exit /b 1
)

echo.
echo Publish complete:
echo %CD%\publish\win-x64
pause
