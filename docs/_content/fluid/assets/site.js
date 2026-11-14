// Fluid documentation (derived from the Douze documentation site, MIT): theme toggle, mobile navigation, tabs, copy buttons, outline tracking and search.

const root = document.documentElement;

// Theme

function currentTheme() {
  return root.dataset.theme || (matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");
}

document.querySelector("[data-theme-toggle]")?.addEventListener("click", () => {
  const next = currentTheme() === "dark" ? "light" : "dark";
  root.dataset.theme = next;
  try { localStorage.setItem("fluid-theme", next); } catch {}
});

// Mobile navigation

const menu = document.querySelector("[data-menu]");
menu?.addEventListener("click", () => {
  const open = document.body.classList.toggle("nav-open");
  menu.setAttribute("aria-expanded", String(open));
});
document.addEventListener("click", (event) => {
  if (document.body.classList.contains("nav-open") && !event.target.closest(".sidebar, [data-menu]")) {
    document.body.classList.remove("nav-open");
    menu?.setAttribute("aria-expanded", "false");
  }
});
document.querySelector(".sidebar [aria-current='page']")?.scrollIntoView({ block: "center" });

// Tabs: the shortcode writes one section per tab, this turns them into a tab list. The choice is remembered by label.

let tabsId = 0;
for (const tabs of document.querySelectorAll("[data-tabs]")) {
  const panels = [...tabs.querySelectorAll(":scope > .tab")];
  if (panels.length === 0) continue;
  const list = document.createElement("div");
  list.className = "tabs__list";
  list.setAttribute("role", "tablist");
  const id = ++tabsId;
  const buttons = panels.map((panel, i) => {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "tabs__button";
    button.textContent = panel.dataset.tabLabel;
    button.id = `tab-${id}-${i}`;
    button.setAttribute("role", "tab");
    panel.id = `tabpanel-${id}-${i}`;
    panel.setAttribute("role", "tabpanel");
    panel.setAttribute("aria-labelledby", button.id);
    button.setAttribute("aria-controls", panel.id);
    button.addEventListener("click", () => selectLabel(panel.dataset.tabLabel, true));
    button.addEventListener("keydown", (event) => {
      const step = event.key === "ArrowRight" ? 1 : event.key === "ArrowLeft" ? -1 : 0;
      if (!step) return;
      const next = buttons[(i + step + buttons.length) % buttons.length];
      next.click();
      next.focus();
    });
    list.append(button);
    return button;
  });
  tabs.prepend(list);
  tabs.classList.add("is-ready");
  tabs.select = (label) => {
    const index = Math.max(0, panels.findIndex((p) => p.dataset.tabLabel === label));
    panels.forEach((panel, i) => {
      panel.hidden = i !== index;
      buttons[i].setAttribute("aria-selected", String(i === index));
      buttons[i].tabIndex = i === index ? 0 : -1;
    });
  };
  let saved = null;
  try { saved = localStorage.getItem("fluid-tab"); } catch {}
  tabs.select(saved);
}

function selectLabel(label, remember) {
  for (const tabs of document.querySelectorAll("[data-tabs].is-ready")) {
    if ([...tabs.querySelectorAll(":scope > .tab")].some((p) => p.dataset.tabLabel === label)) tabs.select(label);
  }
  if (remember) {
    try { localStorage.setItem("fluid-tab", label); } catch {}
  }
}

// Copy buttons: the highlight step's buttons carry the code, the home page's copy their sibling command.

async function copy(button, text) {
  try {
    await navigator.clipboard.writeText(text);
    const label = button.textContent;
    button.textContent = "Copied";
    button.classList.add("is-copied");
    setTimeout(() => { button.textContent = label; button.classList.remove("is-copied"); }, 1600);
  } catch {}
}

document.addEventListener("click", (event) => {
  const codeButton = event.target.closest(".code-copy");
  if (codeButton) copy(codeButton, codeButton.dataset.code ?? "");
  const copyButton = event.target.closest("[data-copy]");
  if (copyButton) copy(copyButton, copyButton.closest("[data-copy-root]")?.querySelector("code")?.textContent ?? "");
});

// Outline: highlight the section being read.

const outlineLinks = [...document.querySelectorAll(".outline a")];
if (outlineLinks.length) {
  const targets = outlineLinks.map((a) => document.getElementById(decodeURIComponent(a.hash.slice(1)))).filter(Boolean);
  const update = () => {
    let active = targets[0];
    for (const t of targets) if (t.getBoundingClientRect().top < 120) active = t;
    outlineLinks.forEach((a) => a.classList.toggle("is-active", active && a.hash === `#${active.id}`));
  };
  addEventListener("scroll", update, { passive: true });
  update();
}

// Search: Pagefind's UI, loaded on first use.

const dialog = document.querySelector("[data-search-dialog]");
let searchLoaded = false;

async function openSearch() {
  if (!dialog) return;
  if (!searchLoaded) {
    searchLoaded = true;
    const base = document.querySelector("[data-pagefind-ui]").dataset.base;
    const css = document.createElement("link");
    css.rel = "stylesheet";
    css.href = `${base}pagefind/pagefind-ui.css`;
    document.head.append(css);
    await new Promise((resolve, reject) => {
      const script = document.createElement("script");
      script.src = `${base}pagefind/pagefind-ui.js`;
      script.onload = resolve;
      script.onerror = reject;
      document.head.append(script);
    }).then(() => {
      new PagefindUI({ element: "#search", showSubResults: true, showImages: false, resetStyles: false });
    }).catch(() => {
      document.querySelector("#search").textContent = "Search is available once the site is built with its search step.";
    });
  }
  dialog.showModal();
  setTimeout(() => dialog.querySelector("input")?.focus(), 30);
}

document.querySelector("[data-search-open]")?.addEventListener("click", openSearch);
dialog?.addEventListener("click", (event) => { if (event.target === dialog) dialog.close(); });
document.addEventListener("keydown", (event) => {
  const typing = event.target.closest?.("input, textarea, [contenteditable]");
  if (!typing && (event.key === "/" || (event.key === "k" && (event.metaKey || event.ctrlKey)))) {
    event.preventDefault();
    openSearch();
  }
});
