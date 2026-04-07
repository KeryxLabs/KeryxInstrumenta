const THEME_PREFIX = "theme-";
const STORAGE_KEY = "sttp.ui.theme";

function clearThemeClasses(root) {
  for (const cls of Array.from(root.classList)) {
    if (cls.startsWith(THEME_PREFIX)) {
      root.classList.remove(cls);
    }
  }
}

export function applyTheme(themeKey, persist = true) {
  const root = document.documentElement;
  clearThemeClasses(root);

  if (themeKey && themeKey !== "native") {
    root.classList.add(`${THEME_PREFIX}${themeKey}`);
  }

  if (persist) {
    localStorage.setItem(STORAGE_KEY, themeKey);
  }
}

export function restoreTheme(fallbackKey) {
  const saved = localStorage.getItem(STORAGE_KEY);
  // Backward compatibility: previous default `ember-classic` now maps to the true original mode.
  const resolved = saved === "ember-classic" ? "native" : (saved || fallbackKey);
  applyTheme(resolved, false);
  return resolved;
}
