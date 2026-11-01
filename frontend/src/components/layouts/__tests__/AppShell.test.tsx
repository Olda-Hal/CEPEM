import { vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { I18nextProvider } from 'react-i18next';
import i18n from '../../../i18n';
import { AppShell } from '../AppShell';

const logout = vi.fn();

vi.mock('../../../contexts/AuthContext', () => ({
  useAuth: () => ({
    user: {
      id: 1,
      firstName: 'Admin',
      lastName: 'User',
      email: 'admin@example.com',
      uid: 'U-1',
      active: true,
      fullName: 'Admin User',
      roles: ['SysAdmin']
    },
    logout,
    impersonation: null,
    stopImpersonation: vi.fn()
  })
}));

describe('AppShell layout', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
  });

  test('search sits in the top row above the navigation row', () => {
    render(
      <BrowserRouter>
        <I18nextProvider i18n={i18n}>
          <AppShell />
        </I18nextProvider>
      </BrowserRouter>
    );

    const topRow = document.querySelector('.shell-header-inner');
    const navRow = document.querySelector('.shell-header-nav');
    const search = screen.getByRole('button', { name: /search/i });
    const admin = screen.getByRole('link', { name: /^Admin$/ });

    expect(topRow).not.toBeNull();
    expect(navRow).not.toBeNull();
    expect(topRow).toContainElement(search);
    expect(navRow).toContainElement(admin);
    expect(topRow!.compareDocumentPosition(navRow!)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(document.querySelector('.shell-header .language-switcher')).toBeNull();
  });

  test('admin menu is still present next to the other navigation items', () => {
    render(
      <BrowserRouter>
        <I18nextProvider i18n={i18n}>
          <AppShell />
        </I18nextProvider>
      </BrowserRouter>
    );

    expect(screen.getByRole('link', { name: /^Admin$/ })).toBeVisible();
    expect(screen.getByRole('link', { name: /patients/i })).toBeVisible();
  });
});
