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

echo.
echo SUCCESS! Service uninstalled successfully.
echo.
pause
