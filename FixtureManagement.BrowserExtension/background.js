// Fixture Manager – FullTime: the background worker.
//
// When the user clicks Send in the popup:
//   1. ask Fixture Manager which FullTime team pages to fetch (GET /api/fulltime/plan),
//   2. fetch each page from inside the user's FullTime tab, one at a time with a pause between them,
//   3. send the pages, unchanged, to Fixture Manager (POST /api/fulltime/upload), which reads and saves them.
// Only ever runs when the user clicks Send – never in the background or on a schedule.
// Progress is kept in chrome.storage.session so the popup can be closed and reopened.

const PAUSE_BETWEEN_PAGES_MS = 1500;
const FULLTIME_ORIGIN = "https://fulltime.thefa.com";

// ---------- state shown by the popup ----------

const idleState = { status: "idle", message: "", total: 0, done: 0, current: "", failed: [], unknownCodes: [], result: null };

async function setState(changes) {
  const { run = idleState } = await chrome.storage.session.get("run");
  await chrome.storage.session.set({ run: { ...run, ...changes } });
}

// ---------- signing in to Fixture Manager ----------
// Uses the site's token sign-in (/api/auth/login and /api/auth/refresh). The password is never stored;
// the refresh token is kept in chrome.storage.local so the user stays signed in.

async function getAuth() {
  const { auth = null } = await chrome.storage.local.get("auth");
  return auth;
}

function apiUrl(server, path) {
  return new URL(path, server.endsWith("/") ? server : server + "/").toString();
}

async function saveTokens(server, email, tokens) {
  const auth = {
    server,
    email,
    accessToken: tokens.accessToken,
    refreshToken: tokens.refreshToken,
    // Renew a little early.
    expiresAt: Date.now() + (tokens.expiresIn - 30) * 1000,
  };
  await chrome.storage.local.set({ auth });
  return auth;
}

async function signIn(server, email, password) {
  const response = await fetch(apiUrl(server, "api/auth/login"), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  });
  if (response.status === 401) throw new Error("The email or password wasn't accepted.");
  if (!response.ok) throw new Error(`Fixture Manager answered ${response.status} when signing in.`);
  await saveTokens(server, email, await response.json());
}

async function refresh(auth) {
  const response = await fetch(apiUrl(auth.server, "api/auth/refresh"), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken: auth.refreshToken }),
  });
  if (response.status === 400 || response.status === 401) {
    await chrome.storage.local.remove("auth");
    throw new Error("Your sign-in has expired. Please sign in again.");
  }
  if (!response.ok) throw new Error(`Fixture Manager answered ${response.status} when renewing the sign-in.`);
  return saveTokens(auth.server, auth.email, await response.json());
}

/** Calls Fixture Manager with the access token, renewing it first if it's due, and once more on a 401. */
async function callApi(path, options = {}) {
  let auth = await getAuth();
  if (!auth) throw new Error("Please sign in first.");
  if (Date.now() >= auth.expiresAt) auth = await refresh(auth);

  const send = (token) => fetch(apiUrl(auth.server, path), {
    ...options,
    headers: { ...(options.headers || {}), Authorization: `Bearer ${token}` },
  });

  let response = await send(auth.accessToken);
  if (response.status === 401) {
    auth = await refresh(auth);
    response = await send(auth.accessToken);
  }
  return response;
}

// ---------- fetching FullTime pages ----------

async function findFullTimeTab() {
  const tabs = await chrome.tabs.query({ url: `${FULLTIME_ORIGIN}/*` });
  if (tabs.length === 0) throw new Error("Open any page on fulltime.thefa.com in a tab first, then click Send again.");
  return tabs.find((t) => t.active) || tabs[0];
}

/** Fetches a FullTime page from inside the FullTime tab, so it's an ordinary request from this browser. */
async function fetchInTab(tabId, url) {
  const [injection] = await chrome.scripting.executeScript({
    target: { tabId },
    world: "MAIN",
    args: [url],
    func: async (pageUrl) => {
      try {
        const response = await fetch(pageUrl, { credentials: "include" });
        return { status: response.status, html: await response.text() };
      } catch (e) {
        return { status: 0, html: "", error: String(e) };
      }
    },
  });
  return injection.result;
}

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

