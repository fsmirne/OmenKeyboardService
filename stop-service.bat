@echo off
REM Stop HP Omen Keyboard RGB Service
REM Must be run as Administrator

echo Stopping HP Omen Keyboard RGB Service...
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

sc stop %SERVICE_NAME%

if %errorLevel% neq 0 (
    echo ERROR: Failed to stop service!
    pause
    exit /b 1
)

echo.
echo Service stopped successfully!
echo.
pause
