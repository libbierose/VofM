// Streamer.bot code for handling Twitch events and tracking user stats

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public class CPHInline
{
	string gifter;
	string user;

	public bool Execute()
	{
		CPH.TryGetArg("triggerName", out string triggerName);
		string streamer = CPH.TryGetArg("broadcastUser", out string bUser) ? bUser : "";
		string user = GetUser();

		if (streamer == user)
			return true;

		switch (triggerName)
		{
			case "Stream Online":
				HandleStreamOnline();
				break;
			case "Stream Offline":
				HandleStreamOffline();
				break;
		}

		if (!CPH.GetGlobalVar<bool>("StreamOnline", true))
			return true;

		switch (triggerName)
		{
			case "Chat Message":
				HandleChatOrFirstWords(isFirstWord: false);
				break;
			case "First Words":
				HandleChatOrFirstWords(isFirstWord: true);
				break;
			case "Present Viewers":
				HandlePresentViewers();
				break;
			case "Reward Redemption":
				HandleRewardRedemption();
				break;
			case "Cheer":
				HandleCheer();
				break;
			case "Subscription":
			case "Resubscription":
				HandleSubEvent();
				break;
			case "Gift Bomb":
				// Use the gifter username (could be "Anonymous") and number of gifts
				gifter = GetUser(); // should return "Anonymous" or the username
				int giftCount = CPH.TryGetArg("gifts", out int gifts) ? gifts : 1;
				TrackGiftedSub(gifter, null, giftCount, DateTime.UtcNow);
				break;
			case "Gift Subscription":
				gifter = GetUser();
				string recipient = CPH.TryGetArg("recipientUser", out string rUser) ? rUser : "";
				TrackGiftedSub(gifter, recipient, 1, DateTime.UtcNow);
				break;
			case "User Banned":
				user = GetUser();
				IncrementUserModStat(user, "Bans");
				IncrementGlobalModStat("Bans");
				break;
			case "User Timed Out":
				user = GetUser();
				IncrementUserModStat(user, "Timeouts");
				IncrementGlobalModStat("Timeouts");
				break;
			case "Warned User":
				user = GetUser();
				IncrementUserModStat(user, "Warnings");
				IncrementGlobalModStat("Warnings");
				break;
			default:
				CPH.LogInfo($"Unknown trigger: {triggerName}");
				break;
		}

		return true;
	}

	// --- Helpers ---
	private string GetMsgId()
	{
		CPH.TryGetArg("msgId", out string msgId);
		return msgId;
	}

	private string GetUser()
	{
		CPH.TryGetArg("user", out string user);
		return user;
	}

	private string GetUserId()
	{
		CPH.TryGetArg("userId", out string userId);
		return userId;
	}

	// Send Twitch message (split long messages, always reply to correct msg)
	private void SendMessage(string message)
	{
		const int maxLength = 450;

		if (message.Length <= maxLength)
		{
			CPH.TwitchReplyToMessage(message, GetMsgId());
			return;
		}

		int index = 0;
		while (index < message.Length)
		{
			int length = Math.Min(maxLength, message.Length - index);
			int splitIndex = length;

			// If we’re not at the end, backtrack to the last space
			if (index + length < message.Length)
			{
				splitIndex = message.LastIndexOf(' ', index + length, length);
				if (splitIndex <= index)
				{
					// Couldn't find a space, hard split at maxLength
					splitIndex = index + length;
				}
			}
			else
			{
				splitIndex = message.Length;
			}

			string part = message.Substring(index, splitIndex - index).Trim();
			CPH.TwitchReplyToMessage(part, GetMsgId());
			index = splitIndex;
		}
	}

	// --- Main Trigger Handlers ---
	private void HandleStreamOnline()
	{
		CheckAndRollOverVofMLeaderboard();

		string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
		string currentStreamDate = CPH.GetGlobalVar<string>("CurrentStreamDate", true);

		CPH.SetGlobalVar("StreamOnline", true, true);

		// --- Set default reset day if not set ---
		int resetDay = CPH.GetGlobalVar<int>("VofM_LeaderboardResetDay", true);
		if (resetDay < 1 || resetDay > 28)
		{
			// Default to the 1st if not set or invalid
			CPH.SetGlobalVar("VofM_LeaderboardResetDay", 1, true);
		}

		if (currentStreamDate != today)
		{
			if (!string.IsNullOrEmpty(currentStreamDate))
			{
				CPH.SetGlobalVar("LastStreamDate", currentStreamDate, true);
			}
			CPH.SetGlobalVar("CurrentStreamDate", today, true);
			CPH.LogInfo($"Stream day updated: LastStreamDate={currentStreamDate}, CurrentStreamDate={today}");
		}
		else
		{
			CPH.LogInfo("Stream already started for today; dates not updated.");
		}
	}

	private void HandleStreamOffline()
	{
		CPH.SetGlobalVar("StreamOnline", false, true);
	}


	private void HandleChatOrFirstWords(bool isFirstWord)
	{
		if (!CPH.GetGlobalVar<bool>("StreamOnline", true))
			return;

		string user = GetUser();
		string userId = GetUserId();
		if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(userId))
			return;

		string currentMonth = DateTime.UtcNow.ToString("yyyyMM");
		DateTime now = DateTime.UtcNow;

		string lastActiveStr = CPH.GetTwitchUserVar<string>(user, "LastActiveTime", true);
		string lastPresentStr = CPH.GetGlobalVar<string>("LastPresentViewersTime", true);

		DateTime lastActive = DateTime.MinValue;
		DateTime lastPresent = DateTime.MinValue;
		DateTime.TryParse(lastActiveStr, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out lastActive);
		DateTime.TryParse(lastPresentStr, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out lastPresent);

		int secondsToAdd = 0;

		if (lastActive < lastPresent && lastPresent < now)
		{
			// User's last message was before the last Present Viewers tick (or never chatted).
			secondsToAdd = (int)(now - lastPresent).TotalSeconds;
		}
		else if (lastActive >= lastPresent && lastActive < now)
		{
			// User has already chatted after the last Present Viewers tick.
			secondsToAdd = (int)(now - lastActive).TotalSeconds;
		}

		// Only add if within sensible bounds
		if (secondsToAdd > 0 && secondsToAdd < 900)
			IncrementWatchTimeSeconds(user, now, secondsToAdd);

		// Update user's last active time to now
		CPH.SetTwitchUserVar(user, "LastActiveTime", now.ToString("o"), true);

		// Only announce on First Words
		UpdateViewerStreak(user, userId, false);

		if (isFirstWord)
			SaveUserProfileImageUrl(user);

		IncrementChatMessageCount(user, now);
	}


	private void HandlePresentViewers()
	{
		if (!CPH.TryGetArg("isLive", out bool isLive))
			return;

		// Keep global StreamOnline flag accurate
		CPH.SetGlobalVar("StreamOnline", isLive, true);
		if (!isLive) return;

		string currentMonth = DateTime.UtcNow.ToString("yyyyMM");
		DateTime now = DateTime.UtcNow;
		string lastUpdateStr = CPH.GetGlobalVar<string>("LastPresentViewersTime", true);
		string streamer = CPH.TryGetArg("broadcastUser", out string bUser) ? bUser : "";

		DateTime lastUpdate = now;
		if (!string.IsNullOrEmpty(lastUpdateStr))
			DateTime.TryParse(lastUpdateStr, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out lastUpdate);

		int secondsElapsed = (int)(now - lastUpdate).TotalSeconds;
		if (secondsElapsed < 0) secondsElapsed = 0;

		if (CPH.TryGetArg("users", out List<Dictionary<string, object>> users) && users != null)
		{
			foreach (var userDict in users)
			{
				if (userDict.TryGetValue("display", out object userObj) && userDict.TryGetValue("id", out object userIdObj))
				{
					string user = userObj?.ToString();
					string userId = userIdObj?.ToString();
					if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(userId))
						continue;

					if (streamer == user)
						continue;

					// --- Key Logic: Avoid double counting with chat activity ---
					string lastActiveStr = CPH.GetTwitchUserVar<string>(user, "LastActiveTime", true);
					DateTime lastActive = DateTime.MinValue;
					DateTime.TryParse(lastActiveStr, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out lastActive);

					int secondsToAdd = 0;
					if (lastActive < lastUpdate)
					{
						// User wasn't active since last tick, credit full present tick
						secondsToAdd = secondsElapsed;
					}
					else if (lastActive >= lastUpdate && lastActive < now)
					{
						// User chatted since last tick, credit only the time since their last message
						secondsToAdd = (int)(now - lastActive).TotalSeconds;
					}

					if (secondsToAdd > 0 && secondsToAdd < 900)
						IncrementWatchTimeSeconds(user, now, secondsToAdd);

					UpdateViewerStreak(user, userId, announce: false);
				}
			}
		}

		// Store as UTC
		CPH.SetGlobalVar("LastPresentViewersTime", now.ToString("o"), true);
	}


	// --- Core Streak Logic ---
	private void UpdateViewerStreak(string user, string userId, bool announce)
	{
		string currentStreamDate = CPH.GetGlobalVar<string>("CurrentStreamDate", true);
		string lastStreamDate = CPH.GetGlobalVar<string>("LastStreamDate", true);

		if (string.IsNullOrEmpty(currentStreamDate))
			return;

		string userLastStreamDate = CPH.GetTwitchUserVar<string>(user, "LastStreamDate", true);
		int streak = CPH.GetTwitchUserVar<int>(user, "Streak", true);

		bool isAlreadyCounted = (userLastStreamDate == currentStreamDate);

		if (!isAlreadyCounted)
		{
			if (userLastStreamDate == lastStreamDate)
			{
				streak++;
			}
			else
			{
				streak = 1;
			}

			// Save new values
			CPH.SetTwitchUserVar(user, "Streak", streak, true);
			CPH.SetTwitchUserVar(user, "LastStreamDate", currentStreamDate, true);

			// -- HIGHEST STREAK: All time --
			int allTimeHighest = CPH.GetTwitchUserVar<int>(user, "AllTimeHighestStreak", true);
			if (streak > allTimeHighest)
				CPH.SetTwitchUserVar(user, "AllTimeHighestStreak", streak, true);

			// -- HIGHEST STREAK: Monthly (Year/Month) --
			var dict = GetMonthlyHighestStreakDict(user);
			DateTime now = DateTime.UtcNow;
			string yearKey = now.Year.ToString();
			string monthKey = now.Month.ToString("D2");
			if (!dict.ContainsKey(yearKey))
				dict[yearKey] = new Dictionary<string, int>();
			if (!dict[yearKey].ContainsKey(monthKey))
				dict[yearKey][monthKey] = 0;
			if (streak > dict[yearKey][monthKey])
				dict[yearKey][monthKey] = streak;
			SaveMonthlyHighestStreakDict(user, dict);

			AddVofMPoints(user, "Streak", streak);

			IncrementStreamsWatched(user, DateTime.UtcNow);
		}

		// Only announce on First Words AND if user is following
		if (announce && IsUserFollowing(userId))
		{
			SendMessage($"Welcome in {user}, your current streak is {streak} stream(s) in a row!");
		}
	}

	// --- Check if user is following (by userId) ---
	private bool IsUserFollowing(string userId)
	{
		if (string.IsNullOrEmpty(userId))
			return false;
		var info = CPH.TwitchGetExtendedUserInfoById(userId);
		return info != null && info.IsFollowing;
	}

	private void IncrementWatchTimeSeconds(string user, DateTime now, int secondsToAdd)
	{
		// All time
		int allTimeSecs = CPH.GetTwitchUserVar<int>(user, "AllTimeWatchSeconds", true);
		allTimeSecs += secondsToAdd;
		CPH.SetTwitchUserVar(user, "AllTimeWatchSeconds", allTimeSecs, true);

		// Monthly nested dict (Year/Month)
		var dict = GetMonthlyWatchDict(user);
		string yearKey = now.Year.ToString();
		string monthKey = now.Month.ToString("D2");
		if (!dict.ContainsKey(yearKey))
			dict[yearKey] = new Dictionary<string, int>();
		if (!dict[yearKey].ContainsKey(monthKey))
			dict[yearKey][monthKey] = 0;
		dict[yearKey][monthKey] += secondsToAdd;
		SaveMonthlyWatchDict(user, dict);

		AddVofMPoints(user, "Watchtime", Math.Max(0.5, secondsToAdd / 60));
	}

	// Deserialize user's monthly watch dict
	private Dictionary<string, Dictionary<string, int>> GetMonthlyWatchDict(string user)
	{
		var json = CPH.GetTwitchUserVar<string>(user, "MonthlyWatchSeconds", true);
		if (string.IsNullOrEmpty(json))
			return new Dictionary<string, Dictionary<string, int>>();
		return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(json)
			   ?? new Dictionary<string, Dictionary<string, int>>();
	}

	// Save updated dict
	private void SaveMonthlyWatchDict(string user, Dictionary<string, Dictionary<string, int>> dict)
	{
		var json = Newtonsoft.Json.JsonConvert.SerializeObject(dict);
		CPH.SetTwitchUserVar(user, "MonthlyWatchSeconds", json, true);
	}

	private Dictionary<string, Dictionary<string, int>> GetMonthlyChatDict(string user)
	{
		var json = CPH.GetTwitchUserVar<string>(user, "MonthlyChatMessages", true);
		if (string.IsNullOrEmpty(json))
			return new Dictionary<string, Dictionary<string, int>>();
		return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(json)
			   ?? new Dictionary<string, Dictionary<string, int>>();
	}
	private void SaveMonthlyChatDict(string user, Dictionary<string, Dictionary<string, int>> dict)
	{
		var json = Newtonsoft.Json.JsonConvert.SerializeObject(dict);
		CPH.SetTwitchUserVar(user, "MonthlyChatMessages", json, true);
	}

	private void IncrementChatMessageCount(string user, DateTime now)
	{
		// All time
		int allTime = CPH.GetTwitchUserVar<int>(user, "AllTimeChatMessages", true);
		allTime++;
		CPH.SetTwitchUserVar(user, "AllTimeChatMessages", allTime, true);

		// Monthly
		var dict = GetMonthlyChatDict(user);
		string yearKey = now.Year.ToString();
		string monthKey = now.Month.ToString("D2");
		if (!dict.ContainsKey(yearKey))
			dict[yearKey] = new Dictionary<string, int>();
		if (!dict[yearKey].ContainsKey(monthKey))
			dict[yearKey][monthKey] = 0;
		dict[yearKey][monthKey]++;
		SaveMonthlyChatDict(user, dict);

		AddVofMPoints(user, "Chat Message");
	}

	private Dictionary<string, Dictionary<string, int>> GetMonthlyStreamsDict(string user)
	{
		var json = CPH.GetTwitchUserVar<string>(user, "MonthlyStreamsWatched", true);
		if (string.IsNullOrEmpty(json))
			return new Dictionary<string, Dictionary<string, int>>();
		return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(json)
			   ?? new Dictionary<string, Dictionary<string, int>>();
	}
	private void SaveMonthlyStreamsDict(string user, Dictionary<string, Dictionary<string, int>> dict)
	{
		var json = Newtonsoft.Json.JsonConvert.SerializeObject(dict);
		CPH.SetTwitchUserVar(user, "MonthlyStreamsWatched", json, true);
	}

	private void IncrementStreamsWatched(string user, DateTime now)
	{
		// All time
		int allTime = CPH.GetTwitchUserVar<int>(user, "AllTimeStreamsWatched", true);
		allTime++;
		CPH.SetTwitchUserVar(user, "AllTimeStreamsWatched", allTime, true);

		// Monthly
		var dict = GetMonthlyStreamsDict(user);
		string yearKey = now.Year.ToString();
		string monthKey = now.Month.ToString("D2");
		if (!dict.ContainsKey(yearKey))
			dict[yearKey] = new Dictionary<string, int>();
		if (!dict[yearKey].ContainsKey(monthKey))
			dict[yearKey][monthKey] = 0;
		dict[yearKey][monthKey]++;
		SaveMonthlyStreamsDict(user, dict);
	}

	private void HandleRewardRedemption()
	{
		string user = GetUser();
		string userId = GetUserId();
		if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(userId))
			return;

		string currentMonth = DateTime.UtcNow.ToString("yyyyMM");
		DateTime now = DateTime.UtcNow;

		// Get reward cost from args
		int rewardCost = 0;
		if (!CPH.TryGetArg("rewardCost", out rewardCost))
			rewardCost = 0;

		// Increment counts
		IncrementRedemptions(user, now, rewardCost);

		// Use the mode to decide how points are awarded
		string mode = GetVofMRedemptionPointMode();

		if (mode == "perRedemption")
		{
			AddVofMPoints(user, "Reward Redemption");
		}
		else if (mode == "byCost")
		{
			// Use the cost as the multiplier!
			AddVofMPoints(user, "Reward Redemption", rewardCost > 0 ? rewardCost : 1);
		}
	}

	private void IncrementRedemptions(string user, DateTime now, int rewardCost)
	{
		// --- ALL-TIME ---
		int allTimeRedemptions = CPH.GetTwitchUserVar<int>(user, "AllTimeRedemptions", true);
		allTimeRedemptions++;
		CPH.SetTwitchUserVar(user, "AllTimeRedemptions", allTimeRedemptions, true);

		int allTimePointsSpent = CPH.GetTwitchUserVar<int>(user, "AllTimePointsSpent", true);
		allTimePointsSpent += rewardCost;
		CPH.SetTwitchUserVar(user, "AllTimePointsSpent", allTimePointsSpent, true);

		// --- MONTHLY (nested dict, same pattern as watchtime) ---
		var dict = GetMonthlyRedemptionDict(user);
		string yearKey = now.Year.ToString();
		string monthKey = now.Month.ToString("D2");

		// Init structure
		if (!dict.ContainsKey(yearKey))
			dict[yearKey] = new Dictionary<string, RedemptionStats>();
		if (!dict[yearKey].ContainsKey(monthKey))
			dict[yearKey][monthKey] = new RedemptionStats();

		// Increment
		dict[yearKey][monthKey].Redemptions++;
		dict[yearKey][monthKey].PointsSpent += rewardCost;

		SaveMonthlyRedemptionDict(user, dict);
	}

	// Helper class for monthly stats
	public class RedemptionStats
	{
		public int Redemptions { get; set; } = 0;
		public int PointsSpent { get; set; } = 0;
	}

	private Dictionary<string, Dictionary<string, RedemptionStats>> GetMonthlyRedemptionDict(string user)
	{
		var json = CPH.GetTwitchUserVar<string>(user, "MonthlyRedemptions", true);
		if (string.IsNullOrEmpty(json))
			return new Dictionary<string, Dictionary<string, RedemptionStats>>();
		return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, RedemptionStats>>>(json)
			?? new Dictionary<string, Dictionary<string, RedemptionStats>>();
	}

	private void SaveMonthlyRedemptionDict(string user, Dictionary<string, Dictionary<string, RedemptionStats>> dict)
	{
		var json = Newtonsoft.Json.JsonConvert.SerializeObject(dict);
		CPH.SetTwitchUserVar(user, "MonthlyRedemptions", json, true);
	}

	// Nested dictionary: Year -> Month -> Highest streak
	private Dictionary<string, Dictionary<string, int>> GetMonthlyHighestStreakDict(string user)
	{
		var json = CPH.GetTwitchUserVar<string>(user, "MonthlyHighestStreak", true);
		if (string.IsNullOrEmpty(json))
			return new Dictionary<string, Dictionary<string, int>>();
		return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(json)
			?? new Dictionary<string, Dictionary<string, int>>();
	}

	private void SaveMonthlyHighestStreakDict(string user, Dictionary<string, Dictionary<string, int>> dict)
	{
		var json = Newtonsoft.Json.JsonConvert.SerializeObject(dict);
		CPH.SetTwitchUserVar(user, "MonthlyHighestStreak", json, true);
	}

	private void HandleCheer()
	{
		// Only run if bits is present and greater than zero
		if (!CPH.TryGetArg("bits", out int bits) || bits <= 0)
			return;

		string user = GetUser();
		string userId = GetUserId();
		if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(userId))
			return;

		DateTime now = DateTime.UtcNow;
		string yearKey = now.Year.ToString();
		string monthKey = now.Month.ToString("D2");

		// --- All Time ---
		int allTimeBits = CPH.GetTwitchUserVar<int>(user, "AllTimeBitsCheered", true);
		allTimeBits += bits;
		CPH.SetTwitchUserVar(user, "AllTimeBitsCheered", allTimeBits, true);

		// --- Monthly (Year/Month Nested Dict) ---
		var dict = GetMonthlyBitsDict(user);
		if (!dict.ContainsKey(yearKey))
			dict[yearKey] = new Dictionary<string, int>();
		if (!dict[yearKey].ContainsKey(monthKey))
			dict[yearKey][monthKey] = 0;
		dict[yearKey][monthKey] += bits;
		SaveMonthlyBitsDict(user, dict);

		AddVofMPoints(user, "Cheer", bits);

		// (Optional: message or log for feedback)
		// SendMessage($"{user} cheered {bits} bits! Total: {allTimeBits} all time.");
	}

	// Helpers for monthly bits
	private Dictionary<string, Dictionary<string, int>> GetMonthlyBitsDict(string user)
	{
		var json = CPH.GetTwitchUserVar<string>(user, "MonthlyBitsCheered", true);
		if (string.IsNullOrEmpty(json))
			return new Dictionary<string, Dictionary<string, int>>();
		return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(json)
			?? new Dictionary<string, Dictionary<string, int>>();
	}

	private void SaveMonthlyBitsDict(string user, Dictionary<string, Dictionary<string, int>> dict)
	{
		var json = Newtonsoft.Json.JsonConvert.SerializeObject(dict);
		CPH.SetTwitchUserVar(user, "MonthlyBitsCheered", json, true);
	}

	private void HandleSubEvent()
	{
		string user = GetUser();
		if (string.IsNullOrEmpty(user)) return;

		// Try to get provided values (they exist for resubs, but not always for first-time subs)
		int streak = CPH.TryGetArg("monthStreak", out int s) ? s : 0;
		int cumulative = CPH.TryGetArg("cumulative", out int c) ? c : 0;

		// If this is a first sub (values may be 0 or not present)
		if (streak <= 0) streak = 1;
		if (cumulative <= 0) cumulative = 1;

		// Store the values in user variables
		CPH.SetTwitchUserVar(user, "SubMonthStreak", streak, true);
		CPH.SetTwitchUserVar(user, "SubCumulative", cumulative, true);

		AddVofMPoints(user, "Subscription");
	}

	// Call this for both TwitchGiftBomb and TwitchGiftSub events
	private void TrackGiftedSub(string gifter, string recipient, int count, DateTime now)
	{
		// ----- Handle Anonymous Gifter -----
		if (string.IsNullOrEmpty(gifter) || gifter.ToLower() == "anonymous")
		{
			// All-time global
			int anonGifted = CPH.GetGlobalVar<int>("AnonymousGiftedSubs", true);
			anonGifted += count;
			CPH.SetGlobalVar("AnonymousGiftedSubs", anonGifted, true);

			// Monthly global
			var dict = GetGlobalMonthlyGiftDict("AnonymousGiftedSubsMonthly");
			string yearKey = now.Year.ToString();
			string monthKey = now.Month.ToString("D2");
			if (!dict.ContainsKey(yearKey))
				dict[yearKey] = new Dictionary<string, int>();
			if (!dict[yearKey].ContainsKey(monthKey))
				dict[yearKey][monthKey] = 0;
			dict[yearKey][monthKey] += count;
			SaveGlobalMonthlyGiftDict("AnonymousGiftedSubsMonthly", dict);

			// Optionally track recipient as well (below)
		}
		else
		{
			// ----- Handle User Gifter -----
			// All-time per-user
			int gifted = CPH.GetTwitchUserVar<int>(gifter, "GiftedSubs", true);
			gifted += count;
			CPH.SetTwitchUserVar(gifter, "GiftedSubs", gifted, true);

			// Monthly per-user
			var userDict = GetMonthlyGiftedDict(gifter);
			string yKey = now.Year.ToString();
			string mKey = now.Month.ToString("D2");
			if (!userDict.ContainsKey(yKey))
				userDict[yKey] = new Dictionary<string, int>();
			if (!userDict[yKey].ContainsKey(mKey))
				userDict[yKey][mKey] = 0;
			userDict[yKey][mKey] += count;
			SaveMonthlyGiftedDict(gifter, userDict);
		}

		// --- Optional: Track recipient's total received gifted subs ---
		if (!string.IsNullOrEmpty(recipient) && recipient.ToLower() != "anonymous")
		{
			int received = CPH.GetTwitchUserVar<int>(recipient, "GiftedSubsReceived", true);
			received += count;
			CPH.SetTwitchUserVar(recipient, "GiftedSubsReceived", received, true);

			// You can do monthly tracking here if you like!
		}

		// Award Viewer of the Month points for the gifter
		if (!string.IsNullOrEmpty(gifter) && gifter.ToLower() != "anonymous")
		{
			AddVofMPoints(gifter, "Gift Subscription", count);
		}
	}

	private Dictionary<string, Dictionary<string, int>> GetMonthlyGiftedDict(string user)
	{
		var json = CPH.GetTwitchUserVar<string>(user, "MonthlyGiftedSubs", true);
		if (string.IsNullOrEmpty(json))
			return new Dictionary<string, Dictionary<string, int>>();
		return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(json)
			   ?? new Dictionary<string, Dictionary<string, int>>();
	}
	private void SaveMonthlyGiftedDict(string user, Dictionary<string, Dictionary<string, int>> dict)
	{
		var json = Newtonsoft.Json.JsonConvert.SerializeObject(dict);
		CPH.SetTwitchUserVar(user, "MonthlyGiftedSubs", json, true);
	}

	private Dictionary<string, Dictionary<string, int>> GetGlobalMonthlyGiftDict(string varName)
	{
		var json = CPH.GetGlobalVar<string>(varName, true);
		if (string.IsNullOrEmpty(json))
			return new Dictionary<string, Dictionary<string, int>>();
		return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(json)
			   ?? new Dictionary<string, Dictionary<string, int>>();
	}

	private void SaveGlobalMonthlyGiftDict(string varName, Dictionary<string, Dictionary<string, int>> dict)
	{
		var json = Newtonsoft.Json.JsonConvert.SerializeObject(dict);
		CPH.SetGlobalVar(varName, json, true);
	}

	// Increment any user moderation stat (ban, timeout, warning)
	public void IncrementUserModStat(string user, string statKey)
	{
		// All-time
		int allTime = CPH.GetTwitchUserVar<int>(user, $"AllTime{statKey}", true);
		allTime++;
		CPH.SetTwitchUserVar(user, $"AllTime{statKey}", allTime, true);

		// Monthly
		string month = DateTime.UtcNow.Month.ToString("D2");
		string year = DateTime.UtcNow.Year.ToString();
		var monthlyDict = GetMonthlyModStatDict(user, statKey);
		if (!monthlyDict.ContainsKey(year)) monthlyDict[year] = new Dictionary<string, int>();
		if (!monthlyDict[year].ContainsKey(month)) monthlyDict[year][month] = 0;
		monthlyDict[year][month]++;
		SaveMonthlyModStatDict(user, statKey, monthlyDict);

		AddVofMPoints(user, statKey);
	}

	// Get/Save monthly moderation stats (re-usable for all stats)
	public Dictionary<string, Dictionary<string, int>> GetMonthlyModStatDict(string user, string statKey)
	{
		var json = CPH.GetTwitchUserVar<string>(user, $"Monthly{statKey}", true);
		if (string.IsNullOrEmpty(json))
			return new Dictionary<string, Dictionary<string, int>>();
		return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(json)
			?? new Dictionary<string, Dictionary<string, int>>();
	}
	public void SaveMonthlyModStatDict(string user, string statKey, Dictionary<string, Dictionary<string, int>> dict)
	{
		var json = Newtonsoft.Json.JsonConvert.SerializeObject(dict);
		CPH.SetTwitchUserVar(user, $"Monthly{statKey}", json, true);
	}

	// Increment global stats (for all users)
	public void IncrementGlobalModStat(string statKey)
	{
		// All time
		int allTime = CPH.GetGlobalVar<int>($"AllTimeGlobal{statKey}", true);
		allTime++;
		CPH.SetGlobalVar($"AllTimeGlobal{statKey}", allTime, true);

		// Monthly
		string month = DateTime.UtcNow.Month.ToString("D2");
		string year = DateTime.UtcNow.Year.ToString();
		var monthlyDict = GetGlobalMonthlyModStatDict(statKey);
		if (!monthlyDict.ContainsKey(year)) monthlyDict[year] = new Dictionary<string, int>();
		if (!monthlyDict[year].ContainsKey(month)) monthlyDict[year][month] = 0;
		monthlyDict[year][month]++;
		SaveGlobalMonthlyModStatDict(statKey, monthlyDict);
	}

	public Dictionary<string, Dictionary<string, int>> GetGlobalMonthlyModStatDict(string statKey)
	{
		var json = CPH.GetGlobalVar<string>($"MonthlyGlobal{statKey}", true);
		if (string.IsNullOrEmpty(json))
			return new Dictionary<string, Dictionary<string, int>>();
		return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(json)
			?? new Dictionary<string, Dictionary<string, int>>();
	}
	public void SaveGlobalMonthlyModStatDict(string statKey, Dictionary<string, Dictionary<string, int>> dict)
	{
		var json = Newtonsoft.Json.JsonConvert.SerializeObject(dict);
		CPH.SetGlobalVar($"MonthlyGlobal{statKey}", json, true);
	}

	/* Viewer Of The Month Points Code */

	// Get or create VofM_BasePoints as a global variable with defaults if missing
	public Dictionary<string, double> GetOrCreateVofMBasePoints()
	{
		var json = CPH.GetGlobalVar<string>("VofM_BasePoints", true);
		if (string.IsNullOrEmpty(json))
		{
			// Default values
			var defaults = new Dictionary<string, double>
			{
				{"Streak", 1},
				{"Watchtime", 1},
				{"Chat Message", 1},
				{"Reward Redemption", 1},
				{"Cheer", 0.01},
				{"Gift Subscription", 1},
				{"Subscription", 5},
				{"Bans", -999},
				{"Timeouts", -0.10},
				{"Warnings", -0.10}
			};
			// Write to global so it exists in future
			SetVofMBasePoints(defaults);
			return defaults;
		}
		return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, double>>(json)
			   ?? new Dictionary<string, double>();
	}

	// Set/update the VofM_BasePoints variable (pass a dictionary)
	public void SetVofMBasePoints(Dictionary<string, double> points)
	{
		var json = Newtonsoft.Json.JsonConvert.SerializeObject(points);
		CPH.SetGlobalVar("VofM_BasePoints", json, true);
	}

	public Dictionary<string, bool> GetDefaultVofMStreakTypes()
	{
		return new Dictionary<string, bool>
		{
			// Set to true if you want the streak as a multiplier for that event by default
			{"Streak", false},
			{"Watchtime", false},
			{"Chat Message", true},
			{"Reward Redemption", true},
			{"Cheer", true},
			{"Gift Subscription", false},
			{"Subscription", true},
			{"Bans", false},
			{"Timeouts", false},
			{"Warnings", false}
			// Add/remove types as needed!
		};
	}

	// Use this to get the config, with auto-initialize if missing
	public Dictionary<string, bool> GetVofMStreakTypes()
	{
		var json = CPH.GetGlobalVar<string>("VofM_StreakTypes", true);
		if (string.IsNullOrEmpty(json))
		{
			var defaults = GetDefaultVofMStreakTypes();
			SetVofMStreakTypes(defaults);
			return defaults;
		}
		return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, bool>>(json)
			   ?? GetDefaultVofMStreakTypes();
	}

	public void SetVofMStreakTypes(Dictionary<string, bool> types)
	{
		var json = Newtonsoft.Json.JsonConvert.SerializeObject(types);
		CPH.SetGlobalVar("VofM_StreakTypes", json, true);
	}

	// Returns the mode: "perRedemption" (default) or "byCost"
	public string GetVofMRedemptionPointMode()
	{
		var mode = CPH.GetGlobalVar<string>("VofM_RedemptionPointMode", true);

		if (string.IsNullOrEmpty(mode))
			CPH.SetGlobalVar("VofM_RedemptionPointMode", "perRedemption", true);

		return string.IsNullOrEmpty(mode) ? "perRedemption" : mode;
	}

	public void SetVofMRedemptionPointMode(string mode)
	{
		// Only allow valid values
		if (mode != "perRedemption" && mode != "byCost")
			mode = "perRedemption";
		CPH.SetGlobalVar("VofM_RedemptionPointMode", mode, true);
	}

	public void AddVofMPoints(string user, string type, double actionMultiplier = 1)
	{
		// --- Calculate points to add ---
		var pointsTable = GetOrCreateVofMBasePoints();
		double basePoints = pointsTable.ContainsKey(type) ? pointsTable[type] : 0;

		// If base point is 0, don't add anything!
		if (basePoints == 0)
			return;

		var streakTypes = GetVofMStreakTypes();
		bool useStreak = streakTypes.ContainsKey(type) && streakTypes[type];

		int streak = 1;
		if (useStreak)
		{
			streak = CPH.GetTwitchUserVar<int>(user, "Streak", true);
			if (streak < 1) streak = 1;
		}

		// Calculate and round points to add
		double pointsToAdd = Math.Round(basePoints * actionMultiplier * streak, 2);

		if (pointsToAdd == 0)
			return;

		// --- Update per-user points (for compatibility) ---
		double totalPoints = CPH.GetTwitchUserVar<double>(user, "VofM_Points", true);
		totalPoints = Math.Round(totalPoints + pointsToAdd, 2);
		CPH.SetTwitchUserVar(user, "VofM_Points", totalPoints, true);

		// --- Update the per-period leaderboard dictionary ---
		string periodKey = GetLeaderboardPeriod(); // Use the helper below!

		// Main leaderboard is a dictionary of periods: user:points
		var allLeaderboards = CPH.GetGlobalVar<Dictionary<string, Dictionary<string, double>>>("VofM_Leaderboard", true)
			?? new Dictionary<string, Dictionary<string, double>>();

		if (!allLeaderboards.ContainsKey(periodKey))
			allLeaderboards[periodKey] = new Dictionary<string, double>();

		if (allLeaderboards[periodKey].ContainsKey(user))
			allLeaderboards[periodKey][user] = Math.Round(allLeaderboards[periodKey][user] + pointsToAdd, 2);
		else
			allLeaderboards[periodKey][user] = pointsToAdd;

		// Optionally, keep "current" as a reference for UI fallback
		allLeaderboards["current"] = allLeaderboards[periodKey];

		// Save back to global
		CPH.SetGlobalVar("VofM_Leaderboard", allLeaderboards, true);
		SendCurrentLeaderboardWebsocket(periodKey, allLeaderboards[periodKey]);
	}

	public void SendCurrentLeaderboardWebsocket(string month, Dictionary<string, double> leaderboard)
	{
		// Prepare leaderboard as sorted list of users
		var entries = leaderboard
			.OrderByDescending(kv => kv.Value)
			.Take(50) // Only top 10 if you want
			.Select((kv, idx) => new
			{
				user = kv.Key,
				points = Math.Round(kv.Value, 2),
				avatar = CPH.GetTwitchUserVar<string>(kv.Key, "ProfileImageUrl", true)
			}).ToList();

		var payload = new
		{
			type = "liveleaderboard",
			month = month,
			entries = entries
		};

		string sessionId = "*";
		BroadcastToAllSessions(payload);
	}

	/// <summary>
	/// Returns the current leaderboard period key (yyyy-MM) based on the configured reset day.
	/// </summary>
	public string GetLeaderboardPeriod()
	{
		int resetDay = CPH.GetGlobalVar<int>("VofM_LeaderboardResetDay", true);
		if (resetDay < 1 || resetDay > 28) resetDay = 1;

		DateTime now = DateTime.UtcNow;
		DateTime resetDate = new DateTime(now.Year, now.Month, resetDay);

		if (now < resetDate)
			resetDate = resetDate.AddMonths(-1);

		// e.g. "2025-07"
		return resetDate.ToString("yyyy-MM");
	}

	public void CheckAndRollOverVofMLeaderboard()
	{
		// 1. Get the configured reset day (default to 1 if not set or invalid)
		int resetDay = CPH.GetGlobalVar<int>("VofM_LeaderboardResetDay", true);
		if (resetDay < 1 || resetDay > 28) resetDay = 1;

		DateTime now = DateTime.UtcNow;

		// 2. Only run on the reset day (and only once per month)
		if (now.Day != resetDay) return;

		// Use the same period key as in AddVofMPoints
		string thisPeriod = GetLeaderboardPeriod(); // e.g. 2025-07 based on custom day
		string lastRollover = CPH.GetGlobalVar<string>("VofM_LastLeaderboardRollover", true);
		if (lastRollover == thisPeriod)
			return; // Already rolled over this month

		// 3. Grab the ALL leaderboards and all-time winners
		var allLeaderboards = CPH.GetGlobalVar<Dictionary<string, Dictionary<string, double>>>("VofM_Leaderboard", true)
			?? new Dictionary<string, Dictionary<string, double>>();
		var allTime = CPH.GetGlobalVar<Dictionary<string, List<Dictionary<string, object>>>>("VofM_AllTimeWinners", true)
			?? new Dictionary<string, List<Dictionary<string, object>>>();

		Dictionary<string, double> leaderboard = null;
		if (allLeaderboards.ContainsKey(thisPeriod))
			leaderboard = allLeaderboards[thisPeriod];
		else
			leaderboard = new Dictionary<string, double>();

		if (leaderboard.Count > 0)
		{
			// Sort and get top 3
			var top3 = leaderboard.OrderByDescending(kv => kv.Value).Take(3).ToList();

			var winners = new List<Dictionary<string, object>>();
			for (int i = 0; i < top3.Count; i++)
			{
				winners.Add(new Dictionary<string, object>
				{
					{ "user", top3[i].Key },
					{ "Points", top3[i].Value }
				});
			}

			// Save winners for this period
			allTime[thisPeriod] = winners;
			CPH.SetGlobalVar("VofM_AllTimeWinners", allTime, true);

			// Save current winner (for UI etc)
			if (winners.Count > 0)
			{
				CPH.SetGlobalVar("VofM_CurrentWinner", winners[0]["user"], true);
				CPH.SetGlobalVar("VofM_CurrentWinnerPoints", winners[0]["Points"], true);
			}
			else
			{
				CPH.SetGlobalVar("VofM_CurrentWinner", "", true);
				CPH.SetGlobalVar("VofM_CurrentWinnerPoints", 0.0, true);
			}
		}
		else
		{
			CPH.SetGlobalVar("VofM_CurrentWinner", "", true);
			CPH.SetGlobalVar("VofM_CurrentWinnerPoints", 0.0, true);
		}

		// 4. Reset ONLY the leaderboard for this period
		allLeaderboards[thisPeriod] = new Dictionary<string, double>();
		allLeaderboards["current"] = allLeaderboards[thisPeriod];
		CPH.SetGlobalVar("VofM_Leaderboard", allLeaderboards, true);

		// 5. Update last rollover period
		CPH.SetGlobalVar("VofM_LastLeaderboardRollover", thisPeriod, true);
	}

	public void SaveUserProfileImageUrl(string user)
	{
		// Get current date
		var today = DateTime.UtcNow.Date;

		// Get the last updated date (if any)
		string lastUpdatedStr = CPH.GetTwitchUserVar<string>(user, "ProfileImageUrl_LastUpdated", true);
		DateTime lastUpdated;
		bool needsUpdate = true;

		if (!string.IsNullOrEmpty(lastUpdatedStr) && DateTime.TryParse(lastUpdatedStr, out lastUpdated))
		{
			// If it's less than 30 days since last update, skip update
			if ((today - lastUpdated).TotalDays < 30)
			{
				needsUpdate = false;
			}
		}

		if (needsUpdate)
		{
			// Fetch from Twitch API
			var info = CPH.TwitchGetExtendedUserInfoByLogin(user);
			if (info != null && !string.IsNullOrEmpty(info.ProfileImageUrl))
			{
				CPH.SetTwitchUserVar(user, "DisplayName", info.UserName, true);
				CPH.SetTwitchUserVar(user, "ProfileImageUrl", info.ProfileImageUrl, true);
				CPH.SetTwitchUserVar(user, "ProfileImageUrl_LastUpdated", today.ToString("yyyy-MM-dd"), true);
			}
		}
		// If not needed, do nothing (uses previously saved image)
	}
	
	public List<string> GetConnectedSessions()
    {
        var json = CPH.GetGlobalVar<string>("VofM_WebsocketSessions", true);
        if (string.IsNullOrEmpty(json))
            return new List<string>();
        return Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
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
