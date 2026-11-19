# Fixture Manager – FullTime (browser extension)

Fetches the club's FullTime team pages **in your own browser, when you click Send**, and sends them to
Fixture Manager, which reads them and saves them as the latest FullTime upload for reconciliation.
It never runs on its own or on a schedule.

## Install (Chrome or Edge)

1. Open `chrome://extensions` (Edge: `edge://extensions`).
2. Turn on **Developer mode**.
3. **Load unpacked** → choose this `FixtureManagement.BrowserExtension` folder.
4. Pin it (puzzle-piece icon → pin) so its button is on the toolbar.

After changing the files, click the extension's reload ↻ button on `chrome://extensions`.

## Use

1. Click the extension's button, pick the Fixture Manager site, and sign in with your site account.
2. Open any page on **fulltime.thefa.com** in a tab.
3. Click **Send fixtures from FullTime**. It asks Fixture Manager for the team list (`GET /api/fulltime/plan`),
   fetches each team's FullTime page from that tab with a 1.5 s pause between pages, then sends them all
   (`POST /api/fulltime/upload`). The popup can be closed; reopen it to see progress.
4. If FullTime refuses a page (403/429) it stops asking, and sends what it already has.

## Notes

- The site must have the `/api/fulltime` endpoints deployed.
- The password is never stored; the site's refresh token is kept in the extension's local storage.
- Problems: `chrome://extensions` → this extension → **service worker** → Console.
