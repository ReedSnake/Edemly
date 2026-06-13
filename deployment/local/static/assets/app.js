(function () {
  const state = {
    config: null
  };

  document.addEventListener("DOMContentLoaded", () => {
    loadConfig();
    bindCopyButtons();
  });

  async function loadConfig() {
    try {
      const response = await fetch("/client.json", { cache: "no-store" });
      if (!response.ok) {
        throw new Error(`client.json returned ${response.status}`);
      }

      state.config = await response.json();
      renderConfig(state.config);
      checkFeed(state.config);
    } catch (error) {
      renderError(error);
    }
  }

  function renderConfig(config) {
    const updates = config.updates || {};
    const values = {
      environment: config.environment || "unknown",
      schemaVersion: String(config.schemaVersion || "-"),
      latestVersion: updates.latestVersion || "not set",
      minimumRequiredVersion: updates.minimumRequiredVersion || "not set",
      mandatory: updates.mandatory ? "yes" : "no",
      installerUrl: updates.installerUrl || "/updates/windows/stable/NSO.Edemly-win-Setup.exe"
    };

    document.querySelectorAll("[data-config]").forEach((element) => {
      const key = element.getAttribute("data-config");
      element.textContent = values[key] || "not set";
    });

    document.querySelectorAll("[data-config-href='installer']").forEach((element) => {
      element.setAttribute("href", values.installerUrl);
    });

    document.querySelectorAll("[data-config-href='updateFeed']").forEach((element) => {
      const feedUrl = normalizeFeedUrl(updates.windowsStableUrl);
      element.setAttribute("href", feedUrl);
    });

    document.querySelectorAll("[data-update-mode]").forEach((element) => {
      element.textContent = updates.mandatory ? "Mandatory" : "Optional";
    });

    renderServers(config.servers || []);
  }

  function renderServers(servers) {
    document.querySelectorAll("[data-server-list]").forEach((container) => {
      container.textContent = "";

      if (!servers.length) {
        const empty = document.createElement("p");
        empty.className = "muted";
        empty.textContent = "No servers configured.";
        container.appendChild(empty);
        return;
      }

      servers.forEach((server) => {
        const card = document.createElement("article");
        card.className = `endpoint-card${server.enabled === false ? " disabled" : ""}`;

        const title = document.createElement("h3");
        title.textContent = server.name || "server";
        card.appendChild(title);

        const details = document.createElement("dl");
        addDetail(details, "api", server.apiBaseUrl);
        addDetail(details, "hub", server.hubBaseUrl);
        addDetail(details, "pay", server.paymentBaseUrl);
        addDetail(details, "prio", String(server.priority ?? "-"));
        card.appendChild(details);

        container.appendChild(card);
      });
    });
  }

  function addDetail(list, name, value) {
    const term = document.createElement("dt");
    term.textContent = name;
    const definition = document.createElement("dd");
    definition.textContent = value || "not set";
    list.appendChild(term);
    list.appendChild(definition);
  }

  async function checkFeed(config) {
    const feedUrl = normalizeFeedUrl(config.updates && config.updates.windowsStableUrl);
    const targets = document.querySelectorAll("[data-feed-status]");
    if (!targets.length) {
      return;
    }

    try {
      const response = await fetch(feedUrl, { cache: "no-store" });
      targets.forEach((element) => {
        element.textContent = response.ok ? "Published" : "Not published";
      });
    } catch {
      targets.forEach((element) => {
        element.textContent = "Not reachable";
      });
    }
  }

  function normalizeFeedUrl(baseUrl) {
    if (!baseUrl) {
      return "/updates/windows/stable/releases.win.json";
    }

    return `${baseUrl.replace(/\/$/, "")}/releases.win.json`;
  }

  function bindCopyButtons() {
    const toast = document.querySelector("[data-toast]");
    document.querySelectorAll("[data-copy]").forEach((button) => {
      button.addEventListener("click", async () => {
        const selector = button.getAttribute("data-copy");
        const target = selector ? document.querySelector(selector) : null;
        const text = target ? target.textContent.trim() : "";
        if (!text) {
          return;
        }

        try {
          await navigator.clipboard.writeText(text);
          showToast(toast);
        } catch {
          showToast(toast, "Copy failed");
        }
      });
    });
  }

  function showToast(toast, message) {
    if (!toast) {
      return;
    }

    toast.textContent = message || "Copied";
    toast.hidden = false;
    window.clearTimeout(showToast.timer);
    showToast.timer = window.setTimeout(() => {
      toast.hidden = true;
    }, 1800);
  }

  function renderError(error) {
    document.querySelectorAll("[data-config]").forEach((element) => {
      element.textContent = "unavailable";
    });

    document.querySelectorAll("[data-server-list]").forEach((container) => {
      container.innerHTML = "";
      const message = document.createElement("p");
      message.className = "muted";
      message.textContent = `Could not load client.json: ${error.message}`;
      container.appendChild(message);
    });
  }
})();
