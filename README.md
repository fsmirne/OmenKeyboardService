# HP Omen Sequencer Keyboard RGB Service

A cross-platform service that automatically controls the RGB LED colors on HP Omen keyboards. The service runs at system startup and applies custom color profiles based on a simple JSON configuration file.

**Supported Platforms:**
- ✅ Windows 10/11 (Windows Service)
- ✅ Linux (systemd service) - Ubuntu 20.04+, Debian, Fedora, Arch, etc.

## Features

- **Automatic Startup**: Runs as a system service (Windows Service or systemd) that starts when the OS boots
- **Cross-Platform**: Single codebase works on both Windows and Linux
- **USB Reconnection**: Automatically detects keyboard reconnection and restores colors when using KVM switches or replug events
- **Platform-Specific Features**:
  - **Windows**: Power management (sleep/wake), session lock/unlock detection, display power monitoring, user presence detection, WMI device monitoring
  - **Linux**: Power management (suspend/resume), device monitoring via /dev filesystem with smart keyboard detection
- **JSON Configuration**: Easy-to-edit configuration file for color profiles
- **Hot Reload**: Automatically detects config file changes and applies new colors
- **Key Groups**: Control multiple keys at once (WASD, arrows, function keys, etc.)
- **Individual Keys**: Set colors for specific keys
- **Multiple Profiles**: Easily switch between different color schemes

## Requirements

### Common Requirements
- HP Omen keyboard (USB VID: 0x03F0, PID: 0x1F41)
- .NET 10 SDK (for building) or Runtime (for running pre-built binaries)

### Windows Requirements
- Windows 10/11
- Administrator rights (for service installation)

### Linux Requirements
- Ubuntu 20.04+ or any Linux distribution with systemd
- Root/sudo access (for service installation and HID device access)

## Installation

