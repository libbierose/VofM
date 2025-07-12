using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

public class CPHInline
{
    public bool Execute()
    {
        // 1. Handle connection open/close events for session tracking
        if (args.ContainsKey("triggerName"))
        {
            switch (args["triggerName"].ToString())
            {
                case "Websocket Custom Server Open":
                    OnWebSocketOpen(args); // Your session tracking function
                    return true;
                case "Websocket CustomServerClose":
                    OnWebSocketClose(args); // Your session tracking function
                    return true;
            }
        }
        if (!args.ContainsKey("__source") || args["__source"].ToString() != "WebsocketCustomServerMessage")
            return true;

        string data = args.ContainsKey("data") ? args["data"].ToString() : "";
        string action = GetRequestAction(data).ToLowerInvariant();

        switch (action)
        {
            case "getsettings":
            case "get-settings":
                SendSettings(args);
                break;

            case "setsettings":
            case "set-settings":
                UpdateSettings(data);
                SendSettings(args, broadcast: true); // notify dashboard(s)
                break;

            case "getleaderboard":
            case "get-leaderboard":
                SendLeaderboard(args, data);
                break;

            case "getcurrentwinner":
            case "get-current-winner":
                SendCurrentWinner(args);
                break;

            case "getalltimewinners":
            case "get-all-time-winners":
                SendAllTimeWinners(args);
                break;

            case "getbasepoints":
            case "get-base-points":
                SendBasePoints(args);
                break;

            case "getstreaktypes":
            case "get-streak-types":
                SendStreakTypes(args);
                break;

            case "getresetday":
            case "get-reset-day":
                SendResetDay(args);
                break;

            case "getuser":
            case "get-user":
                SendUserStats(args, data);
                break;

            case "getmonths":
                SendMonths(args);
                break;

            default:
                CPH.LogWarn("VofM Websocket: Unknown action: " + action);
                break;
        }

        return true;
    }

    // --- Action Handlers ---

    private void SendSettings(Dictionary<string, object> args, bool broadcast = false)
    {
        int resetDay = CPH.GetGlobalVar<int>("VofM_LeaderboardResetDay", true);
        var basePoints = GetGlobalDict<string, double>("VofM_BasePoints");
        var streakTypes = GetGlobalDict<string, bool>("VofM_StreakTypes");
        var redemptionPointMode = CPH.GetGlobalVar<string>("VofM_RedemptionPointMode", true) ?? "perRedemption";
        string currentWinner = CPH.GetGlobalVar<string>("VofM_CurrentWinner", true) ?? "";

        var payload = new Dictionary<string, object>
        {
            { "type", "settings" },
            { "resetDay", resetDay },
            { "basePoints", basePoints },
            { "streakTypes", streakTypes },
            { "redemptionPointMode", redemptionPointMode },
            { "currentWinner", currentWinner }
        };
        string json = JsonConvert.SerializeObject(payload);
        if (broadcast)
            CPH.WebsocketCustomServerBroadcast(json, "", 0);
        else
            SendWebsocketReply(args, json);
    }

    private void UpdateSettings(string data)
    {
        if (string.IsNullOrEmpty(data))
            return;
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(data);
        if (dict.ContainsKey("resetDay"))
            CPH.SetGlobalVar("VofM_LeaderboardResetDay", Convert.ToInt32(dict["resetDay"]), true);
        if (dict.ContainsKey("basePoints"))
            SetGlobalDict("VofM_BasePoints", JsonConvert.DeserializeObject<Dictionary<string, double>>(dict["basePoints"].ToString()));
        if (dict.ContainsKey("streakTypes"))
            SetGlobalDict("VofM_StreakTypes", JsonConvert.DeserializeObject<Dictionary<string, bool>>(dict["streakTypes"].ToString()));
        if (dict.ContainsKey("redemptionPointMode"))
            CPH.SetGlobalVar("VofM_RedemptionPointMode", dict["redemptionPointMode"].ToString(), true);

        // --- Add this block ---
        if (dict.ContainsKey("currentWinner"))
            CPH.SetGlobalVar("VofM_CurrentWinner", dict["currentWinner"].ToString(), true);

        var payload = new Dictionary<string, object>
        {
            { "type", "settings-saved" }
        };

        SendWebsocketReply(args, JsonConvert.SerializeObject(payload));

    }

