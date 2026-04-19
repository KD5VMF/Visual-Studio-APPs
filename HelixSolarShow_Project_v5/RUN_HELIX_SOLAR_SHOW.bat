@echo off
setlocal
cd /d "%~dp0src\HelixSolarShow"
dotnet run -c Release
