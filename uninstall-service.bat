@echo off
REM Uninstall HP Omen Keyboard RGB Service
REM Must be run as Administrator

echo Uninstalling HP Omen Keyboard RGB Service...
echo.

REM Check for admin rights
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script must be run as Administrator!
    echo Right-click this file and select "Run as administrator"
    pause
    exit /b 1
)

set SERVICE_NAME="HP Omen Keyboard RGB Service"

REM Check if service exists
sc query %SERVICE_NAME% >nul 2>&1
if %errorLevel% neq 0 (
    echo Service is not installed.
    pause
    exit /b 0
)

REM Get the install directory from the registered service binary path
set INSTALL_DIR=
for /f "tokens=3*" %%a in ('sc qc %SERVICE_NAME% ^| findstr "BINARY_PATH_NAME"') do set "INSTALL_DIR=%%a %%b"
REM Strip the executable filename to get the directory
for %%f in ("%INSTALL_DIR%") do set "INSTALL_DIR=%%~dpf"
REM Strip trailing backslash
if "%INSTALL_DIR:~-1%"=="\" set "INSTALL_DIR=%INSTALL_DIR:~0,-1%"

REM Stop the service
echo Stopping service...
sc stop %SERVICE_NAME%
timeout /t 2 >nul

REM Delete the service
echo Deleting service...
sc delete %SERVICE_NAME%

if %errorLevel% neq 0 (
    echo ERROR: Failed to delete service!
    pause
    exit /b 1
)

REM Remove install directory
if defined INSTALL_DIR if exist "%INSTALL_DIR%" (
    echo Removing %INSTALL_DIR%...
    rmdir /S /Q "%INSTALL_DIR%"
)

echo.
echo SUCCESS! Service uninstalled successfully.
echo.
pause
