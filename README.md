# Viewer Of The Month Dashboard

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
