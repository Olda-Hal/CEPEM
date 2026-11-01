import { render, screen } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from '../../i18n';
import SettingsPage from '../SettingsPage';

vi.mock('../../components/PasswordChangeModal', () => ({
  PasswordChangeModal: () => null
}));

describe('SettingsPage', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
  });

  test('language selection lives in settings', () => {
    render(
      <I18nextProvider i18n={i18n}>
        <SettingsPage />
      </I18nextProvider>
    );

    const language = screen.getByRole('combobox');
    expect(language).toBeInTheDocument();
    expect(screen.getByText('Language')).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /english/i })).toBeInTheDocument();
  });
});
