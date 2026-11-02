import { vi, type Mocked } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { I18nextProvider } from 'react-i18next';
import i18n from '../../i18n';
import { apiClient } from '../../utils/api';
import PublicGdprPage from '../PublicGdprPage';

vi.mock('../../utils/api');
const mockApiClient = apiClient as Mocked<typeof apiClient>;

const renderPage = (entry = '/gdpr?lang=cs') =>
  render(
    <MemoryRouter initialEntries={[entry]}>
      <I18nextProvider i18n={i18n}>
        <PublicGdprPage />
      </I18nextProvider>
    </MemoryRouter>
  );

describe('PublicGdprPage', () => {
  it('renders the published wording and switches language', async () => {
    await i18n.changeLanguage('cs');
    mockApiClient.get.mockImplementation(async (url: string) => {
      if (url.includes('language=en')) {
        return {
          available: true,
          version: '2',
          language: 'en',
          title: 'GDPR EN',
          text: 'English wording',
          availableLanguages: ['cs', 'en']
        };
      }
      return {
        available: true,
        version: '1',
        language: 'cs',
        title: 'GDPR CZ',
        text: 'Czech wording',
        availableLanguages: ['cs', 'en']
      };
    });

    renderPage();

    expect(await screen.findByText('GDPR CZ')).toBeInTheDocument();
    expect(screen.getByText('Czech wording')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('tab', { name: 'English' }));

    expect(await screen.findByText('GDPR EN')).toBeInTheDocument();
    expect(screen.getByText('English wording')).toBeInTheDocument();
    expect(mockApiClient.get).toHaveBeenCalledWith('/api/gdpr/public/current?language=en');
  });

  it('explains when no wording is published', async () => {
    await i18n.changeLanguage('nl');
    mockApiClient.get.mockResolvedValue({
      available: false,
      language: 'nl',
      availableLanguages: []
    });

    renderPage('/gdpr?lang=nl');

    expect(await screen.findByText('Deze tekst is nog niet gepubliceerd.')).toBeInTheDocument();
  });
});
