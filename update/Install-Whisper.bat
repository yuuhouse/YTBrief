@echo off
setlocal EnableExtensions
title Install faster-whisper for OpenYTBrief
chcp 65001 >nul

if /i "%~1"=="--help" goto HELP
if /i "%~1"=="/?" goto HELP
if /i "%~1"=="--dry-run" goto DRYRUN

set "PYTHON_CMD="
set "EXIT_CODE=0"
set "PYTHONIOENCODING=utf-8"
set "PYTHONUTF8=1"

where python >nul 2>nul
if %ERRORLEVEL%==0 set "PYTHON_CMD=python"

if not defined PYTHON_CMD (
  where py >nul 2>nul
  if %ERRORLEVEL%==0 set "PYTHON_CMD=py"
)

echo Install Whisper for OpenYTBrief
echo.
echo This installs faster-whisper into your Python environment.
echo The model itself is downloaded on first transcription and can take several minutes.
echo.

if not defined PYTHON_CMD (
  echo ERROR: Python was not found. Install Python 3.10+ first.
  set "EXIT_CODE=1"
  goto END
)

echo Python command: %PYTHON_CMD%
%PYTHON_CMD% --version
echo.

if not "%OYTB_NO_PAUSE%"=="1" pause

echo.
echo Updating pip...
%PYTHON_CMD% -m pip install --upgrade pip --progress-bar on
if errorlevel 1 (
  set "EXIT_CODE=%ERRORLEVEL%"
  goto END
)

echo.
echo Installing or updating faster-whisper...
%PYTHON_CMD% -m pip install --upgrade faster-whisper --progress-bar on
if errorlevel 1 (
  set "EXIT_CODE=%ERRORLEVEL%"
  goto END
)

echo.
echo Checking faster-whisper...
%PYTHON_CMD% -c "import faster_whisper" >nul 2>nul
if %ERRORLEVEL%==0 (
  echo faster-whisper Python module is available.
  goto END
)

echo faster-whisper installation did not complete successfully. Please check the messages above.
set "EXIT_CODE=1"
goto END

:DRYRUN
echo Would run:
echo   python -m pip install --upgrade pip --progress-bar on
echo   python -m pip install --upgrade faster-whisper --progress-bar on
goto END

:HELP
echo Install Whisper for OpenYTBrief
echo.
echo Usage:
echo   update\Install-Whisper.bat
echo   update\Install-Whisper.bat --dry-run
goto END

:END
echo.
if "%OYTB_NO_PAUSE%"=="1" goto EXIT
echo Press any key to close this window.
pause >nul
:EXIT
endlocal
exit /b %EXIT_CODE%
