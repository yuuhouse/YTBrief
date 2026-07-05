@echo off
setlocal EnableExtensions
title Update yt-dlp for OpenYTBrief
chcp 65001 >nul

if /i "%~1"=="--help" goto HELP
if /i "%~1"=="/?" goto HELP
if /i "%~1"=="--dry-run" goto DRYRUN

set "ROOT_DIR=%~dp0.."
set "YTDLP=%ROOT_DIR%\yt-dlp.exe"

echo Update yt-dlp for OpenYTBrief
echo.

if not exist "%YTDLP%" (
  echo ERROR: Cannot find "%YTDLP%"
  goto END
)

"%YTDLP%" --update
goto END

:DRYRUN
echo Would run:
echo   "%~dp0..\yt-dlp.exe" --update
goto END

:HELP
echo Update yt-dlp for OpenYTBrief
echo.
echo Usage:
echo   update\Update-YtDlp.bat
echo   update\Update-YtDlp.bat --dry-run
goto END

:END
echo.
if "%OYTB_NO_PAUSE%"=="1" goto EXIT
echo Press any key to close this window.
pause >nul
:EXIT
endlocal
