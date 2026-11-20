# Fixture Manager – FullTime (browser extension)

Fetches the club's FullTime team pages **in your own browser, when you click Send**, and sends them to Fixture
Manager, which compares them with its fixtures (Review FullTime and Gameweek pages). It never runs on its own.

## Install (Chrome or Edge)

1. Download **FixtureManagerExtension.zip**:
   https://github.com/euanmac/FixtureManagementV3/releases/download/extension/FixtureManagerExtension.zip
2. Unzip it somewhere you'll keep it (e.g. Documents). **Don't delete the folder afterwards** – the browser runs the
   extension from it.
3. Open **`chrome://extensions`** (Edge: **`edge://extensions`**) and turn on **Developer mode**
   (top right in Chrome; left-hand panel in Edge).
4. Click **Load unpacked** and choose the unzipped **FixtureManagerExtension** folder.
5. Pin it: the puzzle-piece icon in the toolbar → pin **Fixture Manager – FullTime**.

The browser may now and then remind you that a developer-mode extension is on – that's expected.
Work or school computers can block developer-mode extensions; use a personal one if so.

## Use

1. Click the extension's button. Choose the Fixture Manager site (normally **Live**) and sign in with your
   Fixture Manager account. The password isn't stored.
2. Tick the leagues to fetch (your choice is remembered).
3. Open any page on **fulltime.thefa.com** in a tab.
4. Click **Send fixtures from FullTime**. It fetches each team's FullTime page from that tab, a second or so apart,
   then sends them. The popup can be closed; reopen it to see progress.
5. In Fixture Manager, open **Review FullTime** (or Gameweek) to see and act on the differences.

If FullTime refuses a page it stops asking and sends what it already has. Fetching some leagues only replaces those
leagues' data; the others are kept.

## Updating

Download the new zip, replace the contents of your FixtureManagerExtension folder with it, then click the reload ↻
button on the extension in `chrome://extensions` (or `edge://extensions`).

## For developers

- Plain JavaScript, no build step: `manifest.json` (Manifest V3), `background.js` (sign-in, fetching, sending),
  `popup.html/js/css`, `icons/`.
- The sites it may talk to are listed in `manifest.json` → `host_permissions`; the popup refuses others.
- Website endpoints: `GET /api/fulltime/plan` (teams to fetch), `POST /api/fulltime/upload` (the pages).
- Packaging: the "Package browser extension" GitHub workflow (`.github/workflows/extension.yml`). Bump `version` in
  `manifest.json` for each release.
- Problems: `chrome://extensions` → this extension → **service worker** → Console.