async function getPlan() {
  const response = await callApi("api/fulltime/plan");
  if (!response.ok) throw new Error(`Fixture Manager answered ${response.status} for the list of teams.`);
  return response.json();
}

/** leagues: the League values to fetch (numbers, as in the plan); null or missing = all. */
async function run(leagues) {
  await chrome.storage.session.set({ run: { ...idleState, status: "running", message: "Getting the list of teams…" } });
  try {
    const tab = await findFullTimeTab();

    const plan = await getPlan();
    if (Array.isArray(leagues)) plan.teams = plan.teams.filter((t) => leagues.includes(t.league));
    if (plan.teams.length === 0) throw new Error("No teams to fetch – tick at least one league.");

    const pages = [];
    const failed = [];
    await setState({ total: plan.teams.length, message: "Fetching team pages from FullTime…" });

    for (let i = 0; i < plan.teams.length; i++) {
      const team = plan.teams[i];
      await setState({ current: team.displayName, done: i });

      const page = await fetchInTab(tab.id, team.pageUrl);
      if (page.status === 200) {
        pages.push({ teamId: team.teamId, html: page.html });
      } else {
        failed.push({ teamName: team.displayName, error: page.status === 0 ? `Couldn't reach FullTime (${page.error})` : `FullTime answered ${page.status}` });
        // Refused: stop rather than keep asking. What was fetched so far is still sent.
        if (page.status === 403 || page.status === 429) {
          const skipped = plan.teams.slice(i + 1);
          skipped.forEach((t) => failed.push({ teamName: t.displayName, error: "Not fetched – FullTime refused an earlier page" }));
          break;
        }
      }
      await setState({ done: i + 1, failed });
      if (i < plan.teams.length - 1) await sleep(PAUSE_BETWEEN_PAGES_MS);
    }

    if (pages.length === 0) throw new Error("No pages could be fetched from FullTime, so nothing was sent.");

    await setState({ current: "", message: `Sending ${pages.length} pages to Fixture Manager…`, failed });
    const uploadResponse = await callApi("api/fulltime/upload", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ pages }),
    });
    if (uploadResponse.status !== 200 && uploadResponse.status !== 422) {
      throw new Error(`Fixture Manager answered ${uploadResponse.status} to the upload.`);
    }
    const result = await uploadResponse.json();
    // Pages the website couldn't use, alongside the ones that couldn't be fetched.
    const all = failed.concat(result.failed.map((f) => ({ teamName: f.teamName, error: f.error })));
    await setState({
      status: "done",
      message: uploadResponse.status === 200
        ? `Saved ${result.fixtures} fixtures for ${result.teams} teams.`
        : "Fixture Manager couldn't use any of the pages, so nothing was saved.",
      failed: all,
      unknownCodes: result.unknownTypeCodes || [],
      result,
    });
  } catch (e) {
    await setState({ status: "error", message: e.message, current: "" });
  }
}

// ---------- messages from the popup ----------

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  (async () => {
    try {
      if (message.type === "signIn") {
        await signIn(message.server, message.email, message.password);
        sendResponse({ ok: true });
      } else if (message.type === "signOut") {
        await chrome.storage.local.remove("auth");
        sendResponse({ ok: true });
      } else if (message.type === "plan") {
        sendResponse({ ok: true, plan: await getPlan() });
      } else if (message.type === "send") {
        const { run: current } = await chrome.storage.session.get("run");
        if (current && current.status === "running") return sendResponse({ ok: false, error: "Already running." });
        run(message.leagues);   // not awaited: the popup follows progress through storage
        sendResponse({ ok: true });
      }
    } catch (e) {
      sendResponse({ ok: false, error: e.message });
    }
  })();
  return true;   // answering asynchronously
});
