import { vi, type Mocked } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { I18nextProvider } from 'react-i18next';
import i18n from '../../i18n';
import { apiClient } from '../../utils/api';
import { AuthProvider } from '../../contexts/AuthContext';
import { ToastProvider } from '../../contexts/ToastContext';
import EmailLogPage from '../EmailLogPage';

vi.mock('../../utils/api');
const mockApiClient = apiClient as Mocked<typeof apiClient>;

const logs = {
  logs: [
    {
      id: 2,
      createdAtUtc: '2026-11-01T10:00:00Z',
      toAddress: 'eva@example.com',
      subject: 'Rezervace potvrzena',
      kind: 'ReservationConfirmed',
      status: 'Sent',
      error: null,
      personId: 5,
      recipientName: 'Eva Kralova',
      reservationSlotId: 9
    },
    {
      id: 1,
      createdAtUtc: '2026-11-01T09:00:00Z',
      toAddress: 'jan@example.com',
      subject: 'Reset hesla',
      kind: 'PasswordReset',
      status: 'Failed',
      error: 'SMTP timeout',
      personId: null,
      recipientName: null,
      reservationSlotId: null
    }
  ],
  totalCount: 2,
  hasMore: false
};

const renderPage = () =>
  render(
    <BrowserRouter>
      <I18nextProvider i18n={i18n}>
        <AuthProvider>
          <ToastProvider>
            <EmailLogPage />
          </ToastProvider>
        </AuthProvider>
      </I18nextProvider>
    </BrowserRouter>
  );

describe('EmailLogPage', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
    mockApiClient.get.mockReset();
    mockApiClient.get.mockResolvedValue(logs);
  });

  test('renders reservation e-mail kinds and statuses', async () => {
    renderPage();

    expect(await screen.findByText('Rezervace potvrzena')).toBeInTheDocument();
    expect(screen.getByText('Eva Kralova')).toBeInTheDocument();
    expect(screen.getAllByText('Failed').length).toBeGreaterThan(0);

    fireEvent.change(screen.getByLabelText(/type|typ/i), {
      target: { value: 'ReservationConfirmed' }
    });
    await waitFor(() =>
      expect(mockApiClient.get).toHaveBeenCalledWith(
        expect.stringContaining('kind=ReservationConfirmed')
      )
    );
  });

  test('opens a row detail with the SMTP error and slot', async () => {
    renderPage();
    await screen.findByText('Reset hesla');

    fireEvent.click(screen.getByText('Reset hesla'));

    expect(await screen.findByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText('SMTP timeout')).toBeInTheDocument();
  });

  test('SMS tab loads the SMS delivery log in the same page', async () => {
    mockApiClient.get.mockImplementation((endpoint: string) => {
      if (endpoint.includes('/sms-log')) {
        return Promise.resolve([
          {
            id: 1,
            createdAtUtc: '2026-11-01T10:00:00Z',
            toNumber: '+420777123456',
            templateCode: 'appointment_reminder',
            status: 'Sent',
            error: null,
            recipientName: 'Eva Kralova',
            reservationSlotId: 9
          }
        ]);
      }
      return Promise.resolve(logs);
    });
    renderPage();
    await screen.findByText('Rezervace potvrzena');

    fireEvent.click(screen.getByRole('tab', { name: /^SMS$/i }));

    expect(await screen.findByText('Eva Kralova')).toBeInTheDocument();
    expect(screen.getByText('+420777123456')).toBeInTheDocument();
    expect(mockApiClient.get).toHaveBeenCalledWith('/api/communicationtemplates/sms-log?limit=100');
  });

  test('search submits the trimmed term', async () => {
    renderPage();
    await screen.findByText('Rezervace potvrzena');
    mockApiClient.get.mockClear();

    const search = screen.getByPlaceholderText(/search recipient|hledat příjemce/i);
    fireEvent.change(search, { target: { value: '  eva  ' } });
    fireEvent.click(screen.getByRole('button', { name: /search|hledat/i }));

    await waitFor(() =>
      expect(mockApiClient.get).toHaveBeenCalledWith(expect.stringContaining('search=eva'))
    );
  });
});
