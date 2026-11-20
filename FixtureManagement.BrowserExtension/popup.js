// Fixture Manager – FullTime: the popup. Signs in, starts a send, and shows progress from chrome.storage.session.

const $ = (id) => document.getElementById(id);

function showError(text) {
  $("error").textContent = text || "";
  $("error").hidden = !text;
}

function ask(message) {
  return chrome.runtime.sendMessage(message);
}

// ---------- leagues ----------
// The website's plan lists the teams to fetch, each with its league. The popup shows one tick-box per league (with its
// team count); the choice is kept in chrome.storage.local as the league numbers ticked (missing = all).

let planLeagues = [];   // [{ league, name, teams }]

async function loadLeagues() {
  const answer = await ask({ type: "plan" });
  if (!answer.ok) {
    $("league-list").textContent = answer.error;
    return;
  }
  const byLeague = new Map();
  for (const team of answer.plan.teams) {
    const entry = byLeague.get(team.league) || { league: team.league, name: team.leagueName, teams: 0 };
    entry.teams++;
    byLeague.set(team.league, entry);
  }
  planLeagues = [...byLeague.values()].sort((a, b) => a.name.localeCompare(b.name));
  const { leagues: saved } = await chrome.storage.local.get("leagues");
  renderLeagues(Array.isArray(saved) ? saved : planLeagues.map((l) => l.league));
}

function renderLeagues(ticked) {
  $("league-list").classList.remove("hint");
  $("league-list").replaceChildren(...planLeagues.map((l) => {
    const label = document.createElement("label");
    const box = document.createElement("input");
    box.type = "checkbox";
    box.value = l.league;
    box.checked = ticked.includes(l.league);
    box.addEventListener("change", saveLeagues);
    label.append(box, `${l.name} (${l.teams} ${l.teams === 1 ? "team" : "teams"})`);
    return label;
  }));
}

function tickedLeagues() {
  return [...$("league-list").querySelectorAll("input:checked")].map((box) => Number(box.value));
}

async function saveLeagues() {
  await chrome.storage.local.set({ leagues: tickedLeagues() });
}

async function renderAuth() {
  const { auth } = await chrome.storage.local.get("auth");
  $("signed-out").hidden = !!auth;
  $("signed-in").hidden = !auth;
  if (auth) {
    $("who-server").textContent = new URL(auth.server).host;
    $("who-email").textContent = auth.email;
    $("open-site").href = auth.server;
    if (planLeagues.length === 0) loadLeagues();
  } else {
    const { lastServer } = await chrome.storage.local.get("lastServer");
    $("server").value = lastServer || "https://tufixturemanager.azurewebsites.net/";
  }
}

function renderRun(run) {
  if (!run || run.status === "idle") {
    $("progress").hidden = true;
    $("send").disabled = false;
    return;
  }
  $("progress").hidden = false;
  $("send").disabled = run.status === "running";
  $("message").textContent = run.message;
  $("message").className = run.status === "error" ? "error" : "";
  $("bar").max = Math.max(run.total, 1);
  $("bar").value = run.done;
  $("bar").hidden = run.total === 0;
  $("current").textContent = run.status === "running" && run.current ? `${run.done + 1} of ${run.total}: ${run.current}` : "";
  $("failed").replaceChildren(...run.failed.map((f) => {
    const li = document.createElement("li");
    li.textContent = `${f.teamName || "Unknown team"} – ${f.error}`;
    return li;
  }));
  const codes = run.unknownCodes || [];
  $("codes").hidden = codes.length === 0;
  $("codes-list").replaceChildren(...codes.map((c) => {
    const li = document.createElement("li");
    li.textContent = `${c.leagueName}: "${c.code}" × ${c.count} (e.g. ${c.example})`;
    return li;
  }));
  $("open-site").hidden = run.status !== "done";
}

$("sign-in").addEventListener("click", async () => {
  showError("");
  const server = $("server").value.trim();
  let url;
  try {
    url = new URL(server);
    if (url.protocol !== "https:") throw new Error();
  } catch {
    return showError("Enter the site's address, starting https://");
  }
  // The extension may only talk to the sites listed in its manifest (host_permissions).
  const allowed = chrome.runtime.getManifest().host_permissions
    .map((p) => new URL(p.replace("/*", "/")).hostname)
    .filter((h) => h !== "fulltime.thefa.com");
  if (!allowed.includes(url.hostname)) {
    return showError(`This extension only works with: ${allowed.join(", ")}.`);
  }
  await chrome.storage.local.set({ lastServer: server });
  $("sign-in").disabled = true;
  const answer = await ask({ type: "signIn", server, email: $("email").value.trim(), password: $("password").value });
  $("sign-in").disabled = false;
  $("password").value = "";
  if (!answer.ok) return showError(answer.error);
  await renderAuth();
});

$("sign-out").addEventListener("click", async () => {
  showError("");
  await ask({ type: "signOut" });
  planLeagues = [];
  $("league-list").textContent = "Loading the leagues…";
  await renderAuth();
});

$("send").addEventListener("click", async () => {
  showError("");
  const leagues = tickedLeagues();
  if (planLeagues.length > 0 && leagues.length === 0) return showError("Tick at least one league.");
  // Before the leagues have loaded, send everything (null = all).
  const answer = await ask({ type: "send", leagues: planLeagues.length > 0 ? leagues : null });
  if (!answer.ok) showError(answer.error);
});

$("all-leagues").addEventListener("click", async () => {
  renderLeagues(planLeagues.map((l) => l.league));
  await saveLeagues();
});

$("no-leagues").addEventListener("click", async () => {
  renderLeagues([]);
  await saveLeagues();
});

chrome.storage.onChanged.addListener((changes, area) => {
  if (area === "session" && changes.run) renderRun(changes.run.newValue);
  if (area === "local" && changes.auth) renderAuth();
});

(async () => {
  await renderAuth();
  const { run } = await chrome.storage.session.get("run");
  renderRun(run);
})();
