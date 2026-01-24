@echo off
REM Build HP Omen Keyboard RGB Service

echo Building HP Omen Keyboard RGB Service...
echo.

REM Check if dotnet is installed
dotnet --version >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: .NET SDK is not installed!
    echo Please download and install .NET 8.0 SDK from:
    echo https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo Building self-contained executable...
echo.

dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true

if %errorLevel% neq 0 (
    echo.
    echo ERROR: Build failed!
    pause
    exit /b 1
)

echo.
echo ============================================
echo Build completed successfully!
echo ============================================
echo.
echo Output location:
echo bin\Release\net8.0-windows\win-x64\publish\
echo.
echo Next steps:
echo 1. Copy the contents of the publish folder to a permanent location
echo    (e.g., C:\Program Files\OmenKeyboardService\)
echo 2. Copy install-service.bat to the same location
echo 3. Run install-service.bat as Administrator
echo.
pause
