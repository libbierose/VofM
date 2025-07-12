// ===================
// VofM Dashboard Main JS
// ===================

// --- WebSocket Setup ---
const wsURL = "ws://127.0.0.1:9292/"; // <-- Change if your backend uses another port!
let ws,
	leaderboardDataTable = null,
	liveUpdatesEnabled = true,
	liveUpdatesFirst = true,
	allMonths = [],
	selectedMonth = "",
	currentEntriesMap = {};

let previousPodium = [null, null, null];

// --- Status Helper ---
setStatus = (text = "", style = "info") => {
	const s = document.getElementById("status");
	let display = "";
	if (text) {
		display = `<span class="status-${style}">${text}</span>`;
	}
	s.innerHTML = display;
	console.log(`[Status] ${style}: ${text}`);
};

let isLeaderboardUpdating = false;
let leaderboardUpdateCache = null;

// --- Leaderboard Rendering ---
renderLeaderboard = (entries) => {
	console.log("[Leaderboard] Rendering entries:", entries);
	if (!entries || !Array.isArray(entries)) return;

	const podium = [entries[0], entries[1], entries[2]];
	const tableEntries = entries.slice(3);

	renderPodium(podium, previousPodium);
	previousPodium = [podium[0], podium[1], podium[2]];

	if (leaderboardDataTable) {
		if (isLeaderboardUpdating) {
			leaderboardUpdateCache = entries;
			setStatus("Updating leaderboard... New data available", "waiting");
			console.log("[Leaderboard] Already updating, caching new data for later");
			return;
		}
		isLeaderboardUpdating = true;
		if (liveUpdatesEnabled && !liveUpdatesFirst) {
			const podiumUsernames = podium.filter((u) => u).map((u) => (u.displayName || u.user || "").toLowerCase());
			renderTable(tableEntries, podiumUsernames);
		} else {
			renderTable(tableEntries);
			liveUpdatesFirst = false;
		}
	} else {
		renderTable(tableEntries);
	}

	// Update the map of points for future highlighting
	let newEntriesMap = {};
	entries.slice(3).forEach((e) => {
		let key = (e.user || e.displayName || "").toLowerCase();
		newEntriesMap[key] = e.points;
	});
	currentEntriesMap = newEntriesMap;
	console.log("[Leaderboard] Updated currentEntriesMap:", currentEntriesMap);

	isLeaderboardUpdating = false;
	setStatus("Leaderboard updated", "success");
	console.log("[Leaderboard] Render complete");
	if (leaderboardUpdateCache) {
		console.log("[Leaderboard] Processing cached entries after update");
		isLeaderboardUpdating = true;
		setStatus("Processing cached leaderboard data...", "waiting");
		const cachedEntries = leaderboardUpdateCache;
		leaderboardUpdateCache = null;
		renderLeaderboard(cachedEntries);
	}
};

renderPodium = (podium, previousPodium) => {
	podium.forEach((user, i) => {
		let key = user ? (user.displayName || user.user || "") + (user.points ?? "") : "";
		let oldKey = previousPodium[i] ? (previousPodium[i].displayName || previousPodium[i].user || "") + (previousPodium[i].points ?? "") : "";
		let elAvatar = document.getElementById(`podiumAvatar${i + 1}`);
		let elName = document.getElementById(`podiumName${i + 1}`);
		let elPoints = document.getElementById(`podiumPoints${i + 1}`);
		elAvatar.src = user?.avatar || "placeholder.png";
		elName.textContent = user?.displayName || user?.user || ["Second", "First", "Third"][i];
		elPoints.textContent = (user?.points != null ? user.points : "0") + " pts";
		let podiumCard = elAvatar.closest(".podium-card");
		if (key !== oldKey && podiumCard && liveUpdatesEnabled) {
			podiumCard.classList.add("glow-update");
			setTimeout(() => {
				podiumCard.classList.remove("glow-update");
			}, 1200);
			console.log(`[Podium] Updated position ${i + 1}:`, user);
		}
	});
};

