# HP Omen Sequencer Keyboard RGB Service

A Windows service that automatically controls the RGB LED colors on HP Omen keyboards. The service runs at Windows startup and applies custom color profiles based on a simple JSON configuration file.

## Features

- **Automatic Startup**: Runs as a Windows service that starts when Windows boots (before user login)
- **Power Management**: Automatically restores colors after waking from sleep or hibernation
- **Session Lock/Unlock**: Restores colors when you unlock your Windows session
- **KVM Switch Support**: Automatically detects keyboard reconnection and restores colors when using KVM switches
- **USB Reconnection**: Monitors for keyboard replug events and reapplies colors automatically
- **JSON Configuration**: Easy-to-edit configuration file for color profiles
- **Hot Reload**: Automatically detects config file changes and applies new colors
- **Key Groups**: Control multiple keys at once (WASD, arrows, function keys, etc.)
- **Individual Keys**: Set colors for specific keys
- **Multiple Profiles**: Easily switch between different color schemes

## Requirements

- Windows 10/11
- HP Omen keyboard (USB VID: 0x03F0, PID: 0x1F41)
- .NET 8.0 Runtime (included if using self-contained build)
- Administrator rights (for service installation)

## Installation

### Step 1: Build the Project

Open a command prompt in the project directory and run:

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

