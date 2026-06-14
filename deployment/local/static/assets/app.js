(function () {
  const fallbackConfig = {
    updates: {
      installerUrl: "/updates/windows/stable/NSO.Edemly-win-Setup.exe",
      latestVersion: "not set",
      minimumRequiredVersion: "not set",
      mandatory: false
    }
  };
  const staticRootUrl = resolveStaticRootUrl();

  document.addEventListener("DOMContentLoaded", () => {
    loadPageData();
  });

  async function loadPageData() {
    const [configResult, releasesResult] = await Promise.allSettled([
      loadJson("client.json"),
      loadJson("releases.json")
    ]);

    const config = configResult.status === "fulfilled" ? configResult.value : fallbackConfig;
    renderConfig(config);

    if (configResult.status === "rejected") {
      renderConfigError(configResult.reason);
    }

    if (releasesResult.status === "fulfilled") {
      renderReleases(releasesResult.value, config);
    } else {
      renderReleaseError(releasesResult.reason);
    }
  }

  async function loadJson(url) {
    const requestUrl = resolveSiteHref(url);
    const response = await fetch(requestUrl, { cache: "no-store" });
    if (!response.ok) {
      throw new Error(`${requestUrl} returned ${response.status}`);
    }

    return response.json();
  }

  function renderConfig(config) {
    const updates = config.updates || {};
    const values = {
      latestVersion: updates.latestVersion || "not set",
      minimumRequiredVersion: updates.minimumRequiredVersion || "not set",
      mandatory: updates.mandatory ? "yes" : "no",
      installerUrl: resolveSiteHref(updates.installerUrl || fallbackConfig.updates.installerUrl)
    };

    document.querySelectorAll("[data-config]").forEach((element) => {
      const key = element.getAttribute("data-config");
      element.textContent = values[key] || "not set";
    });

    document.querySelectorAll("[data-config-href='installer']").forEach((element) => {
      element.setAttribute("href", values.installerUrl);
    });

    document.querySelectorAll("[data-update-mode]").forEach((element) => {
      element.textContent = updates.mandatory ? "Mandatory" : "Optional";
    });
  }

  function renderConfigError(error) {
    document.querySelectorAll("[data-config-status]").forEach((element) => {
      element.textContent = `Could not load client.json: ${error.message}`;
    });
  }

  function renderReleases(catalog, config) {
    const releases = getSortedReleases(catalog);
    const latest = findLatestRelease(catalog, releases);
    const downloadBoundary = resolveDownloadBoundary(catalog, config, releases);

    renderLatestRelease(latest, downloadBoundary);
    renderDownloadReleaseList(releases, latest, downloadBoundary);
    renderReleaseHistory(releases, downloadBoundary);
    renderReleaseSummary(catalog, config, downloadBoundary, latest);
    renderLatestDownloadHref(latest, downloadBoundary, config);
  }

  function getSortedReleases(catalog) {
    return (catalog.releases || [])
      .slice()
      .sort((left, right) => compareVersions(right.version, left.version));
  }

  function findLatestRelease(catalog, releases) {
    if (!releases.length) {
      return null;
    }

    return releases.find((release) => release.version === catalog.latestVersion) || releases[0];
  }

  function resolveDownloadBoundary(catalog, config, releases) {
    const latestMandatory = releases
      .filter((release) => release.mandatory)
      .sort((left, right) => compareVersions(right.version, left.version))[0];

    if (latestMandatory) {
      return latestMandatory.version;
    }

    return catalog.downloadableFromVersion ||
      catalog.minimumRequiredVersion ||
      (config.updates && config.updates.minimumRequiredVersion) ||
      "";
  }

  function renderLatestRelease(release, downloadBoundary) {
    document.querySelectorAll("[data-release-latest]").forEach((container) => {
      clear(container);

      if (!release) {
        container.appendChild(createMutedText("No release data is available."));
        return;
      }

      const copy = document.createElement("div");
      copy.className = "release-copy";
      copy.appendChild(createKicker("Latest release"));
      copy.appendChild(createHeading("h2", release.title || `Edemly ${release.version}`));
      copy.appendChild(createParagraph(release.summary || "No summary provided."));
      copy.appendChild(createChangeList(release.changes));

      const actions = createReleaseActions(release, downloadBoundary, true);

      container.appendChild(copy);
      container.appendChild(actions);
    });
  }

  function renderDownloadReleaseList(releases, latest, downloadBoundary) {
    document.querySelectorAll("[data-download-release-list]").forEach((container) => {
      clear(container);

      const previous = releases.filter((release) => !latest || release.version !== latest.version);
      if (!previous.length) {
        container.appendChild(createMutedText("No previous releases yet."));
        return;
      }

      previous.forEach((release) => {
        container.appendChild(createReleaseRow(release, downloadBoundary, false));
      });
    });
  }

  function renderReleaseHistory(releases, downloadBoundary) {
    document.querySelectorAll("[data-release-history]").forEach((container) => {
      clear(container);

      if (!releases.length) {
        container.appendChild(createMutedText("No release data is available."));
        return;
      }

      releases.forEach((release) => {
        container.appendChild(createReleaseRow(release, downloadBoundary, true));
      });
    });
  }

  function renderReleaseSummary(catalog, config, downloadBoundary, latest) {
    document.querySelectorAll("[data-release-summary]").forEach((container) => {
      clear(container);

      const updates = config.updates || {};
      const items = [
        ["Latest", latest ? latest.version : catalog.latestVersion || "not set"],
        ["Download window", downloadBoundary ? `${downloadBoundary}+` : "not set"],
        ["Minimum required", updates.minimumRequiredVersion || catalog.minimumRequiredVersion || "not set"],
        ["Update mode", updates.mandatory ? "Mandatory" : "Version based"]
      ];

      items.forEach(([label, value]) => {
        const item = document.createElement("div");
        const strong = document.createElement("strong");
        const span = document.createElement("span");
        strong.textContent = label;
        span.textContent = value;
        item.appendChild(strong);
        item.appendChild(span);
        container.appendChild(item);
      });
    });
  }

  function renderLatestDownloadHref(release, downloadBoundary, config) {
    const fallbackHref = config.updates && config.updates.installerUrl
      ? config.updates.installerUrl
      : fallbackConfig.updates.installerUrl;
    const href = canDownload(release, downloadBoundary)
      ? release.downloads.windowsInstaller || fallbackHref
      : fallbackHref;

    document.querySelectorAll("[data-config-href='installer']").forEach((element) => {
      element.setAttribute("href", resolveSiteHref(href));
    });
  }

  function createReleaseRow(release, downloadBoundary, includeChanges) {
    const row = document.createElement("article");
    row.className = "release-row";

    const details = document.createElement("div");
    const title = document.createElement("strong");
    title.textContent = release.title || `Edemly ${release.version}`;
    details.appendChild(title);

    const meta = document.createElement("p");
    meta.className = "release-meta";
    meta.textContent = formatReleaseMeta(release, downloadBoundary);
    details.appendChild(meta);

    details.appendChild(createParagraph(release.summary || "No summary provided."));

    if (includeChanges) {
      details.appendChild(createChangeList(release.changes));
    }

    row.appendChild(details);
    row.appendChild(createReleaseActions(release, downloadBoundary, false));
    return row;
  }

  function createReleaseActions(release, downloadBoundary, primaryInstaller) {
    const actions = document.createElement("div");
    actions.className = "release-actions";

    if (canDownload(release, downloadBoundary)) {
      const downloads = release.downloads || {};

      if (downloads.windowsInstaller) {
        actions.appendChild(createButton(
          downloads.windowsInstaller,
          primaryInstaller ? `Download ${release.version}` : "Installer",
          primaryInstaller));
      }

      if (downloads.windowsPortable) {
        actions.appendChild(createButton(downloads.windowsPortable, "Portable ZIP", false));
      }

      return actions;
    }

    const status = document.createElement("span");
    status.className = "release-status";
    status.textContent = isInDownloadWindow(release, downloadBoundary) ? "Files pending" : "Archived";
    actions.appendChild(status);
    return actions;
  }

  function canDownload(release, downloadBoundary) {
    const downloads = release.downloads || null;
    return isInDownloadWindow(release, downloadBoundary) &&
      !!downloads &&
      (!!downloads.windowsInstaller || !!downloads.windowsPortable);
  }

  function isInDownloadWindow(release, downloadBoundary) {
    return !downloadBoundary || compareVersions(release.version, downloadBoundary) >= 0;
  }

  function formatReleaseMeta(release, downloadBoundary) {
    const parts = [];
    if (release.date) {
      parts.push(release.date);
    }

    if (release.channel) {
      parts.push(release.channel);
    }

    parts.push(release.mandatory ? "mandatory boundary" : "standard");
    parts.push(isInDownloadWindow(release, downloadBoundary) ? "supported" : "archived");
    return parts.join(" / ");
  }

  function createButton(href, text, primary) {
    const link = document.createElement("a");
    link.className = primary ? "button primary" : "button";
    link.href = resolveSiteHref(href);
    link.textContent = text;
    return link;
  }

  function createKicker(text) {
    const kicker = document.createElement("p");
    kicker.className = "eyebrow";
    kicker.textContent = text;
    return kicker;
  }

  function createHeading(tagName, text) {
    const heading = document.createElement(tagName);
    heading.textContent = text;
    return heading;
  }

  function createParagraph(text) {
    const paragraph = document.createElement("p");
    paragraph.textContent = text;
    return paragraph;
  }

  function createMutedText(text) {
    const paragraph = document.createElement("p");
    paragraph.className = "muted";
    paragraph.textContent = text;
    return paragraph;
  }

  function createChangeList(changes) {
    const list = document.createElement("ul");
    list.className = "release-notes";

    (changes || []).forEach((change) => {
      const item = document.createElement("li");
      item.textContent = change;
      list.appendChild(item);
    });

    return list;
  }

  function renderReleaseError(error) {
    document.querySelectorAll("[data-release-latest], [data-download-release-list], [data-release-history]").forEach((container) => {
      clear(container);
      container.appendChild(createMutedText(`Could not load releases.json: ${error.message}`));
    });
  }

  function clear(element) {
    while (element.firstChild) {
      element.removeChild(element.firstChild);
    }
  }

  function resolveStaticRootUrl() {
    const script = document.currentScript;
    if (script && script.src) {
      return new URL("../", script.src);
    }

    return new URL("./", window.location.href);
  }

  function resolveSiteHref(value) {
    if (!value) {
      return "";
    }

    if (/^[a-z][a-z0-9+.-]*:/i.test(value)) {
      return value;
    }

    const normalized = value.startsWith("/") ? value.replace(/^\/+/, "") : value;
    return new URL(normalized, staticRootUrl).href;
  }

  function compareVersions(left, right) {
    const leftParts = parseVersionParts(left);
    const rightParts = parseVersionParts(right);
    const length = Math.max(leftParts.length, rightParts.length);

    for (let index = 0; index < length; index++) {
      const leftValue = leftParts[index] || 0;
      const rightValue = rightParts[index] || 0;

      if (leftValue !== rightValue) {
        return leftValue > rightValue ? 1 : -1;
      }
    }

    return 0;
  }

  function parseVersionParts(version) {
    return String(version || "")
      .split(/[+-]/)[0]
      .split(".")
      .map((part) => Number.parseInt(part, 10))
      .map((part) => Number.isNaN(part) ? 0 : part);
  }
})();
