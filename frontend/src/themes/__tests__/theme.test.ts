import { beforeEach, describe, expect, test } from 'vitest';
import { applyTheme, getCurrentTheme, initializeTheme, setTheme, toggleTheme } from '../theme';

describe('light/dark theme', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('data-theme');
  });

  test('defaults to light and applies the document attribute', () => {
    initializeTheme();

    expect(getCurrentTheme()).toBe('light');
    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
  });

  test('remembers an explicit theme choice', () => {
    setTheme('dark');

    expect(getCurrentTheme()).toBe('dark');
    expect(localStorage.getItem('cepem-theme')).toBe('dark');
  });

  test('uses the stored theme on startup', () => {
    localStorage.setItem('cepem-theme', 'dark');

    initializeTheme();

    expect(getCurrentTheme()).toBe('dark');
  });

  test('toggle switches between light and dark', () => {
    applyTheme('light');
    toggleTheme();
    expect(getCurrentTheme()).toBe('dark');

    toggleTheme();
    expect(getCurrentTheme()).toBe('light');
  });
});
