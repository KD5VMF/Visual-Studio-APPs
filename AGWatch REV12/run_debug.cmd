@echo off
setlocal
cd /d "%~dp0"

echo ==== BUILD START ==== > build_log_REV12.txt
dotnet --info >> build_log_REV12.txt 2>&1
dotnet clean AGWatch\AGWatch.csproj -c Release >> build_log_REV12.txt 2>&1
dotnet build AGWatch\AGWatch.csproj -c Release >> build_log_REV12.txt 2>&1

if errorlevel 1 (
  echo.
  echo Build failed. Send build_log_REV12.txt
  pause
  exit /b 1
)

echo ==== RUN START ==== > run_log_REV12.txt
"AGWatch\bin\Release\net8.0-windows\AGWatch_REV12.exe" >> run_log_REV12.txt 2>&1

echo.
echo Send these if it fails:
echo build_log_REV12.txt
echo run_log_REV12.txt
echo Documents\AGWatch\fatal_error_REV12.txt
pause
