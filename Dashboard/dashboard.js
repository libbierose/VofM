const wsURL = "ws://127.0.0.1:9292/"; // <-- Change if your backend uses another port!
let ws,
	leaderboardDataTable = null,
	liveUpdatesEnabled = true,
	liveUpdatesFirst = true,
	allMonths = [],
	selectedMonth = "",
	currentEntriesMap = {};

let previousPodium = [null, null, null];

function setStatus(text = "", style = "info") {
	const s = document.getElementById("status");
	let display = "";
	if (text) {
		display = `<span class="status-${style}">${text}</span>`;
	}
	s.innerHTML = display;
}

function renderLeaderboard(entries) {
	// Defensive: always handle missing data
	if (!entries || !Array.isArray(entries)) return;

	// Split top 3 for podium, rest for table
	const podium = [entries[0], entries[1], entries[2]];
	const tableEntries = entries.slice(3);

	// --- PODIUM: update with glow if changed ---
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
		}
	});
	previousPodium = [podium[0], podium[1], podium[2]];

	// --- TABLE: live update vs initial/manual load ---
	if (leaderboardDataTable) {
		if (liveUpdatesEnabled && !liveUpdatesFirst) {
			liveUpdatesFirst = false;
			// Live update (only update rows that change, highlight them)
			tableEntries.forEach((entry, idx) => {
				let userKey = entry.user || entry.displayName || "";
				let prevPoints = currentEntriesMap[userKey.toLowerCase()];
				let changed = prevPoints != null && prevPoints !== entry.points;

				// Find row for this user
				let rowIdx = leaderboardDataTable.rows((i, data) => {
					return (data[2] || "").toLowerCase() === userKey.toLowerCase();
				});

				if (rowIdx.count()) {
					leaderboardDataTable
						.row(rowIdx.indexes()[0])
						.data([
							idx + 4,
							`<img src="${entry.avatar || "placeholder.png"}" class="leaderboard-avatar">`,
							entry.displayName || entry.user,
							`<span class="points${changed ? " highlight-update" : ""}">${entry.points}</span>`,
						]);
					if (changed) {
						setTimeout(() => {
							$(".highlight-update").removeClass("highlight-update");
						}, 1200);
					}
				} else {
					// Row for user not found (rare, new entry) - add new
					leaderboardDataTable.row.add([
						idx + 4,
						`<img src="${entry.avatar || "placeholder.png"}" class="leaderboard-avatar">`,
						entry.displayName || entry.user,
						`<span class="points">${entry.points}</span>`,
					]);
				}
			});
			leaderboardDataTable.draw(false);
		} else {
			// Manual/first load: full clear, no highlight
			leaderboardDataTable.clear();
			tableEntries.forEach((entry, idx) => {
				leaderboardDataTable.row.add([
					idx + 4,
					`<img src="${entry.avatar || "placeholder.png"}" class="leaderboard-avatar">`,
					entry.displayName || entry.user,
					`<span class="points">${entry.points}</span>`,
				]);
			});
			leaderboardDataTable.draw(false);
		}
	} else {
		// Initial table setup
		let table = $("#leaderboardTable tbody");
		table.empty();
		tableEntries.forEach((entry, idx) => {
			table.append(`
                <tr>
                    <td>${idx + 4}</td>
                    <td><img src="${entry.avatar || "placeholder.png"}" class="leaderboard-avatar"></td>
                    <td>${entry.displayName || entry.user}</td>
                    <td><span class="points">${entry.points}</span></td>
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

	// Update the map of points for future highlighting
	currentEntriesMap = {};
	tableEntries.forEach((e) => {
		let key = (e.user || e.displayName || "").toLowerCase();
		currentEntriesMap[key] = e.points;
	});
}

document.getElementById("liveUpdatesSwitch").addEventListener("change", function () {
	liveUpdatesEnabled = this.checked;
	if (liveUpdatesEnabled) {
		setStatus("Live updates enabled", "success");
	} else {
		setStatus("Live updates paused", "info");
	}
});

// --- Live Updates Toggle ---
$("#liveUpdatesSwitch").on("change", function () {
	liveUpdatesEnabled = this.checked;
	liveUpdatesFirst = true;
	setLiveUpdateMode(liveUpdatesEnabled);
	if (liveUpdatesEnabled) {
		setStatus("Live updates enabled", "success");
	} else {
		setStatus("Live updates paused", "info");
	}
});

function setLiveUpdateMode(enabled) {
	const monthDropdown = document.getElementById("monthDropdown");
	const refreshBtn = document.getElementById("refreshBtn");

	if (enabled) {
		// Set to current month and disable both controls
		selectedMonth = getCurrentMonth();
		if (monthDropdown) {
			monthDropdown.value = selectedMonth;
			monthDropdown.disabled = true;
		}
		if (refreshBtn) {
			refreshBtn.disabled = true;
		}
		// Optionally, refetch leaderboard to sync
		ws.send(JSON.stringify({ action: "getleaderboard", month: selectedMonth }));
	} else {
		// Enable controls
		if (monthDropdown) monthDropdown.disabled = false;
		if (refreshBtn) refreshBtn.disabled = false;
	}
}

// --- Websocket connect and handlers ---
function connectWebSocket() {
	ws = new WebSocket(wsURL);

	ws.onopen = () => {
		// Initial load
		ws.send(JSON.stringify({ action: "getmonths" }));
		websocketRequest("currentwinner");
		websocketRequest("alltimewinners");
		websocketRequest("settings");
	};

	ws.onmessage = (evt) => {
		let msg;
		try {
			msg = JSON.parse(evt.data);
		} catch {
			return;
		}
		if (msg.type === "months") {
			allMonths = msg.months || [];
			selectedMonth = allMonths[0] || getCurrentMonth();
			fillMonthDropdown(allMonths);
			ws.send(JSON.stringify({ action: "getleaderboard", month: selectedMonth }));
		}
		if (msg.type === "leaderboard") {
			renderLeaderboard(msg.entries || []);
			setStatus("Leaderboard Updated", "success");
		}
		if (msg.type === "liveleaderboard" && liveUpdatesEnabled) {
			renderLeaderboard(msg.entries || []);
			setStatus("Leaderboard live update", "success");
		}
		if (msg.type === "currentwinner") {
			renderCurrentWinner(msg);
			setStatus("Received Current Winner", "success");
		}
		if (msg.type === "alltimewinners") {
			renderAllTimeWinners(msg.allTimeWinners || {});
			setStatus("Received All Time Winners", "success");
		}
		if (msg.type === "userstats") {
			renderUserStatsModal(msg.data); // Your existing function
			// Show the modal if not already shown
			$("#userStatsModal").modal("show");
		}
	};
	ws.onclose = () => setTimeout(connectWebSocket, 2500);
}

function requestSettings() {
	setStatus("Loading settings...", "waiting");
	ws.send(JSON.stringify({ action: "getsettings" }));
	ws.send(JSON.stringify({ action: "getcurrentwinner" }));
	ws.send(JSON.stringify({ action: "getalltimewinners" }));
}

websocketRequest = (action) => {
	switch (action) {
		case "currentwinner":
			setStatus("Requesting Current Winner", "waiting");
			ws.send(JSON.stringify({ action: "getcurrentwinner" }));
			break;
		case "alltimewinners":
			setStatus("Requesting All-Time Winner", "waiting");
			ws.send(JSON.stringify({ action: "getalltimewinners" }));
		case "settings":
			setStatus("Requesting Settings", "waiting");
			ws.send(JSON.stringify({ action: "getsettings" }));
		default:
			break;
	}
};

function renderCurrentWinner(user) {
	document.getElementById("currentWinnerAvatar").src = user.avatar || "https://placehold.co/64x64";
	document.getElementById("currentWinnerName").innerText = user.displayName || user.user || "None";
	document.getElementById("currentWinnerPoints").innerText = (user.points || 0) + " points";
}

function renderAllTimeWinners(allTime) {
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
}

function getCurrentMonth() {
	let d = new Date(),
		m = (d.getMonth() + 1).toString().padStart(2, "0"),
		y = d.getFullYear();
	return `${y}-${m}`;
}

function fillMonthDropdown(months) {
	let sel = document.getElementById("monthDropdown");
	sel.innerHTML = "";
	months.forEach((m) => {
		let opt = document.createElement("option");
		opt.value = m;
		opt.textContent = m === getCurrentMonth() ? `${m} (Current)` : m;
		sel.appendChild(opt);
	});
	sel.value = selectedMonth || getCurrentMonth();
}

// --- Refresh button (manual, disables live) ---
$("#refreshLeaderboard").on("click", function () {
	liveUpdatesEnabled = false;
	$("#liveToggle").prop("checked", false);
	setStatus("Manual refresh...", "waiting");
	ws.send(
		JSON.stringify({
			action: "getleaderboard",
			month: selectedMonth || getCurrentMonth(),
		})
	);
});

document.getElementById("monthDropdown").onchange = function () {
	selectedMonth = this.value;
	setStatus("Fetching leaderboard...", "waiting");
	ws.send(JSON.stringify({ action: "getleaderboard", month: selectedMonth }));
};

// Populate modal fields from current settings
function populateSettingsModal(settings) {
	$("#resetDayInput").val(settings.resetDay || 1);
	$("#redemptionModeSelect").val(settings.redemptionPointMode || "perRedemption");
	$("#currentWinnerInput").val(settings.currentWinner || "");
}

// Helper: Open stats modal for a given username
function openUserStats(username) {
	if (!username) return;
	setStatus("Loading user stats...", "waiting");
	ws.send(
		JSON.stringify({
			action: "getuser",
			user: username,
			month: getCurrentMonth(), // or selectedMonth if using dropdown!
		})
	);
	// Modal will show after renderUserStatsModal is called from ws.onmessage
}

// 1. Current Winner click
$("#currentWinnerCard, #currentWinnerName, #currentWinnerAvatar").on("click", function () {
	let username = $("#currentWinnerName").text().trim();
	openUserStats(username);
});

// 2. Podium cards click
$(".podium-card").on("click", function () {
	let username = $(this).find(".podium-username").text().trim();
	openUserStats(username);
});

// 3. Leaderboard table usernames
$("#leaderboardTable tbody").on("click", "td.username", function () {
	let username = $(this).text().trim();
	openUserStats(username);
});

function renderUserStatsModal(data) {
	// Defensive: handle no/invalid data
	if (!data) {
		document.getElementById("userStatsDisplayName").innerText = "Viewer";
		document.getElementById("userStatsAvatar").src = "placeholder.png";
		document.getElementById("userStatsAllTimePoints").innerText = "0";
		document.getElementById("userStatsAllTimeRedemptions").innerText = "0";
		document.getElementById("userStatsAllTimeSpent").innerText = "0";
		document.getElementById("userStatsAllTimeChatMsgs").innerText = "0";
		document.getElementById("userStatsAllTimeWatch").innerText = "0 min";
		document.getElementById("userStatsTable").innerHTML = `<tr><td colspan="2" class="text-center text-muted">No data loaded.</td></tr>`;
		return;
	}

	// --- Profile and All-Time Stats ---
	let allTimeStats = [
		{ label: "Points", value: data.VofM_Points ?? 0 },
		{ label: "Redemptions", value: data.AllTimeRedemptions ?? 0 },
		{ label: "Points Spent", value: data.AllTimePointsSpent ?? 0 },
		{ label: "Chat Msgs", value: data.AllTimeChatMessages ?? 0 },
		{ label: "Watch Time", value: (data.AllTimeWatchSeconds ? Math.round(data.AllTimeWatchSeconds / 60) : 0) + " min" },
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
	// Assume you have all months in a global array called "allMonths" (else getCurrentMonth only)
	let months = typeof allMonths !== "undefined" && Array.isArray(allMonths) && allMonths.length ? allMonths : [getCurrentMonth()];

	let currentMonth = typeof userStatsCurrent !== "undefined" && userStatsCurrent.month ? userStatsCurrent.month : getCurrentMonth();

	let dropdown = document.getElementById("userStatsMonthDropdown");
	dropdown.innerHTML = "";
	months.forEach((m) => {
		let opt = document.createElement("option");
		opt.value = m;
		opt.textContent = m === getCurrentMonth() ? `${m} (Current)` : m;
		dropdown.appendChild(opt);
	});
	dropdown.value = currentMonth;

	// Prevent event stacking
	dropdown.onchange = null;
	dropdown.onchange = function () {
		// Optionally show loading here
		userStatsCurrent = userStatsCurrent || {};
		userStatsCurrent.month = this.value;
		// Example: use your websocket to fetch new stats
		ws.send(JSON.stringify({ action: "getuser", user: userStatsCurrent.user, month: userStatsCurrent.month }));
	};

	// --- Monthly Stats Table (use data as flat object or .MonthlyStats subobject if present) ---
	let stats = data.MonthlyStats || data;
	const fields = [
		["Points", stats.Points],
		["Redemptions", stats.Redemptions],
		["Points Spent", stats.PointsSpent],
		["Chat Msgs", stats.ChatMessages],
		["Watch Time", stats.WatchSeconds ? Math.round(stats.WatchSeconds / 60) + " min" : "0"],
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
}

// --- On load ---
$(document).ready(function () {
	connectWebSocket();
	setLiveUpdateMode(true);
});
