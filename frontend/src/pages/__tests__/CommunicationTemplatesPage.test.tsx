import { vi, type Mocked } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
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

    const row = screen.getByText('intake_reminder').closest('tr');
    fireEvent.click(within(row!).getByRole('button', { name: /edit/i }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toBeInTheDocument();
    expect(within(dialog).getByRole('textbox', { name: /body/i })).toBeInTheDocument();
    expect(within(dialog).getByText('{{patientName}}')).toBeInTheDocument();
  });

  test('creating a template posts the form and reloads', async () => {
    mockApiClient.post.mockResolvedValue({});
    renderPage();
    await screen.findByText('intake_reminder');

    fireEvent.click(screen.getByRole('button', { name: /new template/i }));
    const dialog = await screen.findByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText(/^subject$/i), {
      target: { value: 'Nova zprava' }
    });
    fireEvent.change(within(dialog).getByLabelText(/^body$/i), {
      target: { value: 'Dobry den {{patientName}}' }
    });
    fireEvent.click(within(dialog).getByRole('button', { name: /^save$/i }));

    await waitFor(() =>
      expect(mockApiClient.post).toHaveBeenCalledWith('/api/communicationtemplates', {
        code: 'password_reset',
        channel: 'Email',
        language: 'cs',
        scopeType: 'Global',
        scopeId: 0,
        subject: 'Nova zprava',
        body: 'Dobry den {{patientName}}',
        isActive: true
      })
    );
  });

  test('center scope offers hospitals and posts the selected center', async () => {
    mockApiClient.get.mockImplementation((endpoint: string) => {
      if (endpoint.startsWith('/api/hospitals')) {
        return Promise.resolve([{ id: 7, name: 'CEPEM Praha' }]);
      }
      return Promise.resolve([]);
    });
    mockApiClient.post.mockResolvedValue({});
    renderPage();

    fireEvent.click(await screen.findByRole('button', { name: /new template/i }));
    const dialog = await screen.findByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText(/^scope$/i), {
      target: { value: 'Center' }
    });
    expect(within(dialog).getByRole('option', { name: 'CEPEM Praha' })).toBeInTheDocument();

    fireEvent.change(within(dialog).getByLabelText(/^subject$/i), {
      target: { value: 'Center subject' }
    });
    fireEvent.change(within(dialog).getByLabelText(/^body$/i), {
      target: { value: 'Center body' }
    });
    fireEvent.click(within(dialog).getByRole('button', { name: /^save$/i }));

    await waitFor(() =>
      expect(mockApiClient.post).toHaveBeenCalledWith(
        '/api/communicationtemplates',
        expect.objectContaining({ scopeType: 'Center', scopeId: 7 })
      )
    );
  });

  test('groups language variants into one template row', async () => {
    const variants = [
      {
        id: 11,
        code: 'intake_reminder',
        channel: 'Email',
        language: 'cs',
        scopeType: 'Global',
        scopeId: 0,
        scopeName: 'Global',
        subject: 'Cesky predmet',
        body: 'Ceske telo',
        isActive: true,
        updatedAtUtc: '2026-11-01T10:00:00Z'
      },
      {
        id: 12,
        code: 'intake_reminder',
        channel: 'Email',
        language: 'en',
        scopeType: 'Global',
        scopeId: 0,
        scopeName: 'Global',
        subject: 'English subject',
        body: 'English body',
        isActive: true,
        updatedAtUtc: '2026-11-01T10:00:00Z'
      },
      {
        id: 13,
        code: 'intake_reminder',
        channel: 'Email',
        language: 'nl',
        scopeType: 'Global',
        scopeId: 0,
        scopeName: 'Global',
        subject: 'Nederlands onderwerp',
        body: 'Nederlandse tekst',
        isActive: true,
        updatedAtUtc: '2026-11-01T10:00:00Z'
      }
    ];
    mockApiClient.get.mockImplementation((endpoint: string) => {
      if (endpoint.startsWith('/api/hospitals')) {
        return Promise.resolve([]);
      }
      return Promise.resolve(variants);
    });
    renderPage();

    expect(await screen.findByText('intake_reminder')).toBeInTheDocument();
    expect(screen.getAllByText('CS').length).toBeGreaterThan(0);
    expect(screen.getAllByText('EN').length).toBeGreaterThan(0);
    expect(screen.getAllByText('NL').length).toBeGreaterThan(0);
    expect(screen.getByText('Cesky predmet')).toBeInTheDocument();

    const editButtons = screen.getAllByRole('button', { name: /edit/i });
    fireEvent.click(editButtons[0]!);
    const dialog = await screen.findByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText(/^language$/i), {
      target: { value: 'en' }
    });

    expect(within(dialog).getByLabelText(/^subject$/i)).toHaveValue('English subject');
    expect(within(dialog).getByLabelText(/^body$/i)).toHaveValue('English body');
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
