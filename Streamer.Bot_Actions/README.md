# About

This folder is part of the Viewer Of The Month (VofM) project, which brings Twitch viewer engagement tracking and automation to your stream. It contains:

-  **stats.cs**: Calculates and manages viewer stats (points, redemptions, chat, watch time) in Streamer.Bot.
-  **websocket.cs**: Enables real-time WebSocket communication between Streamer.Bot and the dashboard, so leaderboard and stats update instantly.
-  **Viewer Of The Month.lra**: Import file for Streamer.Bot to quickly set up all actions and triggers.

The dashboard (in the `Dashboard/` folder) provides a web interface for viewing and managing all stats, podiums, and settings. Together, these tools let you automate, display, and celebrate your most loyal viewers in real time.

# Streamer.Bot Actions

This folder contains all files needed to integrate Viewer Of The Month with Streamer.Bot.

## Folder Structure

```
Streamer.Bot_Actions/
├── Viewer Of The Month.lra   # Streamer.Bot action import file
├── Src/
├──├── stats.cs                  # C# script for stats action
├──├── websocket.cs              # C# script for WebSocket integration
```

### File Details

-  **stats.cs**: Handles stats-related logic for Viewer Of The Month actions.
-  **websocket.cs**: Provides WebSocket communication for real-time updates between Streamer.Bot and your dashboard.
-  **Viewer Of The Month.lra**: Pre-packaged Streamer.Bot actions, triggers, and settings. Import this file to quickly set up all required actions.

## How to Import Actions into Streamer.Bot

1. Open **Streamer.Bot**.
2. Click the **Import** button.
3. Browse to this folder and select `Viewer Of The Month.lra`.
4. Confirm the import. The actions, triggers, and settings will be added to your Streamer.Bot setup.
5. Review and customize the imported actions as needed (e.g., set up triggers, permissions, or variables).

> **Tip:** If you update the C# scripts (`stats.cs` or `websocket.cs`), you can copy and paste the code into the corresponding C# action in Streamer.Bot for quick updates.

You can now use and customize the imported actions in Streamer.Bot to connect with your dashboard and overlays.
