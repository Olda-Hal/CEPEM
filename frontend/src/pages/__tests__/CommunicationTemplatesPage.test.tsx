import { vi, type Mocked } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { I18nextProvider } from 'react-i18next';
import i18n from '../../i18n';
import { apiClient } from '../../utils/api';
import { AuthProvider } from '../../contexts/AuthContext';
import { ToastProvider } from '../../contexts/ToastContext';
import CommunicationTemplatesPage from '../CommunicationTemplatesPage';

vi.mock('../../utils/api');
const mockApiClient = apiClient as Mocked<typeof apiClient>;

const templates = [
  {
    id: 1,
    code: 'intake_reminder',
    channel: 'Email',
    language: 'cs',
    scopeType: 'Global',
    scopeId: 0,
    scopeName: 'Global',
    subject: 'Vyplňte formulář',
    body: 'Dobrý den {{patientName}}',
    isActive: true,
    updatedAtUtc: '2026-11-01T10:00:00Z'
  },
  {
    id: 2,
    code: 'appointment_reminder',
    channel: 'Sms',
    language: 'en',
    scopeType: 'Center',
    scopeId: 7,
    scopeName: 'CEPEM Praha',
    subject: null,
    body: 'Reminder {{date}}',
    isActive: true,
    updatedAtUtc: '2026-11-01T10:00:00Z'
  }
];

const renderPage = () =>
  render(
    <BrowserRouter>
      <I18nextProvider i18n={i18n}>
        <AuthProvider>
          <ToastProvider>
            <CommunicationTemplatesPage />
          </ToastProvider>
        </AuthProvider>
      </I18nextProvider>
    </BrowserRouter>
  );

describe('CommunicationTemplatesPage', () => {
  beforeEach(() => {
    mockApiClient.get.mockReset();
    mockApiClient.get.mockImplementation((endpoint: string) => {
      if (endpoint.startsWith('/api/hospitals')) {
        return Promise.resolve([]);
      }
      return Promise.resolve(templates);
    });
  });

  test('renders templates with channel and scope', async () => {
    renderPage();

    expect(await screen.findByText('intake_reminder')).toBeInTheDocument();
    expect(screen.getByText('appointment_reminder')).toBeInTheDocument();
    expect(screen.getByText(/CEPEM Praha/)).toBeInTheDocument();
    expect(screen.getAllByText('SMS').length).toBeGreaterThan(0);
  });

  test('opens the editor with a textarea and variable hints', async () => {
    renderPage();
    await screen.findByText('intake_reminder');

    const editButtons = screen.getAllByRole('button', { name: /edit/i });
    fireEvent.click(editButtons[0]!);
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /body/i })).toBeInTheDocument();
    expect(screen.getByText('{{patientName}}')).toBeInTheDocument();
  });

  test('filter change reloads templates with the channel parameter', async () => {
    renderPage();
    await screen.findByText('intake_reminder');
    mockApiClient.get.mockClear();

    fireEvent.change(screen.getByLabelText(/channel/i), { target: { value: 'Sms' } });

    await waitFor(() => {
      expect(mockApiClient.get).toHaveBeenCalledWith(expect.stringContaining('channel=Sms'));
    });
  });
});