The compiled service will be in: `bin\Release\net8.0-windows\win-x64\publish\`

### Step 2: Copy Files

Copy the following files from the publish folder to a permanent location (e.g., `C:\Program Files\OmenKeyboardService\`):

- `OmenKeyboardService.exe`
- `config.json`
- `config.examples.json` (optional, for reference)

### Step 3: Install the Service

1. Copy `install-service.bat` to the same folder as the executable
2. Right-click `install-service.bat` and select **"Run as administrator"**
3. The service will be installed and started automatically

**Note**: The service is configured to start automatically at boot and will apply colors before you log in. It also monitors power events to restore colors after waking from sleep or hibernation.

## Configuration

### Config File Location

The service looks for `config.json` in the same directory as the executable.

### Basic Configuration

Edit `config.json` to set your desired colors:

```json
{
  "profileName": "Gaming",
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

### Using Batch Scripts (Recommended)

All scripts must be run as Administrator (right-click → "Run as administrator"):

- **`install-service.bat`** - Install and start the service
- **`uninstall-service.bat`** - Stop and remove the service
- **`start-service.bat`** - Start the service
- **`stop-service.bat`** - Stop the service

### Using Windows Services Manager

1. Press `Win + R`, type `services.msc`, press Enter
2. Find "HP Omen Keyboard RGB Service"
3. Right-click for options: Start, Stop, Restart, Properties

### Using Command Line

```bash
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

## Troubleshooting

### Service Won't Start

1. **Check keyboard is connected**: Make sure your HP Omen keyboard is plugged in
2. **Check Event Viewer**:
   - Press `Win + R`, type `eventvwr.msc`, press Enter
   - Navigate to Windows Logs → Application
   - Look for errors from "HP Omen Keyboard RGB Service"
3. **Verify config file**: Make sure `config.json` is in the same folder as the executable
4. **Run as administrator**: Service installation requires admin rights

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

### Colors Reset After Sleep/Wake

The service automatically monitors power events and restores colors when the computer wakes from sleep. The service waits 2 seconds after wake to allow hardware to initialize, then retries up to 5 times if the keyboard isn't ready.

If colors still don't persist after sleep:

1. **Check Event Viewer**:
   - Press `Win + R`, type `eventvwr.msc`, press Enter
   - Navigate to Windows Logs → Application
   - Look for messages from "HP Omen Keyboard RGB Service"
   - Check for "System resumed from sleep" followed by "Successfully applied colors"
   - If you see "Failed to apply colors after 5 attempts", the keyboard may need more time to initialize

2. **Verify service is running**:
   - Press `Win + R`, type `services.msc`, press Enter
   - Find "HP Omen Keyboard RGB Service" and check status

3. **Manual trigger**: If colors don't restore automatically, edit and save `config.json` to trigger a manual reapplication

4. **Increase delay**: Some systems may need more time. Check Event Viewer to see if retries are timing out

### Colors Reset After Lock/Unlock

The service monitors Windows session events and automatically restores colors when you unlock your session. The service waits 1 second after unlock to allow the keyboard to wake from low-power state, then retries up to 5 times if needed.

If colors don't restore after unlocking:

1. **Check Event Viewer**: Look for "Session unlocked" followed by "Successfully applied colors" messages
2. **Verify service is running**: The service must run under LocalSystem or an account with session monitoring permissions
3. **Check retry attempts**: If you see retry warnings, the keyboard is taking longer than expected to wake up
4. **Manual trigger**: Edit and save `config.json` to manually reapply colors

### KVM Switch / USB Reconnection

The service uses Windows WMI events to instantly detect keyboard reconnection and automatically restores colors. This works for:

- **KVM switches**: When switching between computers
- **USB replug**: When unplugging and replugging the keyboard
- **USB hub changes**: When moving the keyboard to a different USB port

If colors don't restore after reconnection:

1. **Check Event Viewer**: Look for "HP Omen keyboard reconnected" messages from the service
2. **Verify detection**: Colors should restore within 500ms of Windows recognizing the device
3. **Check USB power**: Some KVM switches may not provide adequate power to the keyboard
4. **WMI service**: Ensure the Windows Management Instrumentation service is running

## Uninstallation

1. Run `uninstall-service.bat` as Administrator
2. Delete the service folder

## Technical Details

### How It Works

1. Service starts when Windows boots (before user login)
2. Reads `config.json` to get color mappings
3. Connects to HP Omen keyboard via HID protocol
4. Sends RGB color commands to keyboard firmware
5. Monitors config file for changes and reloads automatically
6. Monitors Windows power events and restores colors after waking from sleep
7. Monitors Windows session events and restores colors after unlocking your session
8. Subscribes to USB device arrival events via WMI and instantly restores colors when the keyboard reconnects (KVM switches, USB replug, etc.)

### HID Protocol

The service uses the HID (Human Interface Device) protocol to communicate with the keyboard:
- Vendor ID: 0x03F0 (HP)
- Product ID: 0x1F41 (Omen keyboard)
- Commands are sent as 64-byte HID output reports
- RGB data is split across 9 command packets (3 per color channel)

### Logging

Service logs are written to Windows Event Log with detailed information about every action:
- Source: HP Omen Keyboard RGB Service
- Location: Windows Logs → Application

View logs in Event Viewer:
1. Press `Win + R`, type `eventvwr.msc`, press Enter
2. Navigate to Windows Logs → Application
3. Look for events with source "HP Omen Keyboard RGB Service"

View logs in PowerShell:
```powershell
# View last 20 log entries
Get-EventLog -LogName Application -Source "HP Omen Keyboard RGB Service" -Newest 20

# View only errors
Get-EventLog -LogName Application -Source "HP Omen Keyboard RGB Service" -EntryType Error -Newest 10

# View all logs since last sleep/wake
Get-EventLog -LogName Application -Source "HP Omen Keyboard RGB Service" -After (Get-Date).AddHours(-1)
```

Important log messages to look for:
- "Service started successfully" - Service initialization
- "System resumed from sleep" - Power event detected
- "Session unlocked" - Lock/unlock event detected
- "HP Omen keyboard reconnected" - USB device arrival detected
- "Successfully applied colors" - Colors applied successfully
- "Failed to apply colors (attempt X/Y)" - Retry in progress
- "Failed to apply colors after X attempts" - All retries exhausted, keyboard not ready

## Credits

Based on the Rust library [lights-for-omen-sequencer](https://github.com/slysherz/lights-for-omen-sequencer)

Originally ported to C# as Sequencer.linq, then converted to a Windows service.

## License

This project is provided as-is for personal use with HP Omen keyboards.
