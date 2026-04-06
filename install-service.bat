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

if "%~1"=="" (
    set INSTALL_DIR=%LOCALAPPDATA%\OmenKeyboardService
) else (
    set INSTALL_DIR=%~1
)
set PUBLISH_DIR=%~dp0bin\Release\net10.0\win-x64\publish
set SERVICE_NAME="HP Omen Keyboard RGB Service"

REM Check if the publish output exists
if not exist "%PUBLISH_DIR%\OmenKeyboardService.exe" (
    echo ERROR: Publish output not found at %PUBLISH_DIR%
    echo Please build the project first using: build.bat
    pause
    exit /b 1
)

REM Stop and delete existing service before copying files
sc query %SERVICE_NAME% >nul 2>&1
if %errorLevel% equ 0 (
    echo Service already exists. Stopping...
    sc stop %SERVICE_NAME%
    timeout /t 2 >nul
    echo Deleting old service...
    sc delete %SERVICE_NAME%
    timeout /t 2 >nul
)

REM Copy published files to install directory
echo Copying files to %INSTALL_DIR%...
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
xcopy /E /Y /Q "%PUBLISH_DIR%\*" "%INSTALL_DIR%\" >nul
if %errorLevel% neq 0 (
    echo ERROR: Failed to copy files!
    pause
    exit /b 1
)

REM Copy config.json if it exists in the project root and not already in the install dir
if not exist "%INSTALL_DIR%\config.json" (
    if exist "%~dp0config.json" (
        copy /Y "%~dp0config.json" "%INSTALL_DIR%\config.json" >nul
    )
)
echo.

REM Register the Event Log source for application logging
echo Registering Event Log source...
powershell -Command "if (-not [System.Diagnostics.EventLog]::SourceExists('HP Omen Keyboard RGB Service')) { [System.Diagnostics.EventLog]::CreateEventSource('HP Omen Keyboard RGB Service', 'Application'); Write-Host 'Event Log source registered successfully' } else { Write-Host 'Event Log source already exists' }"
echo.

REM Create and start the service
echo Installing service...
sc create %SERVICE_NAME% binPath= "%INSTALL_DIR%\OmenKeyboardService.exe" start= auto DisplayName= %SERVICE_NAME%

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
echo Install location: %INSTALL_DIR%
echo The service will now start automatically when Windows boots.
echo Edit %INSTALL_DIR%\config.json to change keyboard colors.
echo.
pause
