@echo off
setlocal EnableExtensions
title Update OpenYTBrief Tools
chcp 65001 >nul

if /i "%~1"=="--help" goto HELP
if /i "%~1"=="/?" goto HELP
if /i "%~1"=="--dry-run" goto DRYRUN

echo Update OpenYTBrief Tools
echo.
echo Step 1/2: Update yt-dlp
set "OYTB_NO_PAUSE=1"
call "%~dp0Update-YtDlp.bat"
set "OYTB_NO_PAUSE="

echo.
echo Step 2/2: Install or update Whisper
set "OYTB_NO_PAUSE=1"
call "%~dp0Install-Whisper.bat"
set "OYTB_NO_PAUSE="
goto END

:DRYRUN
set "OYTB_NO_PAUSE=1"
call "%~dp0Update-YtDlp.bat" --dry-run
echo.
call "%~dp0Install-Whisper.bat" --dry-run
set "OYTB_NO_PAUSE="
goto END

:HELP
echo Update all OpenYTBrief tools
echo.
echo Usage:
echo   update\Update-All.bat
echo   update\Update-All.bat --dry-run
goto END

:END
echo.
echo Press any key to close this window.
pause >nul
endlocal
