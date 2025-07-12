# Viewer Of The Month Dashboard

## About

Viewer Of The Month (VofM) is a Twitch dashboard and automation suite for tracking, displaying, and celebrating your most engaged viewers. It features a modern web dashboard, OBS overlays, and deep integration with Streamer.Bot for real-time stats and automation.

-  **Dashboard**: A web-based interface for viewing the current leaderboard, podium, all-time winners, and detailed user stats. Includes settings for customizing how points are awarded and displayed.
-  **stats.cs**: A C# script for Streamer.Bot that calculates and manages viewer stats, such as points, redemptions, chat activity, and watch time.
-  **websocket.cs**: A C# script for Streamer.Bot that enables real-time communication between Streamer.Bot and the dashboard via WebSocket, allowing instant updates to the leaderboard and overlays.
-  **.lra file**: A pre-packaged Streamer.Bot action import file to quickly set up all required actions and triggers.

With VofM, you can:

-  Reward your most loyal viewers with a live-updating leaderboard and podium
-  Display stats and overlays in OBS
-  Automate point tracking and winner selection with Streamer.Bot
-  Customize point logic, streaks, and more

A web dashboard for managing and displaying Twitch Viewer Of The Month leaderboards, podiums, and user stats. Built with JavaScript, jQuery, Bootstrap, and DataTables. Integrates with Streamer.Bot and a WebSocket backend.

## Features

-  Live leaderboard and podium updates
-  All-time winners sidebar
-  User stats modal with monthly and all-time data
-  Settings modal for leaderboard configuration
-  Responsive, modern UI

## Getting Started

1. Clone this repository.
2. Open `Dashboard/dashboard.html` in your browser.
3. Ensure your backend WebSocket server is running at `ws://127.0.0.1:9292/` (or update the URL in `dashboard.js`).
4. Configure settings via the dashboard UI.

## Folder Structure

-  `Dashboard/` — Main dashboard files (HTML, JS, CSS, images)
-  `OBS_Overlays/` — Overlay HTML and images for OBS
-  `Streamer.Bot_Actions/` — C# scripts for Streamer.Bot integration
-  `JSON/` — Example JSON data
-  `Archive/` — Previous versions and backups

## Requirements

-  Web browser (Chrome, Edge, Firefox, etc.)
-  Streamer.Bot (for integration)
-  WebSocket backend (custom or provided)

## Development

-  Edit `Dashboard/dashboard.js` for frontend logic.
-  Customize styles in `Dashboard/dashhboard.css`.
-  Update overlays in `OBS_Overlays/` as needed.

## License

MIT

---

Made by Libbie-Rose | Twitch: [libbierose\_](https://twitch.tv/libbierose_) | GitHub: [libbierose](https://github.com/libbierose)
