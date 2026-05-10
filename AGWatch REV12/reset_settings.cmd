@echo off
setlocal
set SETTINGS=%USERPROFILE%\Documents\AGWatch\settings.json
if exist "%SETTINGS%" (
  copy "%SETTINGS%" "%SETTINGS%.backup"
  del "%SETTINGS%"
  echo Deleted current AGWatch settings and saved backup as settings.json.backup
) else (
  echo No AGWatch settings file found.
)
pause