Choose your platform:
- [Windows Installation](#windows-installation)
- [Linux Installation](#linux-installation)

### Windows Installation

#### Step 1: Build the Project

Open a command prompt or PowerShell in the project directory and run:

```bash
build.bat
```

Or manually:

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

The compiled service will be in: `bin\Release\net10.0\win-x64\publish\`

#### Step 2: Copy Files

Copy the following files from the publish folder to a permanent location (e.g., `C:\Program Files\OmenKeyboardService\`):

- `OmenKeyboardService.exe`
- `config.json`
- `install-service.bat`
- Other `.bat` scripts (optional, for service management)

#### Step 3: Install the Service

1. Navigate to the folder containing the files
2. Right-click `install-service.bat` and select **"Run as administrator"**
3. The installation script will:
   - Register the Event Log source for application logging
   - Install the Windows service
   - Start the service automatically

The service is configured to start automatically at boot and will monitor for power events, session changes, and device reconnection.

### Linux Installation

#### Step 1: Install .NET 10 SDK

If you don't have .NET 10 SDK installed:

```bash
# Ubuntu/Debian
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0

# Fedora
sudo dnf install dotnet-sdk-10.0

# Arch
yay -S dotnet-sdk
```

For other distributions, see: https://docs.microsoft.com/en-us/dotnet/core/install/linux

#### Step 2: Build the Project

```bash
chmod +x build.sh
./build.sh
```

Or manually:

```bash
dotnet publish -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true
```

The compiled service will be in: `bin/Release/net10.0/linux-x64/publish/`

#### Step 3: Install the Service

Navigate to the publish directory and run the install script:

```bash
cd bin/Release/net10.0/linux-x64/publish/
chmod +x install-service.sh
sudo ./install-service.sh
```

The installation script will:
- Copy files to `/usr/local/bin/omen-keyboard-rgb/`
- Create udev rules for keyboard access
- Install and start the systemd service
- Enable the service to start at boot

## Configuration

### Config File Location

The service looks for `config.json` in the same directory as the executable:

- **Windows**: Same folder as `OmenKeyboardService.exe` (e.g., `C:\Program Files\OmenKeyboardService\config.json`)
- **Linux**: `/usr/local/bin/omen-keyboard-rgb/config.json`

### Basic Configuration

Edit `config.json` to set your desired colors:

```json
{
  "profileName": "Gaming",
  "logLevel": "Warning",
  "profile": {
    "fps": "FF0000",
    "arrows": "FF0000",
    "fkeys": "0099FF",
    "pkeys": "FF00FF",
    "media": "FFFF00",
    "numpad": "00FFFF",
    "windows": "FF6600"
  }
}
```

### Log Level Configuration

The `logLevel` field controls how much detail the service logs. This is useful for reducing Event Log clutter in normal operation while still being able to enable detailed logging for debugging.

**Available log levels:**
- `Warning` (Default) - Only logs warnings and errors. Recommended for normal use to keep Event Logs clean.
- `Information` - Logs all operations including startup, config changes, session events, and color applications. Use this when debugging issues.
- `Error` - Only logs critical errors. Minimal logging.
- `Debug` - Very detailed logging including internal operations. Very verbose, use only for deep debugging.

**To change the log level:**
1. Edit `config.json` and change the `logLevel` value
2. Stop and restart the service for the change to take effect

**Example - Enable detailed logging for debugging:**
```json
{
  "profileName": "Gaming",
  "logLevel": "Information",
  "profile": {
    ...
  }
}
```

**Note:** The log level change requires a service restart. After changing the log level in `config.json`, you must stop and start the service using the service management commands or scripts.

### Available Key Groups

| Group Name | Description | Keys |
|------------|-------------|------|
| `fps` | WASD keys | W, A, S, D |
| `arrows` | Arrow keys | Up, Down, Left, Right |
| `fkeys` | Function keys | F1-F12 |
| `pkeys` | Programmable keys | P1-P5 |
| `media` | Media controls | Play, Stop, Next, Previous |
| `numpad` | Numeric keypad | All numpad keys including Enter |
| `system` | System keys | PrtScr, ScrLock, Pause, Insert, Home, PgUp, Delete, End, PgDn |
| `windows` | Windows key | Windows key only |
| `all` | All keys | Default color for all keys |

### Color Format

Colors are specified in hexadecimal RGB format: `RRGGBB`

Examples:
- `FF0000` - Red
- `00FF00` - Green
- `0000FF` - Blue
- `FFFF00` - Yellow
- `FF00FF` - Magenta
- `00FFFF` - Cyan
- `FFFFFF` - White
- `000000` - Black (off)

### Example Profiles

See `config.examples.json` for ready-to-use color profiles:

- **Gaming** - Red WASD and arrows, blue function keys
- **Work** - Calm blue tones
- **Cyberpunk** - Purple and pink
- **Hacker** - Matrix green
- **Sunset** - Orange and red gradient
- **Rainbow** - Different colors for each key group
- **Fire** - Red/orange/yellow theme
- **Ice** - Blue/cyan theme

### Setting Individual Key Colors

You can also set colors for individual keys:

```json
{
  "profileName": "Custom",
  "profile": {
    "w": "FF0000",
    "a": "00FF00",
    "s": "0000FF",
    "d": "FFFF00",
    "esc": "FF00FF",
    "enter": "00FFFF"
  }
}
```

### Applying Changes

Just save `config.json` - the service automatically detects changes and applies the new colors within 1 second. No need to restart the service!

## Service Management

### Windows Service Management

#### Using Batch Scripts (Recommended)

All scripts must be run as Administrator (right-click → "Run as administrator"):

- **`install-service.bat`** - Install and start the service
- **`uninstall-service.bat`** - Stop and remove the service
- **`start-service.bat`** - Start the service
- **`stop-service.bat`** - Stop the service

#### Using Windows Services Manager

1. Press `Win + R`, type `services.msc`, press Enter
2. Find "HP Omen Keyboard RGB Service"
3. Right-click for options: Start, Stop, Restart, Properties

#### Using Command Line

```cmd
# Install
sc create "HP Omen Keyboard RGB Service" binPath="C:\Path\To\OmenKeyboardService.exe" start=auto

# Start
sc start "HP Omen Keyboard RGB Service"

# Stop
sc stop "HP Omen Keyboard RGB Service"

# Uninstall
sc delete "HP Omen Keyboard RGB Service"

# Check status
sc query "HP Omen Keyboard RGB Service"
```

### Linux Service Management

#### Using Shell Scripts

All scripts must be run with sudo:

- **`sudo ./install-service.sh`** - Install and start the service
- **`sudo ./uninstall-service.sh`** - Stop and remove the service
- **`sudo ./start-service.sh`** - Start the service
- **`sudo ./stop-service.sh`** - Stop the service

#### Using systemctl Commands

```bash
# Start the service
sudo systemctl start omen-keyboard-rgb

# Stop the service
sudo systemctl stop omen-keyboard-rgb

# Restart the service
sudo systemctl restart omen-keyboard-rgb

# Check status
sudo systemctl status omen-keyboard-rgb

# Enable service at boot
sudo systemctl enable omen-keyboard-rgb

# Disable service at boot
sudo systemctl disable omen-keyboard-rgb

# View logs in real-time
sudo journalctl -u omen-keyboard-rgb -f

# View recent logs
sudo journalctl -u omen-keyboard-rgb -n 50
```

## Troubleshooting

### Common Issues (All Platforms)

#### Service Won't Start

1. **Check keyboard is connected**: Make sure your HP Omen keyboard is plugged in

   ```bash
   # Windows: Check Device Manager
   # Linux: Check USB devices
   lsusb | grep 03f0:1f41
   ```

2. **Verify config file**: Make sure `config.json` exists in the correct location
   - Windows: Same folder as `OmenKeyboardService.exe`
   - Linux: `/usr/local/bin/omen-keyboard-rgb/config.json`

3. **Check service logs**:
   - Windows: Event Viewer (see below)
   - Linux: `sudo journalctl -u omen-keyboard-rgb -n 50`

### Windows-Specific Troubleshooting

#### Check Event Viewer

1. Press `Win + R`, type `eventvwr.msc`, press Enter
2. Navigate to Windows Logs → Application
3. Look for errors from "HP Omen Keyboard RGB Service"

#### Event Viewer PowerShell Commands

```powershell
# View last 20 log entries
Get-EventLog -LogName Application -Source "HP Omen Keyboard RGB Service" -Newest 20

# View only errors and warnings
Get-EventLog -LogName Application -Source "HP Omen Keyboard RGB Service" -EntryType Error,Warning -Newest 10

# View logs since last hour
Get-EventLog -LogName Application -Source "HP Omen Keyboard RGB Service" -After (Get-Date).AddHours(-1)
```

### Linux-Specific Troubleshooting

#### Check Service Status

```bash
sudo systemctl status omen-keyboard-rgb
```

#### View Detailed Logs

```bash
# View recent logs
sudo journalctl -u omen-keyboard-rgb -n 50

# Follow logs in real-time
sudo journalctl -u omen-keyboard-rgb -f

# View logs since last boot
sudo journalctl -u omen-keyboard-rgb -b
```

#### Check Permissions

Verify udev rules are installed:

```bash
cat /etc/udev/rules.d/99-omen-keyboard.rules
```

Reload udev rules if needed:

```bash
sudo udevadm control --reload-rules
sudo udevadm trigger
```

### Colors Not Applying

1. **Check config syntax**: Ensure `config.json` is valid JSON (use a JSON validator)
2. **Check color format**: Colors must be 6-digit hex (e.g., `FF0000`, not `#FF0000`)
3. **Check key names**: Key group names are case-sensitive (use lowercase)
4. **Restart service**: Try stopping and starting the service manually

### Keyboard Not Found Error

The service only works with HP Omen keyboards (VID: 0x03F0, PID: 0x1F41). If you have a different keyboard model, this service won't work.

### Permission Denied

The service needs to run with appropriate permissions to access HID devices. Make sure:
- Service is installed with administrator rights
- Service account has permission to access hardware

### Platform-Specific Features

#### Windows: Colors Reset After Sleep/Wake

The service automatically monitors power events and restores colors when the computer wakes from sleep. The service waits 2 seconds after wake to allow hardware to initialize, then retries up to 5 times if the keyboard isn't ready.

If colors don't persist after sleep:

1. **Check Event Viewer**: Look for "System resumed from sleep" messages
2. **Verify service is running**: Check `services.msc`
3. **Manual trigger**: Edit and save `config.json` to trigger a manual reapplication

#### Windows: Colors Reset After Lock/Unlock

The service monitors Windows session events and automatically restores colors when you unlock your session.

If colors don't restore after unlocking:

1. **Check Event Viewer**: Look for "Session unlocked" followed by "Successfully applied colors" messages
2. **Verify service is running**: The service must run under LocalSystem or an account with session monitoring permissions
3. **Manual trigger**: Edit and save `config.json` to manually reapply colors

#### Windows: Colors Reset After Screen Turns Off (No Sleep)

The service monitors user presence detection and display power state changes. When your screen turns off due to inactivity but the computer doesn't sleep, the service automatically restores colors when you return and wake the display.

This feature handles scenarios where:
- Screen timeout occurs (user idle/away)
- User returns and moves mouse or presses a key
- Display powers back on

If colors don't restore when returning from screen timeout:

1. **Check Event Viewer**: Look for "User presence detected (returned from idle)" or "Display powered ON" messages
2. **Verify service is running**: Check `services.msc`
3. **Check power settings**: Ensure Windows is configured to turn off the display after inactivity (Settings → System → Power & sleep)
4. **Manual trigger**: Edit and save `config.json` to manually reapply colors

#### Linux: Colors Reset After Suspend/Resume

The service monitors system suspend/resume events by watching `/sys/power/wakeup_count` for changes. When the system resumes from suspend, the service automatically restores colors after a 2-second delay to allow hardware to initialize.

If colors don't restore after suspend/resume:

1. **Check logs**: Look for "System resumed from suspend" messages
   ```bash
   sudo journalctl -u omen-keyboard-rgb -n 20 | grep -i "resumed\|suspend"
   ```
2. **Verify wakeup_count is accessible**: The service monitors `/sys/power/wakeup_count`
   ```bash
   cat /sys/power/wakeup_count
   ```
3. **Verify service is running**: Check `sudo systemctl status omen-keyboard-rgb`
4. **Manual trigger**: Edit and save `config.json` to manually reapply colors

### KVM Switch / USB Reconnection (All Platforms)

The service automatically detects keyboard reconnection and restores colors. This works for:

- **KVM switches**: When switching between computers
- **USB replug**: When unplugging and replugging the keyboard
- **USB hub changes**: When moving the keyboard to a different USB port

**Windows**: Uses WMI events to instantly detect the HP Omen keyboard specifically (within 500ms)
**Linux**: Monitors /dev/hidraw* for new device creation, verifies via sysfs that it's the HP Omen keyboard (VID:03F0, PID:1F41) before triggering (within 1.5 seconds)

If colors don't restore after reconnection:

1. **Check logs**:
   - Windows: Event Viewer for "HP Omen keyboard reconnected" messages
   - Linux: `sudo journalctl -u omen-keyboard-rgb -f` for "HP Omen keyboard detected" messages
2. **Check USB power**: Some KVM switches may not provide adequate power to the keyboard
3. **Manual trigger**: Edit and save `config.json` to manually reapply colors

## Uninstallation

### Windows

1. Run `uninstall-service.bat` as Administrator
2. Delete the service folder

Or manually:

```cmd
sc stop "HP Omen Keyboard RGB Service"
sc delete "HP Omen Keyboard RGB Service"
```

### Linux

Run the uninstall script:

```bash
cd /usr/local/bin/omen-keyboard-rgb
sudo ./uninstall-service.sh
```

Or manually:

```bash
sudo systemctl stop omen-keyboard-rgb
sudo systemctl disable omen-keyboard-rgb
sudo rm /etc/systemd/system/omen-keyboard-rgb.service
sudo systemctl daemon-reload
sudo rm -rf /usr/local/bin/omen-keyboard-rgb
sudo rm /etc/udev/rules.d/99-omen-keyboard.rules
sudo udevadm control --reload-rules
sudo udevadm trigger
```

## Technical Details

### Architecture

The service uses a cross-platform architecture with platform-specific implementations:

- **Common Code**: `KeyboardRgbService`, `OmenKeyboardController`, HID communication
- **Platform Abstraction**: `IPlatformService` interface
- **Windows Implementation**: `WindowsPlatformService` - WMI device monitoring, power events (sleep/wake), session events (lock/unlock/logon), display power notifications, user presence detection
- **Linux Implementation**: `LinuxPlatformService` - /dev filesystem monitoring with keyboard-specific filtering, suspend/resume detection via sysfs wakeup_count monitoring

### How It Works

1. Service starts at system boot (Windows Service or systemd)
2. Detects platform and loads appropriate platform service
3. Reads `config.json` to get color mappings
4. Connects to HP Omen keyboard via HID protocol using HidSharp library
5. Sends RGB color commands to keyboard firmware
6. Monitors config file for changes and reloads automatically
7. Monitors platform-specific events:
   - **Windows**: Power events (sleep/wake), session events (lock/unlock), display power state changes, user presence detection (idle/active), WMI USB device arrival
   - **Linux**: Power events (suspend/resume via wakeup_count), /dev/hidraw* device creation with keyboard-specific filtering via sysfs
8. Automatically restores colors when keyboard reconnects (KVM switches, USB replug, etc.)

### HID Protocol

The service uses the HID (Human Interface Device) protocol to communicate with the keyboard:
- Vendor ID: 0x03F0 (HP)
- Product ID: 0x1F41 (Omen keyboard)
- Commands are sent as 64-byte HID output reports
- RGB data is split across 9 command packets (3 per color channel)

### Logging

The service logs important events and errors. The log verbosity is controlled by the `logLevel` setting in `config.json` (default: `Warning`). See [Log Level Configuration](#log-level-configuration) for details.

#### Windows Logging

Logs are written to Windows Event Log. The installation script automatically registers the Event Log source.

**Log Location:**
- Source: `HP Omen Keyboard RGB Service`
- Log Name: `Application`
- Minimum Level: Configurable via `logLevel` in `config.json` (default: Warning)

**View logs in Event Viewer:**
1. Press `Win + R`, type `eventvwr.msc`, press Enter
2. Navigate to Windows Logs → Application
3. Look for events with source "HP Omen Keyboard RGB Service"

**View logs in PowerShell:**
```powershell
# View last 20 log entries
Get-EventLog -LogName Application -Source "HP Omen Keyboard RGB Service" -Newest 20

# View only errors and warnings
Get-EventLog -LogName Application -Source "HP Omen Keyboard RGB Service" -EntryType Error,Warning -Newest 10
```

#### Linux Logging

Logs are written to console output, which is captured by systemd journal.

**View logs:**
```bash
# View all logs
sudo journalctl -u omen-keyboard-rgb

# View last 50 entries
sudo journalctl -u omen-keyboard-rgb -n 50

# Follow logs in real-time
sudo journalctl -u omen-keyboard-rgb -f

# View logs since last boot
sudo journalctl -u omen-keyboard-rgb -b

# View only errors and warnings
sudo journalctl -u omen-keyboard-rgb -p err..warning

# Export logs to file
sudo journalctl -u omen-keyboard-rgb > keyboard-service.log
```

#### Important Log Messages

Look for these messages in the logs (both platforms):
- `HP Omen Keyboard RGB Service starting on [Platform]...` - Service initialization
- `Service started successfully. Monitoring for config changes and platform events...` - Service ready
- `Applying profile: [ProfileName]` - Loading configuration
- `Successfully applied colors to keyboard. Groups configured: X` - Colors applied
- Platform-specific events:
  - Windows: `System resumed from sleep`, `Session unlocked`, `Display powered ON`, `User presence detected (returned from idle)`, `HP Omen keyboard reconnected (KVM switch or USB replug detected)`
  - Linux: `System resumed from suspend`, `HP Omen keyboard detected`, `New HID device detected`
- `Successfully applied colors on attempt X` - Colors applied after retry
- `Failed to apply colors after X attempts` - All retries exhausted

## Credits

Based on the Rust library [lights-for-omen-sequencer](https://github.com/slysherz/lights-for-omen-sequencer)

Originally ported to C# as Sequencer.linq, then converted to a Windows service.

## License

This project is provided as-is for personal use with HP Omen keyboards.
