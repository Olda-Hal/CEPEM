import { vi, type Mocked } from 'vitest';
import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { I18nextProvider } from 'react-i18next';
import i18n from '../../i18n';
import { apiClient } from '../../utils/api';
import { AuthProvider } from '../../contexts/AuthContext';
import { ToastProvider } from '../../contexts/ToastContext';
import ActivityLogPage from '../ActivityLogPage';

vi.mock('../../utils/api');
const mockApiClient = apiClient as Mocked<typeof apiClient>;

const mockLogs = {
  logs: [
    {
      id: 2,
      timestamp: '2026-10-27T10:00:00Z',
      service: 'HealthcareAPI',
      action: '/api/events',
      details: 'Request from 10.0.0.2',
      employeeId: 7,
      employeeEmail: 'nurse@example.com',
      method: 'POST',
      statusCode: 500
    },
    {
      id: 1,
      timestamp: '2026-10-27T09:00:00Z',
      service: 'HealthcareAPI',
      action: '/api/patients',
      details: 'Request from 10.0.0.1',
      employeeId: 3,
      employeeEmail: 'doctor@example.com',
      method: 'GET',
      statusCode: 200
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
            <ActivityLogPage />
          </ToastProvider>
        </AuthProvider>
      </I18nextProvider>
    </BrowserRouter>
  );

describe('ActivityLogPage', () => {
  beforeEach(() => {
    mockApiClient.get.mockClear();
    mockApiClient.get.mockResolvedValue(mockLogs);
  });

  test('loads and renders log rows', async () => {
    renderPage();

    await screen.findByText('/api/patients');

    expect(screen.getByText('/api/events')).toBeInTheDocument();
    expect(screen.getByText('doctor@example.com')).toBeInTheDocument();
    expect(screen.getByText('nurse@example.com')).toBeInTheDocument();
  });

  test('request includes method and status filters after change', async () => {
    renderPage();
    await screen.findByText('/api/patients');
    mockApiClient.get.mockClear();

    fireEvent.change(screen.getByLabelText(/method/i), { target: { value: 'POST' } });

    await waitFor(() => {
      expect(mockApiClient.get).toHaveBeenCalledWith(expect.stringContaining('method=POST'));
    });
  });

  test('clicking a row opens the detail modal', async () => {
    renderPage();
    await screen.findByText('/api/patients');

    fireEvent.click(screen.getByText('doctor@example.com'));

    expect(await screen.findByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText('Request from 10.0.0.1')).toBeInTheDocument();
  });

  test('detail modal closes via its close button', async () => {
    renderPage();
    await screen.findByText('/api/patients');
    fireEvent.click(screen.getByText('doctor@example.com'));
    await screen.findByRole('dialog');

    fireEvent.click(screen.getByRole('button', { name: /close/i }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  test('search submits with the search parameter', async () => {
    renderPage();
    await screen.findByText('/api/patients');
    mockApiClient.get.mockClear();

    fireEvent.change(screen.getByPlaceholderText(/search/i), { target: { value: 'patients' } });
    fireEvent.click(screen.getByRole('button', { name: /^search$/i }));

    await waitFor(() => {
      expect(mockApiClient.get).toHaveBeenCalledWith(expect.stringContaining('search=patients'));
    });
  });
});
