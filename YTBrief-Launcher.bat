@echo off
setlocal EnableExtensions
title YTBrief Launcher
chcp 65001 >nul

set "SCRIPT_DIR=%~dp0"
set "ROOT_DIR=%SCRIPT_DIR%"
set "RUN_MODE="
set "APP_EXE="
set "PROJECT_FILE="

if exist "%SCRIPT_DIR%OpenYTBrief.csproj" (
  set "ROOT_DIR=%SCRIPT_DIR%"
  set "PROJECT_FILE=%SCRIPT_DIR%OpenYTBrief.csproj"
)

if not defined PROJECT_FILE if exist "%SCRIPT_DIR%YTBrief.csproj" (
  set "ROOT_DIR=%SCRIPT_DIR%"
  set "PROJECT_FILE=%SCRIPT_DIR%YTBrief.csproj"
)

if not defined PROJECT_FILE if exist "%SCRIPT_DIR%..\OpenYTBrief.csproj" (
  set "ROOT_DIR=%SCRIPT_DIR%..\"
  set "PROJECT_FILE=%SCRIPT_DIR%..\OpenYTBrief.csproj"
)

if not defined PROJECT_FILE if exist "%SCRIPT_DIR%..\YTBrief.csproj" (
  set "ROOT_DIR=%SCRIPT_DIR%..\"
  set "PROJECT_FILE=%SCRIPT_DIR%..\YTBrief.csproj"
)

if exist "%SCRIPT_DIR%bin\Debug\net8.0-windows\OpenYTBrief.exe" (
  set "RUN_MODE=exe"
  set "APP_EXE=%SCRIPT_DIR%bin\Debug\net8.0-windows\OpenYTBrief.exe"
)

if not defined RUN_MODE if exist "%SCRIPT_DIR%bin\Release\net8.0-windows\OpenYTBrief.exe" (
  set "RUN_MODE=exe"
  set "APP_EXE=%SCRIPT_DIR%bin\Release\net8.0-windows\OpenYTBrief.exe"
)

if not defined RUN_MODE if exist "%ROOT_DIR%publish\OpenYTBrief.exe" (
  set "RUN_MODE=exe"
  set "APP_EXE=%ROOT_DIR%publish\OpenYTBrief.exe"
)

if not defined RUN_MODE if exist "%SCRIPT_DIR%OpenYTBrief.exe" (
  set "RUN_MODE=exe"
  set "APP_EXE=%SCRIPT_DIR%OpenYTBrief.exe"
)

if not defined RUN_MODE if defined PROJECT_FILE (
  set "RUN_MODE=dotnet"
)

echo YTBrief Launcher
echo.

if not defined RUN_MODE (
  echo ERROR: Cannot find OpenYTBrief.exe or OpenYTBrief.csproj.
  echo Put this launcher in the project root or the publish folder.
  goto END
)

set "YT_URL="
set /p "YT_URL=Paste a YouTube or Bilibili URL then press Enter: "

if not defined YT_URL (
  echo.
  echo No URL entered.
  goto END
)

echo.
cd /d "%ROOT_DIR%"
if "%RUN_MODE%"=="exe" (
  "%APP_EXE%" "%YT_URL%"
) else (
  dotnet run --project "%PROJECT_FILE%" -- "%YT_URL%"
)
set "EXIT_CODE=%ERRORLEVEL%"

echo.
echo YTBrief exited with code %EXIT_CODE%.

:END
echo.
if /i "%OYTB_NO_PAUSE%"=="1" goto EXIT
echo Press any key to close this window.
pause >nul
:EXIT
endlocal
