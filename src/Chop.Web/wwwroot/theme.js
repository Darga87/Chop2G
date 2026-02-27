(() => {
  const storageKey = "chop.ui.theme";
  const allowed = new Set(["theme-a", "theme-b", "theme-c"]);
  const fallback = "theme-b";

  const applyTheme = (themeName) => {
    const theme = allowed.has(themeName) ? themeName : fallback;
    const body = document.body;
    if (!body) {
      return;
    }

    body.classList.remove("theme-a", "theme-b", "theme-c");
    body.classList.add(theme);
  };

  const readTheme = () => {
    try {
      const raw = localStorage.getItem(storageKey);
      return allowed.has(raw ?? "") ? raw : fallback;
    } catch {
      return fallback;
    }
  };

  const writeTheme = (themeName) => {
    if (!allowed.has(themeName)) {
      return;
    }

    try {
      localStorage.setItem(storageKey, themeName);
    } catch {
      // No-op in restricted contexts.
    }
  };

  const init = () => applyTheme(readTheme());
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }

  window.chopTheme = {
    get: () => readTheme(),
    set: (themeName) => {
      writeTheme(themeName);
      applyTheme(themeName);
    },
    available: ["theme-a", "theme-b", "theme-c"],
  };
})();
