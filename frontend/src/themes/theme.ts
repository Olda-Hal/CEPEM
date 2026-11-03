export type Theme = 'light' | 'dark';

const THEME_STORAGE_KEY = 'cepem-theme';
const THEME_EVENT = 'cepem-theme-change';

const isTheme = (value: string | null): value is Theme => value === 'light' || value === 'dark';

/** Stored preference, else the operating-system preference. */
export const getInitialTheme = (): Theme => {
  try {
    const stored = localStorage.getItem(THEME_STORAGE_KEY);
    if (isTheme(stored)) return stored;
  } catch {
    // Private-mode storage can throw; fall through to the system preference.
  }

  return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
};

export const getCurrentTheme = (): Theme =>
  document.documentElement.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';

export const applyTheme = (theme: Theme): void => {
  document.documentElement.setAttribute('data-theme', theme);
  document.documentElement.style.colorScheme = theme;
};

export const setTheme = (theme: Theme): void => {
  applyTheme(theme);
  try {
    localStorage.setItem(THEME_STORAGE_KEY, theme);
  } catch {
    // Persistence is best-effort; the in-page theme still switches.
  }
  window.dispatchEvent(new CustomEvent<Theme>(THEME_EVENT, { detail: theme }));
};

export const toggleTheme = (): void => {
  setTheme(getCurrentTheme() === 'dark' ? 'light' : 'dark');
};

export const initializeTheme = (): void => {
  applyTheme(getInitialTheme());
};

export const subscribeToTheme = (listener: (theme: Theme) => void): (() => void) => {
  const handler = (event: Event) => listener((event as CustomEvent<Theme>).detail);
  window.addEventListener(THEME_EVENT, handler);
  return () => window.removeEventListener(THEME_EVENT, handler);
};