    private void SendMonths(Dictionary<string, object> args)
    {
        // Get all months (dictionary keys) from VofM_Leaderboard
        var allLeaderboards = GetGlobalDict<string, Dictionary<string, double>>("VofM_Leaderboard");
        var months = allLeaderboards.Keys.ToList();
        months.Sort((a, b) => b.CompareTo(a)); // Newest first

        var payload = new Dictionary<string, object>
        {
            { "type", "months" },
            { "months", months }
        };
        SendWebsocketReply(args, JsonConvert.SerializeObject(payload));
    }

    private void SendLeaderboard(Dictionary<string, object> args, string data)
    {
        string month = GetRequestField(data, "month");
        if (string.IsNullOrEmpty(month))
            month = DateTime.UtcNow.ToString("yyyy-MM");

        var allLeaderboards = GetGlobalDict<string, Dictionary<string, double>>("VofM_Leaderboard");
        Dictionary<string, double> leaderboard = allLeaderboards.ContainsKey(month)
            ? allLeaderboards[month]
            : new Dictionary<string, double>();

        // Get top N
        var sorted = leaderboard
            .OrderByDescending(x => x.Value)
            .Take(50)
            .ToList();

        var usersOut = new List<object>();
        foreach (var pair in sorted)
        {
            //var info = GetTwitchProfileInfo(pair.Key);
            usersOut.Add(new
            {
                user = pair.Key,
                displayName = GetDisplayName(pair.Key),
                avatar = GetProfileImageUrl(pair.Key),
                points = pair.Value
            });
        }

        var payload = new Dictionary<string, object>
        {
            { "type", "leaderboard" },
            { "month", month },
            { "entries", usersOut }
        };

        SendWebsocketReply(args, JsonConvert.SerializeObject(payload));
    }

    private void SendBasePoints(Dictionary<string, object> args)
    {
        var basePoints = GetGlobalDict<string, double>("VofM_BasePoints");
        var payload = new Dictionary<string, object>
        {
            { "type", "basePoints" },
            { "basePoints", basePoints }
        };
        SendWebsocketReply(args, JsonConvert.SerializeObject(payload));
    }

    private void SendStreakTypes(Dictionary<string, object> args)
    {
        var streakTypes = GetGlobalDict<string, bool>("VofM_StreakTypes");
        var payload = new Dictionary<string, object>
        {
            { "type", "streakTypes" },
            { "streakTypes", streakTypes }
        };
        SendWebsocketReply(args, JsonConvert.SerializeObject(payload));
    }

    private void SendResetDay(Dictionary<string, object> args)
    {
        int resetDay = CPH.GetGlobalVar<int>("VofM_LeaderboardResetDay", true);
        var payload = new Dictionary<string, object>
        {
            { "type", "resetDay" },
            { "resetDay", resetDay }
        };
        SendWebsocketReply(args, JsonConvert.SerializeObject(payload));
    }

