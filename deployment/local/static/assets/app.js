(function () {
  const fallbackConfig = {
    updates: {
      installerUrl: "/updates/windows/stable/NSO.Edemly-win-Setup.exe",
      latestVersion: "not set",
      minimumRequiredVersion: "not set",
      mandatory: false
    }
  };
  const pageSize = 5;
  const staticRootUrl = resolveStaticRootUrl();
  const state = {
    catalog: null,
    config: fallbackConfig,
    platformId: "windows"
  };

  document.addEventListener("DOMContentLoaded", () => {
    loadPageData();
  });

  window.addEventListener("popstate", () => {
    if (state.catalog) {
      renderReleasePage(state.catalog, state.config);
    }
  });

  async function loadPageData() {
    const [configResult, catalogResult] = await Promise.allSettled([
      loadJson("client.json"),
      loadJson("releases.json")
    ]);

    state.config = configResult.status === "fulfilled" ? configResult.value : fallbackConfig;
    renderConfig(state.config);

    if (configResult.status === "rejected") {
      renderConfigError(configResult.reason);
    }

    if (catalogResult.status === "fulfilled") {
      state.catalog = catalogResult.value;
      state.platformId = resolveDefaultPlatform(state.catalog);
      renderCatalog(state.catalog, state.config);
    } else {
      renderCatalogError(catalogResult.reason);
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
  }

  function renderConfigError(error) {
    document.querySelectorAll("[data-config-status]").forEach((element) => {
      element.textContent = `Could not load client.json: ${error.message}`;
    });
  }

  function renderCatalog(catalog, config) {
    renderSharedCatalogData(catalog);
    renderHomePage(catalog);
    renderDownloadPage(catalog, config);
    renderReleasePage(catalog, config);
    renderSupportPage(catalog);
  }

  function renderSharedCatalogData(catalog) {
    const support = catalog.support || {};

    document.querySelectorAll("[data-feedback-href]").forEach((element) => {
      if (support.feedbackUrl) {
        element.setAttribute("href", support.feedbackUrl);
      }
    });

    document.querySelectorAll("[data-support-summary]").forEach((element) => {
      element.textContent = support.summary || "Send feedback, report a bug, or share an idea.";
    });
  }

  function renderHomePage(catalog) {
    const product = catalog.product || {};

    document.querySelectorAll("[data-product-hero-image]").forEach((image) => {
      if (product.heroImage) {
        image.setAttribute("src", resolveSiteHref(product.heroImage));
      }
    });

    document.querySelectorAll("[data-platform-overview]").forEach((container) => {
      clear(container);
      getPlatforms(catalog).forEach((platform) => {
        const item = document.createElement("article");
        item.className = "platform-card";
        item.appendChild(createTag(platform.status));
        item.appendChild(createHeading("h3", platform.name));
        item.appendChild(createParagraph(platform.summary || "Platform details are coming later."));
        container.appendChild(item);
      });
    });
  }

  function renderDownloadPage(catalog, config) {
    const platform = getSelectedPlatform(catalog);
    renderPlatformTabs(catalog);
    renderPlatformNote(platform);
    renderAvailableDownloads(catalog, config, platform.id);
    renderRequirements(catalog, platform.id, "[data-download-requirements]");
  }

  function renderPlatformTabs(catalog) {
    document.querySelectorAll("[data-platform-tabs]").forEach((container) => {
      clear(container);
      getPlatforms(catalog).forEach((platform) => {
        const button = document.createElement("button");
        button.type = "button";
        button.className = platform.id === state.platformId ? "platform-tab active" : "platform-tab";
        button.textContent = platform.name;
        button.addEventListener("click", () => {
          state.platformId = platform.id;
          renderDownloadPage(state.catalog, state.config);
        });
        container.appendChild(button);
      });
    });
  }

  function renderPlatformNote(platform) {
    document.querySelectorAll("[data-platform-note]").forEach((element) => {
      element.textContent = platform.summary || "";
    });
  }

  function renderAvailableDownloads(catalog, config, platformId) {
    const releases = getSortedReleases(catalog);
    const downloadable = releases.filter((release) => canDownloadForPlatform(release, platformId));

    document.querySelectorAll("[data-download-list]").forEach((container) => {
      clear(container);

      if (!downloadable.length) {
        const empty = document.createElement("div");
        empty.className = "empty-state";
        empty.appendChild(createHeading("h2", "No downloads yet"));
        empty.appendChild(createParagraph("This platform is prepared in the catalog, but no package is published yet."));
        container.appendChild(empty);
        return;
      }

      downloadable.forEach((release, index) => {
        container.appendChild(createDownloadCard(release, platformId, index === 0));
      });
    });

    const latest = downloadable[0] || findLatestRelease(catalog, releases);
    renderLatestDownloadHref(latest, platformId, config);
  }

  function createDownloadCard(release, platformId, primary) {
    const article = document.createElement("article");
    article.className = primary ? "download-card-row primary-download" : "download-card-row";

    const text = document.createElement("div");
    text.appendChild(createTag(primary ? "Latest available" : "Available"));
    text.appendChild(createHeading("h2", release.title || `Edemly ${release.version}`));
    text.appendChild(createParagraph(release.summary || "No summary provided."));
    text.appendChild(createMetaLine([release.date, release.channel, platformId]));

    const actions = createDownloadActions(release, platformId);
    article.appendChild(text);
    article.appendChild(actions);
    return article;
  }

  function renderReleasePage(catalog, config) {
    const releases = getSortedReleases(catalog);
    const selected = resolveSelectedRelease(catalog, releases);
    const page = resolveReleasePage(releases, selected);

    renderReleaseStats(catalog, releases);
    renderReleaseList(catalog, releases, selected, page);
    renderReleasePagination(releases, selected, page);
    renderReleaseDetail(catalog, config, selected);
  }

  function renderReleaseStats(catalog, releases) {
    document.querySelectorAll("[data-release-stats]").forEach((container) => {
      clear(container);
      const downloadable = releases.filter((release) => hasAnyDownload(release)).length;
      const archived = releases.filter((release) => !isInDownloadWindow(catalog, release)).length;
      const items = [
        ["Latest", catalog.latestVersion || "not set"],
        ["History", `${releases.length} releases`],
        ["Downloads", `${downloadable} available`],
        ["Archive", `${archived} archived`]
      ];

      items.forEach(([label, value]) => {
        const item = document.createElement("div");
        item.appendChild(createTag(label));
        item.appendChild(createHeading("strong", value));
        container.appendChild(item);
      });
    });
  }

  function renderReleaseList(catalog, releases, selected, page) {
    document.querySelectorAll("[data-release-list]").forEach((container) => {
      clear(container);
      const start = (page - 1) * pageSize;
      const visible = releases.slice(start, start + pageSize);

      visible.forEach((release) => {
        const link = document.createElement("a");
        link.className = release.version === selected.version ? "release-card active" : "release-card";
        link.href = buildReleaseUrl(release.version, page);
        link.addEventListener("click", (event) => {
          event.preventDefault();
          history.pushState(null, "", link.href);
          renderReleasePage(catalog, state.config);
        });

        link.appendChild(createTag(resolveReleaseStateLabel(catalog, release)));
        link.appendChild(createHeading("h3", release.title || `Edemly ${release.version}`));
        link.appendChild(createMetaLine([release.date, release.channel]));
        link.appendChild(createParagraph(release.summary || "No summary provided."));
        container.appendChild(link);
      });
    });
  }

  function renderReleasePagination(releases, selected, page) {
    document.querySelectorAll("[data-release-pagination]").forEach((container) => {
      clear(container);
      const pageCount = Math.max(1, Math.ceil(releases.length / pageSize));

      for (let index = 1; index <= pageCount; index++) {
        const link = document.createElement("a");
        link.className = index === page ? "page-link active" : "page-link";
        link.href = buildReleaseUrl(selected.version, index);
        link.textContent = String(index);
        link.addEventListener("click", (event) => {
          event.preventDefault();
          history.pushState(null, "", link.href);
          renderReleasePage(state.catalog, state.config);
        });
        container.appendChild(link);
      }
    });
  }

  function renderReleaseDetail(catalog, config, release) {
    document.querySelectorAll("[data-release-detail]").forEach((container) => {
      clear(container);

      if (!release) {
        container.appendChild(createMutedText("No release selected."));
        return;
      }

      const hero = document.createElement("div");
      hero.className = "release-detail-hero";
      const media = getPrimaryMedia(release);
      if (media) {
        const image = document.createElement("img");
        image.src = resolveSiteHref(media.url);
        image.alt = media.alt || release.title || release.version;
        hero.appendChild(image);
      }

      const text = document.createElement("div");
      text.appendChild(createTag(resolveReleaseStateLabel(catalog, release)));
      text.appendChild(createHeading("h2", release.title || `Edemly ${release.version}`));
      text.appendChild(createMetaLine([release.date, release.channel]));
      text.appendChild(createParagraph(release.description || release.summary || "No details provided."));
      text.appendChild(createDownloadActions(release, state.platformId));
      hero.appendChild(text);
      container.appendChild(hero);

      container.appendChild(createDetailColumns(catalog, release));
      container.appendChild(createMediaStrip(release));
      renderRequirements(catalog, state.platformId, "[data-release-requirements]");
      renderLatestDownloadHref(release, state.platformId, config);
    });
  }

  function createDetailColumns(catalog, release) {
    const wrapper = document.createElement("div");
    wrapper.className = "detail-columns";
    wrapper.appendChild(createDetailPanel("Highlights", release.highlights));
    wrapper.appendChild(createDetailPanel("Changes", release.changes));
    wrapper.appendChild(createDetailPanel("Known notes", release.knownIssues));

    const platforms = Object.entries(release.platforms || {});
    const platformPanel = document.createElement("article");
    platformPanel.className = "detail-panel";
    platformPanel.appendChild(createHeading("h3", "Platforms"));
    const list = document.createElement("div");
    list.className = "platform-badges";
    platforms.forEach(([id, status]) => {
      const platform = getPlatforms(catalog).find((item) => item.id === id);
      const badge = document.createElement("span");
      badge.textContent = `${platform ? platform.name : id}: ${status}`;
      list.appendChild(badge);
    });
    platformPanel.appendChild(list);
    wrapper.appendChild(platformPanel);
    return wrapper;
  }

  function createDetailPanel(title, items) {
    const panel = document.createElement("article");
    panel.className = "detail-panel";
    panel.appendChild(createHeading("h3", title));
    panel.appendChild(createList(items && items.length ? items : ["No notes yet."]));
    return panel;
  }

  function createMediaStrip(release) {
    const strip = document.createElement("div");
    strip.className = "media-strip";
    const media = release.media || [];

    if (!media.length) {
      return strip;
    }

    media.forEach((item) => {
      if (item.type !== "image") {
        return;
      }

      const image = document.createElement("img");
      image.src = resolveSiteHref(item.url);
      image.alt = item.alt || "Release media";
      strip.appendChild(image);
    });

    return strip;
  }

  function renderSupportPage(catalog) {
    document.querySelectorAll("[data-support-options]").forEach((container) => {
      clear(container);
      const support = catalog.support || {};
      const options = [
        ["Feedback form", support.summary || "Share bugs, ideas, and product feedback.", support.feedbackUrl],
        ["Release notes", "Browse the release history and check what changed.", "../release/"],
        ["Downloads", "Install the latest supported Windows build.", "../download/"]
      ];

      options.forEach(([title, summary, href]) => {
        const card = document.createElement("a");
        card.className = "support-card";
        card.href = href || "#";
        if (href && href.startsWith("http")) {
          card.target = "_blank";
          card.rel = "noreferrer";
        }
        card.appendChild(createHeading("h3", title));
        card.appendChild(createParagraph(summary));
        container.appendChild(card);
      });
    });
  }

  function renderRequirements(catalog, platformId, selector) {
    document.querySelectorAll(selector).forEach((container) => {
      clear(container);
      const requirements = (catalog.systemRequirements || {})[platformId] || [];

      if (!requirements.length) {
        container.appendChild(createMutedText("System requirements are not published for this platform yet."));
        return;
      }

      requirements.forEach((item) => {
        const row = document.createElement("li");
        row.textContent = item;
        container.appendChild(row);
      });
    });
  }

  function renderLatestDownloadHref(release, platformId, config) {
    const fallbackHref = config.updates && config.updates.installerUrl
      ? config.updates.installerUrl
      : fallbackConfig.updates.installerUrl;
    const platformDownloads = release && release.downloads ? release.downloads[platformId] : null;
    const href = platformDownloads && platformDownloads.installer
      ? platformDownloads.installer
      : fallbackHref;

    document.querySelectorAll("[data-config-href='installer']").forEach((element) => {
      element.setAttribute("href", resolveSiteHref(href));
    });
  }

  function createDownloadActions(release, platformId) {
    const actions = document.createElement("div");
    actions.className = "release-actions";
    const downloads = release && release.downloads ? release.downloads[platformId] : null;

    if (downloads && downloads.installer) {
      actions.appendChild(createButton(downloads.installer, "Installer", true));
    }

    if (downloads && downloads.portable) {
      actions.appendChild(createButton(downloads.portable, "Portable ZIP", false));
    }

    if (!actions.children.length) {
      const status = document.createElement("span");
      status.className = "release-status";
      status.textContent = release && hasAnyDownload(release) ? "Choose another platform" : "No public download";
      actions.appendChild(status);
    }

    return actions;
  }

  function canDownloadForPlatform(release, platformId) {
    const downloads = release && release.downloads ? release.downloads[platformId] : null;
    return !!downloads && (!!downloads.installer || !!downloads.portable);
  }

  function hasAnyDownload(release) {
    return Object.values(release.downloads || {}).some((downloads) => {
      return downloads && (downloads.installer || downloads.portable);
    });
  }

  function isInDownloadWindow(catalog, release) {
    const boundary = resolveDownloadBoundary(catalog);
    return !boundary || compareVersions(release.version, boundary) >= 0;
  }

  function resolveDownloadBoundary(catalog) {
    const releases = getSortedReleases(catalog);
    const latestMandatory = releases
      .filter((release) => release.mandatory)
      .sort((left, right) => compareVersions(right.version, left.version))[0];

    if (latestMandatory) {
      return latestMandatory.version;
    }

    return catalog.downloadableFromVersion || catalog.minimumRequiredVersion || "";
  }

  function resolveReleaseStateLabel(catalog, release) {
    if (hasAnyDownload(release)) {
      return "Download available";
    }

    if (!isInDownloadWindow(catalog, release)) {
      return "Archived";
    }

    return release.mandatory ? "Required baseline" : "History";
  }

  function getPlatforms(catalog) {
    return catalog.platforms || [];
  }

  function getSelectedPlatform(catalog) {
    return getPlatforms(catalog).find((platform) => platform.id === state.platformId) ||
      getPlatforms(catalog)[0] ||
      { id: "windows", name: "Windows", summary: "" };
  }

  function resolveDefaultPlatform(catalog) {
    const query = new URLSearchParams(window.location.search);
    const requested = query.get("platform");
    const platforms = getPlatforms(catalog);
    if (platforms.some((platform) => platform.id === requested)) {
      return requested;
    }

    const defaultPlatform = platforms.find((platform) => platform.default) || platforms[0];
    return defaultPlatform ? defaultPlatform.id : "windows";
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

  function resolveSelectedRelease(catalog, releases) {
    const query = new URLSearchParams(window.location.search);
    const version = query.get("version") || catalog.latestVersion;
    return releases.find((release) => release.version === version) || findLatestRelease(catalog, releases);
  }

  function resolveReleasePage(releases, selected) {
    const query = new URLSearchParams(window.location.search);
    const requestedPage = Number.parseInt(query.get("page"), 10);
    if (!Number.isNaN(requestedPage) && requestedPage > 0) {
      return clamp(requestedPage, 1, Math.max(1, Math.ceil(releases.length / pageSize)));
    }

    const selectedIndex = releases.findIndex((release) => release.version === selected.version);
    return Math.floor(Math.max(0, selectedIndex) / pageSize) + 1;
  }

  function buildReleaseUrl(version, page) {
    const url = new URL(window.location.href);
    url.searchParams.set("version", version);
    url.searchParams.set("page", String(page));
    return url.href;
  }

  function getPrimaryMedia(release) {
    return (release.media || []).find((item) => item.type === "image") || null;
  }

  function createButton(href, text, primary) {
    const link = document.createElement("a");
    link.className = primary ? "button primary" : "button";
    link.href = resolveSiteHref(href);
    link.textContent = text;
    return link;
  }

  function createTag(text) {
    const tag = document.createElement("span");
    tag.className = "tag";
    tag.textContent = text || "";
    return tag;
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

  function createMetaLine(parts) {
    const meta = document.createElement("p");
    meta.className = "release-meta";
    meta.textContent = parts.filter(Boolean).join(" / ");
    return meta;
  }

  function createList(items) {
    const list = document.createElement("ul");
    list.className = "release-notes";
    items.forEach((item) => {
      const row = document.createElement("li");
      row.textContent = item;
      list.appendChild(row);
    });
    return list;
  }

  function renderCatalogError(error) {
    document.querySelectorAll("[data-download-list], [data-release-list], [data-release-detail]").forEach((container) => {
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

  function clamp(value, min, max) {
    return Math.min(Math.max(value, min), max);
  }
})();
