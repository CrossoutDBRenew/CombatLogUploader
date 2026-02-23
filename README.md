## CrossoutDB Combat Uploader

CrossoutDB Combat Uploader is a small Windows tray application that automatically/manually uploads your Crossout combat
logs to
the CrossoutDB API.  
It watches the game’s log folders, keeps track of which sessions have already been sent, and periodically pushes new
logs using your personal API key.

### Features

- **Automatic combat log uploads**: Scans Crossout’s log directory and uploads completed combat/game log pairs.
- **API key–based authentication**: Uses a bearer token to authenticate against the CrossoutDB API.
- **Auto-send every 60 seconds**: Optional background timer that periodically re-scans and uploads new logs.
- **Start with Windows**: Optional auto-start via the Windows `Run` registry key.
- **System tray integration**: Hides to the tray instead of exiting, with quick actions and notifications.

### Requirements

- **Operating system**: Windows 10 or later.
- **Runtime**: [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/).
- **Game**: Crossout installed and generating logs in the default location.
- **Game start**: The game must be launched with the ``--cvar cl_combatLog 1`` option to enable combat logging.
- **API key**: You must first generate an API key in your CrossoutDB account, which will be displayed only once.

### How it works

- **Config storage**
    - Config files are stored in:
        - `AppData\Roaming\CrossoutDB\config.json`
        - `AppData\Roaming\CrossoutDB\sent.json`
    - The config contains:
        - **ApiUrl**: defaults to `https://crossoutdb.com/api/v1/logs/upload`.
        - **ApiKey**: your CrossoutDB API key.
        - **AutoSend**: whether to auto-upload every 60 seconds.
        - **AutoStartWithWindows**: whether to run at Windows startup.
        - **LogFolder**: the path to the Crossout logs (default is `AppData\Local\Targem\Crossout\logs` as per Steam
          managed install).

- **Log discovery**
    - The app looks under:
        - `AppData\Local\Targem\Crossout\logs` or the configured `LogFolder`.
    - Each subfolder is treated as a game session.
    - For a folder to be considered “ready”, it must contain:
        - `combat.log`
        - `game.log`
    - The most recent log folder is always ignored (still in use by the game).

- **Upload process**
    - For each ready folder that has **not** yet been uploaded (tracked via `sent.json`), the app:
        - Sends a `multipart/form-data` POST to `ApiUrl` with:
            - `folderName` (the log directory name)
            - `combat_log_file` (`combat.log`)
            - `game_log_file` (`game.log`)
        - Adds an `Authorization: Bearer <ApiKey>` header.
        - Marks successful uploads in `sent.json` to avoid re-sending.

### UI Overview

The main window contains:

- **API URL field**: Defaults to `https://crossoutdb.com/api/v1/logs/upload` but can be changed if needed.
- **Log folder field**: Shows the Crossout log directory, which can be changed if your logs are in a different location.
- **API Key field**: Enter your CrossoutDB API key. The value is masked in the app after saving.
- **Save button**: Saves the API key and options to `config.json` and updates auto-start.
- **Rescan button**: Re-scans the Crossout log directory and logs how many sessions are ready to send.
- **Send button**: Immediately uploads all pending logs (except the latest active folder).
- **Auto send every 60s**: Enables or disables periodic automatic uploads.
- **Start with Windows**: When enabled along with auto-send, the app starts minimized to tray on login.
- **Log box**: Shows information and error messages.

When minimized or closed, the app stays in the **system tray**:

- **Double-click tray icon**: Opens the main window.
- **Right-click menu**:
    - **Open** – restores the window.
    - **Send now** – triggers an immediate upload attempt.
    - **Exit** – closes the app completely.

### Getting your API key

1. Go to the CrossoutDB website and log in to your account.
2. Generate or copy your **API key** from your profile/settings page.
3. Paste it into the app’s **API Key** field and click **Save**.

### Building from source

1. **Install .NET SDK**
    - Install the latest **.NET 9 SDK** from the official .NET downloads.

2. **Clone this repository**

```bash
git clone https://github.com/CrossoutDBRenew/CombatLogUploader CombatLogUploader
cd CombatLogUploader
```

3. **Build**

```bash
dotnet build
```

4. **Run (from source)**

```bash
dotnet run
```

The built executable can be found under `bin\Debug\net9.0-windows\` (or `Release` if you build in Release mode).

### First-time setup

1. Start the application.
2. Note the **Config folder** path printed in the log box if you want to inspect `config.json` and `sent.json`.
3. Paste your **API key**, check the **Logs folder** and click **Save**.
4. Optionally enable:
    - **Auto send every 60s**
    - **Start with Windows**
5. Play Crossout; after a few matches, use **Rescan** or **Send** to confirm uploads are working (again, the last folder
   is ignored).

### Troubleshooting

- **No logs found**
    - Check that Crossout is installed in the default location and has been played at least once.
    - Verify that `AppData\Local\Targem\Crossout\logs` or your configured `LogFolder` exists and contains subfolders with `combat.log` and `game.log`.

- **API key missing / invalid**
    - Make sure you have pasted the correct API key from CrossoutDB.
    - If uploads fail, check the log box for HTTP status codes and try generating a new key.

- **App does nothing at startup**
    - Confirm that **Auto send every 60s** and **Start with Windows** are both enabled.
    - Check Task Manager’s startup tab or the registry key `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` for
      `CrossoutDBUploader`.

### Contributing

Contributions, issues and feature requests are welcome.

- **Bug reports**: Please include your OS version, .NET version, and any relevant log output from the app.
- **Pull requests**: Try to keep changes focused, explain the motivation in the PR description, and follow the existing
  code style.

### License

This project is licensed under the **MIT License**.  
See the `LICENSE` file for full text.

