@echo off
REM Install HP Omen Keyboard RGB Service
REM Must be run as Administrator

echo Installing HP Omen Keyboard RGB Service...
echo.

REM Check for admin rights
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script must be run as Administrator!
    echo Right-click this file and select "Run as administrator"
    pause
    exit /b 1
)

REM Get the current directory
set SERVICE_PATH=%~dp0OmenKeyboardService.exe
set SERVICE_NAME="HP Omen Keyboard RGB Service"

echo Service executable: %SERVICE_PATH%
echo.

REM Check if the executable exists
if not exist "%SERVICE_PATH%" (
    echo ERROR: Service executable not found!
    echo Please build the project first using: dotnet publish -c Release
    pause
    exit /b 1
)

REM Register the Event Log source for application logging
echo Registering Event Log source...
powershell -Command "if (-not [System.Diagnostics.EventLog]::SourceExists('HP Omen Keyboard RGB Service')) { [System.Diagnostics.EventLog]::CreateEventSource('HP Omen Keyboard RGB Service', 'Application'); Write-Host 'Event Log source registered successfully' } else { Write-Host 'Event Log source already exists' }"
echo.

REM Stop the service if it's already running
sc query %SERVICE_NAME% >nul 2>&1
if %errorLevel% equ 0 (
    echo Service already exists. Stopping...
    sc stop %SERVICE_NAME%
    timeout /t 2 >nul
    echo Deleting old service...
    sc delete %SERVICE_NAME%
    timeout /t 2 >nul
)

REM Create and start the service
echo Installing service...
sc create %SERVICE_NAME% binPath= "%SERVICE_PATH%" start= auto DisplayName= %SERVICE_NAME%

if %errorLevel% neq 0 (
    echo ERROR: Failed to create service!
    pause
    exit /b 1
)

echo.
echo Starting service...
sc start %SERVICE_NAME%

if %errorLevel% neq 0 (
    echo WARNING: Service created but failed to start.
    echo Check the Windows Event Viewer for error details.
    echo You can manually start it using: sc start %SERVICE_NAME%
) else (
    echo.
    echo SUCCESS! Service installed and started successfully.
)

echo.
echo The service will now start automatically when Windows boots.
echo You can edit config.json to change keyboard colors.
echo.
pause