renderTable = (tableEntries, podiumUsernames = []) => {
	if (leaderboardDataTable) {
		if (podiumUsernames.length) {
			$("#leaderboardTable tbody tr").each(function () {
				const $tds = $(this).find("td");
				let cellText = $("<div>").html($tds.eq(2).html()).text().trim().toLowerCase();
				if (podiumUsernames.includes(cellText)) {
					leaderboardDataTable.row($(this)).remove();
				}
			});
			leaderboardDataTable.draw(false);
		}
		let changedUsers = [];
		tableEntries.forEach((entry, idx) => {
			let userKey = entry.user || entry.displayName || "";
			let prevPoints = currentEntriesMap[userKey.toLowerCase()];
			let changed = prevPoints != null && prevPoints !== entry.points;
			let rowIdx = leaderboardDataTable.rows((i, data) => {
				let cell = data[2] || "";
				let cellText = $("<div>").html(cell).text();
				return cellText.toLowerCase() === userKey.toLowerCase();
			});
			if (rowIdx.count()) {
				leaderboardDataTable
					.row(rowIdx.indexes()[0])
					.data([
						idx + 4,
						`<img src="${entry.avatar || "placeholder.png"}" class="leaderboard-avatar">`,
						`<span class="username">${entry.displayName || entry.user}</span>`,
						`<span class="points">${entry.points}</span>`,
					]);
				if (changed) {
					changedUsers.push(userKey.toLowerCase());
					console.log(`[Leaderboard] Points changed for ${userKey}: ${prevPoints} → ${entry.points}`);
				}
			} else {
				leaderboardDataTable.row.add([
					idx + 4,
					`<img src="${entry.avatar || "placeholder.png"}" class="leaderboard-avatar">`,
					`<span class="username">${entry.displayName || entry.user}</span>`,
					`<span class="points">${entry.points}</span>`,
				]);
			}
		});
		leaderboardDataTable.draw(false);
		highlightChangedRows(changedUsers);
	} else {
		let table = $("#leaderboardTable tbody");
		table.empty();
		tableEntries.forEach((entry, idx) => {
			table.append(`
				<tr>
					<td>${idx + 4}</td>
					<td class="rank"><img src="${entry.avatar || "placeholder.png"}" class="leaderboard-avatar"></td>
					<td class="username">${entry.displayName || entry.user}</td>
					<td class="points"><span class="points">${entry.points}</span></td>
				</tr>
			`);
		});
		leaderboardDataTable = $("#leaderboardTable").DataTable({
			pageLength: 7,
			order: [[3, "desc"]],
			lengthChange: false,
			searching: true,
			info: false,
			paging: tableEntries.length > 10,
			columnDefs: [{ orderable: false, targets: [1] }],
			dom: "rtip",
		});
		$("#searchInput").on("keyup search input paste cut", function () {
			leaderboardDataTable.search(this.value).draw();
		});
	}
};

highlightChangedRows = (changedUsers) => {
	changedUsers.forEach((userKey) => {
		$("#leaderboardTable tbody tr").each(function () {
			const $tds = $(this).find("td");
			let cellText = $("<div>").html($tds.eq(2).html()).text();
			if (cellText.trim().toLowerCase() === userKey) {
				$tds.eq(3).find(".points").addClass("highlight-update");
			}
		});
	});
	setTimeout(function () {
		$(".highlight-update").removeClass("highlight-update");
	}, 1200);
};

// --- Live Updates Toggle ---
$("#liveUpdatesSwitch").on("change", function () {
	liveUpdatesEnabled = this.checked;
	liveUpdatesFirst = true;
	setLiveUpdateMode(liveUpdatesEnabled);
	console.log(`[LiveUpdates] Toggled: ${liveUpdatesEnabled}`);
	if (liveUpdatesEnabled) {
		setStatus("Live updates enabled", "success");
	} else {
		setStatus("Live updates paused", "info");
	}
});

// --- Set Live Update Mode (enables/disables controls) ---
setLiveUpdateMode = (enabled) => {
	const monthDropdown = document.getElementById("monthDropdown");
	const refreshBtn = document.getElementById("refreshBtn");

	if (enabled) {
		selectedMonth = getCurrentMonth();
		if (monthDropdown) {
			monthDropdown.value = selectedMonth;
			monthDropdown.disabled = true;
		}
		if (refreshBtn) {
			refreshBtn.disabled = true;
		}
		console.log("[LiveUpdates] Syncing leaderboard for current month");
		ws.send(JSON.stringify({ action: "getleaderboard", month: selectedMonth }));
	} else {
		if (monthDropdown) monthDropdown.disabled = false;
		if (refreshBtn) refreshBtn.disabled = false;
	}
};

