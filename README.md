# ExodusHub Kill Tracker

ExodusHub Kill Tracker is a Windows executable that monitors your Star Citizen `Game.log` file for kill events and automatically submits them to the Exodus Hub API for processing.

## Features

- Reads your Star Citizen `Game.log` file in real-time
- Detects and scrubs kill logs
- Sends kill data to the Exodus Hub API
- No registration required

## Getting Started

### Download & Run

1. Download the latest release EXE from the releases page or your distribution source.
2. Run the executable.

### Configuration

On first launch, you will be prompted to enter:
- The path to your `Game.log` file (default: `C:\Program Files\Roberts Space Industries\StarCitizen\LIVE\Game.log`)
- Your Star Citizen username

These settings are saved in `appsettings.json` for future runs.

### Usage

- The tracker will monitor your log file and display detected kills in the console.
- Kill data is automatically sent to the Exodus Hub API at [https://sc.exoduspmc.org/api/kills](https://sc.exoduspmc.org/api/kills).
- Press `Ctrl+C` to exit the tracker.

## Support

For questions or support, please ask in the Exodus Discord.

## Project Structure

- [`Program.cs`](Program.cs): Main application logic and log monitoring
- [`UserSettings.cs`](UserSettings.cs): Handles user configuration
- [`KillData.cs`](KillData.cs): Kill data model
- [`HTTPClient.cs`](HTTPClient.cs): API communication
