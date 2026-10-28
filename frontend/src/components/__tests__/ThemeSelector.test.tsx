import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from '../../i18n';
import { ThemeSelector } from '../ThemeSelector';
import { themeManager } from '../../themes/ThemeManager';

describe('ThemeSelector', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('data-theme');
  });

  test('renders light and dark theme groups from the manager', () => {
    render(
      <I18nextProvider i18n={i18n}>
        <ThemeSelector />
      </I18nextProvider>
    );

    const select = screen.getByTitle(/change theme/i);
    expect(select).toBeInTheDocument();
    expect(select.querySelectorAll('optgroup')).toHaveLength(2);
  });

  test('switching to a dark theme updates the document attribute', () => {
    render(
      <I18nextProvider i18n={i18n}>
        <ThemeSelector />
      </I18nextProvider>
    );

    const select = screen.getByTitle(/change theme/i);
    fireEvent.change(select, { target: { value: 'obsidian' } });

    expect(document.documentElement.getAttribute('data-theme')).toBe('obsidian');
    expect(select).toHaveValue('obsidian');
  });

  test('theme choice survives a manager re-read (persisted)', () => {
    render(
      <I18nextProvider i18n={i18n}>
        <ThemeSelector />
      </I18nextProvider>
    );

    fireEvent.change(screen.getByTitle(/change theme/i), { target: { value: 'obsidian' } });

    expect(themeManager.getCurrentTheme()).toBe('obsidian');
    expect(localStorage.getItem('cepem-theme')).toBeTruthy();
  });
});