// --- WebSocket Connect and Handlers ---
connectWebSocket = () => {
	console.log("[WebSocket] Connecting to", wsURL);
	ws = new WebSocket(wsURL);

	ws.onopen = () => {
		console.log("[WebSocket] Connected");
		ws.send(JSON.stringify({ action: "getmonths" }));
		websocketRequest("currentwinner");
		websocketRequest("alltimewinners");
		websocketRequest("settings");
		setLiveUpdateMode(true);
	};

	ws.onmessage = (evt) => {
		let msg;
		try {
			msg = JSON.parse(evt.data);
		} catch {
			console.warn("[WebSocket] Failed to parse message:", evt.data);
			return;
		}
		console.log("[WebSocket] Received:", msg);

		switch (msg.type) {
			case "months":
				allMonths = msg.months || [];
				selectedMonth = allMonths[0] || getCurrentMonth();
				fillMonthDropdown(allMonths);
				ws.send(JSON.stringify({ action: "getleaderboard", month: selectedMonth }));
				break;
			case "leaderboard":
				renderLeaderboard(msg.entries || []);
				setStatus("Leaderboard Updated", "success");
				break;
			case "liveleaderboard":
				if (liveUpdatesEnabled) {
					console.log("[LiveUpdates] Syncing leaderboard for current month");
					renderLeaderboard(msg.entries || []);
					setStatus("Leaderboard live update", "success");
				}
				break;
			case "currentwinner":
				renderCurrentWinner(msg);
				setStatus("Received Current Winner", "success");
				break;
			case "alltimewinners":
				renderAllTimeWinners(msg.allTimeWinners || {});
				setStatus("Received All Time Winners", "success");
				break;
			case "userstats":
				renderUserStatsModal(msg.data);
				$("#userStatsModal").modal("show");
				setStatus("Received User Stats", "success");
				break;
			case "settings-saved":
				setStatus("Settings saved!", "success");
				$("#settingsModal").modal("hide");
				break;
			case "settings":
				populateSettingsModal(msg);
				break;
			default:
				console.warn("[WebSocket] Unknown message type:", msg.type);
				break;
		}
	};
	ws.onclose = () => {
		console.warn("[WebSocket] Disconnected. Reconnecting in 2.5s...");
		setTimeout(connectWebSocket, 2500);
	};
};

// --- WebSocket Request Helper ---
websocketRequest = (action) => {
	switch (action) {
		case "currentwinner":
			setStatus("Requesting Current Winner", "waiting");
			ws.send(JSON.stringify({ action: "getcurrentwinner" }));
			break;
		case "alltimewinners":
			setStatus("Requesting All-Time Winner", "waiting");
			ws.send(JSON.stringify({ action: "getalltimewinners" }));
			break;
		case "settings":
			setStatus("Requesting Settings", "waiting");
			ws.send(JSON.stringify({ action: "getsettings" }));
			break;
		default:
			console.warn("[WebSocket] Unknown request action:", action);
			break;
	}
};

// --- Render Current Winner Card ---
renderCurrentWinner = (user) => {
	console.log("[CurrentWinner] Rendering:", user);
	document.getElementById("currentWinnerAvatar").src = user.avatar || "https://placehold.co/64x64";
	document.getElementById("currentWinnerName").innerText = user.displayName || user.user || "None";
	document.getElementById("currentWinnerPoints").innerText = (user.points || 0) + " points";
};