    private void SendUserStats(Dictionary<string, object> args, string data)
    {
        // --- Try to get username from args, then from data JSON ---
        string username = null;

        // First check args (websocket often sends via args)
        if (args != null && args.ContainsKey("user") && args["user"] != null)
            username = args["user"].ToString();

        // If not found, try parsing the incoming data JSON string
        if (string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(data))
        {
            try
            {
                var dataObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(data);
                if (dataObj != null && dataObj.ContainsKey("user") && dataObj["user"] != null)
                    username = dataObj["user"].ToString();
            }
            catch { /* Ignore parse errors */ }
        }

        // If still missing, just return (or optionally send an error reply)
        if (string.IsNullOrEmpty(username))
        {
            CPH.LogWarn("SendUserStats: No username provided in websocket request.");
            return;
        }

        string month = null;

        // Prefer args, then data
        if (args != null && args.ContainsKey("month") && args["month"] != null)
            month = args["month"].ToString();

        if (string.IsNullOrEmpty(month) && !string.IsNullOrEmpty(data))
        {
            try
            {
                var dataObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(data);
                if (dataObj != null && dataObj.ContainsKey("month") && dataObj["month"] != null)
                    month = dataObj["month"].ToString();
            }
            catch { /* Ignore parse errors */ }
        }

        // Fallback to current month
        if (string.IsNullOrEmpty(month))
            month = DateTime.UtcNow.ToString("yyyy-MM");

        // --- Gather stats ---
        var userdata = new Dictionary<string, object>
        {
            ["DisplayName"] = CPH.GetTwitchUserVar<string>(username, "DisplayName"),
            ["ProfileImageUrl"] = CPH.GetTwitchUserVar<string>(username, "ProfileImageUrl"),
            ["VofM_Points"] = CPH.GetTwitchUserVar<double>(username, "VofM_Points"),
            ["AllTimeRedemptions"] = CPH.GetTwitchUserVar<int>(username, "AllTimeRedemptions"),
            ["AllTimePointsSpent"] = CPH.GetTwitchUserVar<int>(username, "AllTimePointsSpent"),
            ["AllTimeChatMessages"] = CPH.GetTwitchUserVar<int>(username, "AllTimeChatMessages"),
            ["AllTimeWatchSeconds"] = CPH.GetTwitchUserVar<int>(username, "AllTimeWatchSeconds"),
            ["WatchSeconds"] = GetMonthlyStatInt(username, "MonthlyWatchSeconds", month),
            ["ChatMessages"] = GetMonthlyStatInt(username, "MonthlyChatMessages", month),
            ["Redemptions"] = GetMonthlyStatInt(username, "MonthlyRedemptions", month),
            ["HighestStreak"] = GetMonthlyStatInt(username, "MonthlyHighestStreak", month),
            ["StreamsWatched"] = GetMonthlyStatInt(username, "MonthlyStreamsWatched", month),
            ["BitsCheered"] = GetMonthlyStatInt(username, "MonthlyBitsCheered", month),
            ["Timeouts"] = GetMonthlyStatInt(username, "MonthlyTimeouts", month),
            ["GiftedSubs"] = GetMonthlyStatInt(username, "MonthlyGiftedSubs", month),
            ["Bans"] = GetMonthlyStatInt(username, "MonthlyBans", month)
            // Add additional fields as needed
        };

        // --- Send to frontend as userstats payload ---
        var payload = new
        {
            type = "userstats",
            data = userdata
        };

        string json = JsonConvert.SerializeObject(payload);
        SendWebsocketReply(args, json); // Use your websocket send method
    }


    // --- Helpers ---

    private string GetRequestAction(string json)
    {
        if (string.IsNullOrEmpty(json))
            return "";
        try
        {
            var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            if (dict != null && dict.ContainsKey("action"))
                return dict["action"].ToString();
        }
        catch { }
        return "";
    }

    private string GetRequestField(string data, string field)
    {
        if (string.IsNullOrEmpty(data)) return "";
        try
        {
            var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(data);
            if (dict != null && dict.ContainsKey(field))
                return dict[field].ToString();
        }
        catch { }
        return "";
    }

    private void SendWebsocketReply(Dictionary<string, object> args, string json)
    {
        string sessionId = args.ContainsKey("sessionId") ? args["sessionId"].ToString() : "";
        CPH.WebsocketCustomServerBroadcast(json, sessionId, 0);
    }

    private Dictionary<TKey, TValue> GetGlobalDict<TKey, TValue>(string varName)
    {
        try
        {
            string json = CPH.GetGlobalVar<string>(varName, true);
            if (!string.IsNullOrEmpty(json))
                return JsonConvert.DeserializeObject<Dictionary<TKey, TValue>>(json)
                    ?? new Dictionary<TKey, TValue>();
        }
        catch { }
        return new Dictionary<TKey, TValue>();
    }

    private void SetGlobalDict<TKey, TValue>(string varName, Dictionary<TKey, TValue> dict)
    {
        var json = JsonConvert.SerializeObject(dict);
        CPH.SetGlobalVar(varName, json, true);
    }

    // Helper to fetch profile image and display name
    private (string displayName, string profileImageUrl) GetTwitchProfileInfo(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return ("", "");
        var info = CPH.TwitchGetExtendedUserInfoByLogin(username);
        string displayName = GetDisplayName(username);
        string avatar = GetProfileImageUrl(username);
        return (displayName, avatar);
    }

