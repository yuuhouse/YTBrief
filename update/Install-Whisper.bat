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
echo This installs faster-whisper and the CUDA runtime libraries for NVIDIA GPUs.
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
echo Installing faster-whisper, CUDA libraries, and compatible PyAV...
%PYTHON_CMD% -m pip install --upgrade faster-whisper av==16.1.0 nvidia-cublas-cu12 nvidia-cudnn-cu12 --progress-bar on
if errorlevel 1 (
  set "EXIT_CODE=%ERRORLEVEL%"
  goto END
)

echo.
echo Checking faster-whisper packages and NVIDIA GPU visibility...
%PYTHON_CMD% -c "import av, ctranslate2, faster_whisper, nvidia.cublas, nvidia.cudnn; count=ctranslate2.get_cuda_device_count(); print('PyAV:', av.__version__); print('CUDA devices:', count); assert count > 0, 'No CUDA device detected'"
if %ERRORLEVEL%==0 (
  echo Required packages are installed and an NVIDIA CUDA device is visible.
  echo Model CUDA initialization will be checked when transcription starts.
  goto END
)

echo Package or CUDA device check failed. Please check the messages above.
set "EXIT_CODE=1"
goto END

:DRYRUN
echo Would run:
echo   python -m pip install --upgrade pip --progress-bar on
echo   python -m pip install --upgrade faster-whisper av==16.1.0 nvidia-cublas-cu12 nvidia-cudnn-cu12 --progress-bar on
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