// --- Render All-Time Winners Sidebar ---
renderAllTimeWinners = (allTime) => {
	console.log("[AllTimeWinners] Rendering:", allTime);
	let html = "";
	const monthKeys = Object.keys(allTime || {}).sort((a, b) => b.localeCompare(a));
	if (monthKeys.length === 0) {
		html = `<div class="text-secondary small">No data yet.</div>`;
	} else {
		monthKeys.forEach((month) => {
			html += `<div class="alltime-month fw-semibold mt-2">${month}</div>`;
			html += `<div class="alltime-month-list">`;
			(allTime[month] || []).forEach((w, idx) => {
				const medals = ["<span class='at-medal gold'>🥇</span>", "<span class='at-medal silver'>🥈</span>", "<span class='at-medal bronze'>🥉</span>"];
				html += `
				  <div class="alltime-winner-row d-flex align-items-center mb-2">
					${medals[idx] || ""}
					<div class="alltime-winner-info ms-2">
					  <div class="alltime-winner-name fw-semibold">${w.DisplayName || w.UserName || w.user || "?"}</div>
					  <div class="alltime-winner-points text-secondary">${w.Points || w.points || "0"} pts</div>
					</div>
				  </div>
				`;
			});
			html += `</div>`;
		});
	}
	document.getElementById("allTimeWinners").innerHTML = html;
};

// --- Utility: Get Current Month as YYYY-MM ---
getCurrentMonth = () => {
	let d = new Date(),
		m = (d.getMonth() + 1).toString().padStart(2, "0"),
		y = d.getFullYear();
	return `${y}-${m}`;
};

// --- Fill Month Dropdown ---
fillMonthDropdown = (months) => {
	let sel = document.getElementById("monthDropdown");
	sel.innerHTML = "";
	months.forEach((m) => {
		let opt = document.createElement("option");
		opt.value = m;
		opt.textContent = m === getCurrentMonth() ? `${m} (Current)` : m;
		sel.appendChild(opt);
	});
	sel.value = selectedMonth || getCurrentMonth();
	console.log("[Dropdown] Months filled:", months);
};

// --- Refresh Button (manual, disables live) ---
$("#refreshLeaderboard").on("click", function () {
	liveUpdatesEnabled = false;
	$("#liveToggle").prop("checked", false);
	setStatus("Manual refresh...", "waiting");
	console.log("[Refresh] Manual refresh triggered");
	ws.send(
		JSON.stringify({
			action: "getleaderboard",
			month: selectedMonth || getCurrentMonth(),
		})
	);
});

// --- Month Dropdown Change ---
document.getElementById("monthDropdown").onchange = () => {
	selectedMonth = this.value;
	setStatus("Fetching leaderboard...", "waiting");
	console.log("[Dropdown] Month changed:", selectedMonth);
	ws.send(JSON.stringify({ action: "getleaderboard", month: selectedMonth }));
};

// --- Settings Modal Show: Fetch Settings ---
$("#settingsModal").on("show.bs.modal", function () {
	console.log("[Settings] Modal opened, requesting settings");
	ws.send(JSON.stringify({ action: "getsettings" }));
});

// --- Populate Settings Modal Fields ---
populateSettingsModal = (settings) => {
	console.log("[Settings] Populating modal with:", settings);
	$("#inputResetDay").val(settings.resetDay || 1);
	$("#inputRedemptionMode").val(settings.redemptionPointMode || "perRedemption");
	$("#inputCurrentWinner").val(settings.currentWinner || "");

	// Base Points
	const basePoints = settings.basePoints || {};
	const $basePointsList = $("#basePointsList");
	$basePointsList.empty();
	Object.entries(basePoints).forEach(([key, value]) => {
		$basePointsList.append(`
		<div class="row align-items-center mb-2">
			<div class="col-7 text-start">
				<label class="form-label mb-0">${key}</label>
			</div>
			<div class="col-5 text-end">
				<input type="number" class="form-control form-control-sm base-point-input vofm-input" data-key="${key}" value="${value}" step="0.01" min="0" />
			</div>
		</div>
	`);
	});

	// Streak Types
	const streakTypes = settings.streakTypes || {};
	const $streakTypesList = $("#streakTypesList");
	$streakTypesList.empty();
	Object.entries(streakTypes).forEach(([key, value]) => {
		$streakTypesList.append(`
		<div class="form-check form-switch mb-2 d-flex align-items-center">
			<input type="checkbox" class="form-check-input streak-type-input me-2" data-key="${key}" id="streakSwitch_${key}" ${value ? "checked" : ""} />
			<label class="form-check-label ms-1" for="streakSwitch_${key}">${key}</label>
		</div>
	`);
	});
};

