// Fixture Manager – FullTime: the popup. Signs in, starts a send, and shows progress from chrome.storage.session.

const $ = (id) => document.getElementById(id);

function showError(text) {
  $("error").textContent = text || "";
  $("error").hidden = !text;
}

function ask(message) {
  return chrome.runtime.sendMessage(message);
}

async function renderAuth() {
  const { auth } = await chrome.storage.local.get("auth");
  $("signed-out").hidden = !!auth;
  $("signed-in").hidden = !auth;
  if (auth) {
    $("who-server").textContent = new URL(auth.server).host;
    $("who-email").textContent = auth.email;
    $("open-site").href = auth.server;
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
  try {
    const url = new URL(server);
    if (url.protocol !== "https:") throw new Error();
  } catch {
    return showError("Enter the site's address, starting https://");
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
  await renderAuth();
});

$("send").addEventListener("click", async () => {
  showError("");
  const answer = await ask({ type: "send" });
  if (!answer.ok) showError(answer.error);
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
