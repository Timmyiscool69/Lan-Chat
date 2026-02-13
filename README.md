# Lan Chat - Local Network Chat Application

## How to Use

### Running the App
1. **From Windows Search**: Press `Windows Key` and type "Lan Chat" - click the shortcut to launch
2. **From File Explorer**: Navigate to `bin\Release\publish` folder and double-click `LanChat.exe`
3. **From Batch File**: Double-click `Run Lan Chat.bat`

### Features
- **Real-time LAN Messaging**: Chat with anyone on your local network
- **Auto-Discovery**: Automatically finds and connects to other Lan Chat users
- **Windows Notifications**: Receive toast notifications when messages arrive
- **Settings Menu**: 
  - Toggle Windows notifications on/off
  - Change your display name (3-19 characters, letters/numbers/spaces only)

### Settings
Click the ⚙ (gear) button in the top-right corner of the chat window to access:
- **Enable Windows Notifications**: Toggle desktop notifications
- **Change Name**: Set your username with validation (no special characters like `_`, emojis, or apostrophes)

### System Requirements
- Windows 10 or later
- .NET 10.0 runtime (included - standalone executable)
- Local network connection

## App Location
- **Executable**: `C:\Users\tommy\OneDrive\Documents\cool\bin\Release\publish\LanChat.exe`
- **Start Menu Shortcut**: `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Lan Chat.lnk`

## Troubleshooting
- If the app doesn't appear in Windows search immediately, restart Windows or search manually
- Make sure your firewall allows the app on your network (ports 55001-55002)
- Users must be on the same network to communicate

---

**Version**: 1.0.0  
**Author**: Tim OS