// --- Open User Stats Modal for a Given Username ---
openUserStats = (username) => {
	if (!username) return;
	setStatus("Loading user stats...", "waiting");
	console.log("[UserStats] Requesting stats for:", username);
	userStatsCurrent = window.userStatsCurrent || {};
	userStatsCurrent.user = username; // <-- Set the user context here
	userStatsCurrent.month = getCurrentMonth();
	ws.send(
		JSON.stringify({
			action: "getuser",
			user: username,
			month: getCurrentMonth(), // or selectedMonth if using dropdown!
		})
	);
	// Modal will show after renderUserStatsModal is called from ws.onmessage
};

// --- Click Handlers for User Stats Modal ---
$("#currentWinnerCard, #currentWinnerName, #currentWinnerAvatar").on("click", function () {
	let username = $("#currentWinnerName").text().trim();
	openUserStats(username);
});
$(".podium-card").on("click", function () {
	let username = $(this).find(".podium-username").text().trim();
	openUserStats(username);
});
$("#leaderboardTable tbody").on("click", "td.username", function () {
	let username = $(this).text().trim();
	openUserStats(username);
});
// Place this ONCE after DataTable is initialized:
$("#leaderboardTable tbody").on("click", "td.username, span.username", function () {
	// If it's a <span>, get the text directly
	// If it's a <td>, get the text of the span inside or the td itself
	let username = $(this).hasClass("username") ? $(this).text().trim() : $(this).find(".username").text().trim();
	if (!username) username = $(this).text().trim();
	openUserStats(username);
});

// --- Format Watch Time Utility ---
formatWatchTime = (seconds) => {
	if (!seconds || isNaN(seconds)) return "0 min";
	const mins = Math.floor(seconds / 60);
	const hours = Math.floor(mins / 60);
	const days = Math.floor(hours / 24);
	const months = Math.floor(days / 30);
	const years = Math.floor(months / 12);

	let remainingMonths = months % 12;
	let remainingDays = days % 30;
	let remainingHours = hours % 24;
	let remainingMins = mins % 60;

	let parts = [];
	if (years) parts.push(`${years}y`);
	if (remainingMonths) parts.push(`${remainingMonths}m`);
	if (remainingDays) parts.push(`${remainingDays}d`);
	if (remainingHours) parts.push(`${remainingHours}h`);
	if (remainingMins) parts.push(`${remainingMins}min`);
	return parts.length ? parts.join(" ") : "0 min";
};