    private void SendCurrentWinner(Dictionary<string, object> args)
    {
        string winnerName = CPH.GetGlobalVar<string>("VofM_CurrentWinner", true);
        double winnerPoints = CPH.GetGlobalVar<double>("VofM_CurrentWinnerPoints", true);
        //var info = CPH.TwitchGetExtendedUserInfoByLogin(winnerName);
        var payload = new Dictionary<string, object>
        {
            { "type", "currentwinner" },
            { "displayName", GetDisplayName(winnerName) },
            { "avatar", GetProfileImageUrl(winnerName)},
            { "points", winnerPoints }
        };
        SendWebsocketReply(args, JsonConvert.SerializeObject(payload));
    }

    private void SendAllTimeWinners(Dictionary<string, object> args)
    {
        var allTime = GetGlobalDict<string, List<Dictionary<string, object>>>("VofM_AllTimeWinners");
        var payload = new Dictionary<string, object>
        {
            { "type", "alltimewinners" },
            { "allTimeWinners", allTime }
        };
        SendWebsocketReply(args, JsonConvert.SerializeObject(payload));
    }

    private string GetProfileImageUrl(string user)
    {
        // 1. Try user var first
        string url = CPH.GetTwitchUserVar<string>(user, "ProfileImageUrl", true);
        if (!string.IsNullOrEmpty(url))
            return url;

        // 2. If missing, fetch from Twitch API (and save)
        var info = CPH.TwitchGetExtendedUserInfoByLogin(user);
        if (info != null && !string.IsNullOrEmpty(info.ProfileImageUrl))
        {
            // Save for next time!
            CPH.SetTwitchUserVar(user, "ProfileImageUrl", info.ProfileImageUrl, true);
            return info.ProfileImageUrl;
        }

        // 3. Fallback image
        return "https://upload.wikimedia.org/wikipedia/commons/8/89/Portrait_Placeholder.png";
    }

    private string GetDisplayName(string user)
    {
        // 1. Try user var first
        string url = CPH.GetTwitchUserVar<string>(user, "DisplayName", true);
        if (!string.IsNullOrEmpty(url))
            return url;

        // 2. If missing, fetch from Twitch API (and save)
        var info = CPH.TwitchGetExtendedUserInfoByLogin(user);
        if (info != null && !string.IsNullOrEmpty(info.UserName))
        {
            // Save for next time!
            CPH.SetTwitchUserVar(user, "DisplayName", info.UserName, true);
            return info.UserName;
        }

        // 3. Fallback image
        return user;
    }

    // Utility to get one month's data from the global JSON string
    public int GetMonthlyStatInt(string user, string varName, string month)
    {
        string raw = CPH.GetTwitchUserVar<string>(user, varName);
        if (string.IsNullOrEmpty(raw)) return 0;
        try
        {
            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(raw);
            var parts = month.Split('-'); // "2025-07"
            if (data != null && data.ContainsKey(parts[0]) && data[parts[0]].ContainsKey(parts[1]))
                return data[parts[0]][parts[1]];
        }
        catch { }
        return 0;
    }

    public List<string> GetConnectedSessions()
    {
        var json = CPH.GetGlobalVar<string>("VofM_WebsocketSessions", true);
        if (string.IsNullOrEmpty(json))
            return new List<string>();
        return Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
    }

    public void SetConnectedSessions(List<string> sessions)
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(sessions.Distinct().ToList());
        CPH.SetGlobalVar("VofM_WebsocketSessions", json, true);
    }

    public void OnWebSocketOpen(Dictionary<string, object> args)
    {
        if (args.TryGetValue("sessionId", out var sessionIdObj) && sessionIdObj != null)
        {
            string sessionId = sessionIdObj.ToString();
            var sessions = GetConnectedSessions();
            if (!sessions.Contains(sessionId))
            {
                sessions.Add(sessionId);
                SetConnectedSessions(sessions);
            }
        }
    }

    public void OnWebSocketClose(Dictionary<string, object> args)
    {
        if (args.TryGetValue("sessionId", out var sessionIdObj) && sessionIdObj != null)
        {
            string sessionId = sessionIdObj.ToString();
            var sessions = GetConnectedSessions();
            if (sessions.Contains(sessionId))
            {
                sessions.Remove(sessionId);
                SetConnectedSessions(sessions);
            }
        }
    }
    
    public void BroadcastToAllSessions(object payload)
    {
        var sessions = GetConnectedSessions();
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
        foreach (var sessionId in sessions)
        {
            CPH.WebsocketCustomServerBroadcast(json, sessionId, 0);
        }
    }
}