// --- Render User Stats Modal ---
renderUserStatsModal = (data) => {
	console.log("[UserStats] Rendering modal for:", data);

	// Defensive: handle no/invalid data
	if (!data) {
		document.getElementById("userStatsDisplayName").innerText = "Viewer";
		document.getElementById("userStatsAvatar").src = "placeholder.png";
		document.getElementById("userStatsAllTimeBlock").innerHTML = "";
		document.getElementById("userStatsTable").innerHTML = `<tr><td colspan="2" class="text-center text-muted">No data loaded.</td></tr>`;
		return;
	}

	// --- Profile Info ---
	document.getElementById("userStatsDisplayName").innerText = data.DisplayName || data.UserName || "Viewer";
	document.getElementById("userStatsAvatar").src = data.ProfileImageUrl || "placeholder.png";

	// --- All-Time Stats Cards ---
	let allTimeStats = [
		{ label: "Points", value: data.VofM_Points ?? 0 },
		{ label: "Redeem", value: data.AllTimeRedemptions ?? 0 },
		{ label: "Spent", value: data.AllTimePointsSpent ?? 0 },
		{ label: "Chats", value: data.AllTimeChatMessages ?? 0 },
		{ label: "Watch", value: formatWatchTime(data.AllTimeWatchSeconds) },
	];
	let allTimeHTML = `
	  <div class="alltime-section">
		<div class="alltime-title">All Time</div>
		<div class="alltime-stats-cards">
		  ${allTimeStats
				.map(
					(stat) => `
			<div class="alltime-card">
			  <div class="alltime-label">${stat.label}</div>
			  <div class="alltime-value">${stat.value}</div>
			</div>
		  `
				)
				.join("")}
		</div>
	  </div>
	`;
	document.getElementById("userStatsAllTimeBlock").innerHTML = allTimeHTML;

	// --- Month Dropdown ---
	let months = typeof allMonths !== "undefined" && Array.isArray(allMonths) && allMonths.length ? allMonths : [getCurrentMonth()];
	userStatsCurrent = window.userStatsCurrent || {};
	let currentMonth = userStatsCurrent.month || getCurrentMonth();
	let dropdown = document.getElementById("userStatsMonthDropdown");
	dropdown.innerHTML = "";
	months.forEach((m) => {
		let opt = document.createElement("option");
		opt.value = m;
		opt.textContent = m === getCurrentMonth() ? `${m} (Current)` : m;
		dropdown.appendChild(opt);
	});
	dropdown.value = currentMonth;

	dropdown.onchange = null;
	dropdown.onchange = () => {
		userStatsCurrent.month = this.value;
		console.log("[UserStats] Month changed:", this.value);
		ws.send(JSON.stringify({ action: "getuser", user: userStatsCurrent.user, month: userStatsCurrent.month }));
	};

	// --- Monthly Stats Table ---
	let stats = data.MonthlyStats || data;
	const fields = [
		["Points", stats.Points],
		["Redemptions", stats.Redemptions],
		["Points Spent", stats.PointsSpent],
		["Chat Msgs", stats.ChatMessages],
		["Watch Time", formatWatchTime(stats.WatchSeconds)],
		["Highest Streak", stats.HighestStreak],
		["Streams Watched", stats.StreamsWatched],
		["Bits Cheered", stats.BitsCheered],
		["Gifted Subs", stats.GiftedSubs],
		["Timeouts", stats.Timeouts],
		["Bans", stats.Bans],
	];

	let tableHTML = "";
	let hasRow = false;
	fields.forEach(([label, value]) => {
		if (typeof value !== "undefined" && value !== null) {
			hasRow = true;
			tableHTML += `<tr>
				<td>${label}</td>
				<td class="text-end">${value || 0}</td>
			</tr>`;
		}
	});
	if (!hasRow) {
		tableHTML = `<tr><td colspan="2" class="text-center text-muted">No monthly stats available.</td></tr>`;
	}
	document.getElementById("userStatsTable").innerHTML = tableHTML;

	// --- Show modal (Bootstrap 5) ---
	let modal = document.getElementById("userStatsModal");
	if (!window.userStatsModalInstance) {
		window.userStatsModalInstance = new bootstrap.Modal(modal, { keyboard: true });
	}
	window.userStatsModalInstance.show();
};

// --- Save Settings Button ---
$("#saveSettings").on("click", function () {
	console.log("[Settings] Save button clicked");
	const resetDay = parseInt($("#inputResetDay").val(), 10) || 1;
	const redemptionPointMode = $("#inputRedemptionMode").val();
	const currentWinner = $("#inputCurrentWinner").val();

	// Default keys and values
	const basePointDefaults = {
		Streak: 1.0,
		Watchtime: 1.0,
		"Chat Message": 1.0,
		"Reward Redemption": 1.0,
		Cheer: 1.0,
		"Gift Subscription": 1.0,
		Subscription: 1.0,
		Bans: 1.0,
		Timeouts: 1.0,
		Warnings: 1.0,
	};
	const streakTypeDefaults = {
		Streak: false,
		Watchtime: false,
		"Chat Message": false,
		"Reward Redemption": false,
		Cheer: false,
		"Gift Subscription": false,
		Subscription: false,
		Bans: false,
		Timeouts: false,
		Warnings: false,
	};

	// Gather base points
	let basePoints = { ...basePointDefaults };
	$(".base-point-input").each(function () {
		const key = $(this).data("key");
		const value = parseFloat($(this).val());
		if (!isNaN(value)) basePoints[key] = value;
	});

	// Gather streak types
	let streakTypes = { ...streakTypeDefaults };
	$(".streak-type-input").each(function () {
		const key = $(this).data("key");
		const value = $(this).is(":checked");
		streakTypes[key] = value;
	});

	const payload = {
		action: "setsettings",
		resetDay,
		redemptionPointMode,
		currentWinner,
		basePoints,
		streakTypes,
	};

	console.log("[Settings] Sending payload:", payload);
	ws.send(JSON.stringify(payload));
	setStatus("Saving settings...", "waiting");
});

// --- On load ---
$(document).ready(function () {
	console.log("[Init] Document ready, connecting WebSocket and enabling live updates");
	connectWebSocket();
});
