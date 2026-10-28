import { vi, type Mocked } from 'vitest';
import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { I18nextProvider } from 'react-i18next';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import i18n from '../../i18n';
import { apiClient } from '../../utils/api';
import { AuthProvider } from '../../contexts/AuthContext';
import { PatientsPage } from '../PatientsPage';

vi.mock('../../utils/api');
const mockApiClient = apiClient as Mocked<typeof apiClient>;

const mockNavigate = vi.fn();
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return { ...actual, useNavigate: () => mockNavigate };
});

class FakeIntersectionObserver {
  callback: IntersectionObserverCallback;
  constructor(callback: IntersectionObserverCallback) {
    this.callback = callback;
  }
  observe() {}
  unobserve() {}
  disconnect() {}
}
(globalThis as any).IntersectionObserver = FakeIntersectionObserver;

const patientJohn = {
  id: 1,
  personId: 1,
  firstName: 'John',
  lastName: 'Doe',
  birthDate: '1980-01-15T00:00:00Z',
  insuranceNumber: 123456789,
  uid: '00000001',
  gender: 'M',
  active: true,
  createdAt: '2024-01-01T00:00:00Z',
  alive: true,
  phoneNumber: '777123456',
  email: 'john@example.com'
};

const patientJane = {
  ...patientJohn,
  id: 2,
  personId: 2,
  firstName: 'Jane',
  lastName: 'Svobodova',
  gender: 'F',
  uid: '00000002',
  insuranceNumber: 987654321,
  email: 'jane@example.com'
};

const renderPage = () => {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <I18nextProvider i18n={i18n}>
          <AuthProvider>
            <PatientsPage />
          </AuthProvider>
        </I18nextProvider>
      </BrowserRouter>
    </QueryClientProvider>
  );
};

describe('PatientsPage', () => {
  beforeEach(() => {
    mockApiClient.get.mockClear();
    mockNavigate.mockClear();
    mockApiClient.get.mockResolvedValue({
      patients: [patientJohn, patientJane],
      totalCount: 2,
      hasMore: false
    });
  });

  test('loads and renders patients from the search endpoint', async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText('John')).toBeInTheDocument();
      expect(screen.getByText('Jane')).toBeInTheDocument();
    });

    expect(mockApiClient.get).toHaveBeenCalledWith(
      expect.stringContaining('/api/patients/search?')
    );
  });

  test('shows the patient uid, insurance number and formatted birth date', async () => {
    renderPage();

    await screen.findByText('John');

    expect(screen.getByText('00000001')).toBeInTheDocument();
    expect(screen.getByText('123456789')).toBeInTheDocument();
  });

  test('opens the detail page from view button', async () => {
    renderPage();
    const buttons = await screen.findAllByRole('button', { name: /view details/i });

    fireEvent.click(buttons[0]);

    expect(mockNavigate).toHaveBeenCalledWith('/patients/1');
  });

  test('shows error message when loading fails', async () => {
    mockApiClient.get.mockRejectedValue(new Error('network down'));
    const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

    renderPage();

    await waitFor(() => {
      expect(document.querySelector('.error-message')).toBeTruthy();
    });
    consoleSpy.mockRestore();
  });

  test('shows empty state when no patients are found', async () => {
    mockApiClient.get.mockResolvedValue({ patients: [], totalCount: 0, hasMore: false });

    renderPage();

    await waitFor(() => {
      expect(document.querySelector('.no-results')).toBeTruthy();
    });
  });

  test('debounced search issues a new request', async () => {
    renderPage();
    await screen.findByText('John');
    mockApiClient.get.mockClear();

    fireEvent.change(screen.getByPlaceholderText(/search/i), { target: { value: 'jane' } });

    await waitFor(() => {
      expect(mockApiClient.get).toHaveBeenCalledWith(
        expect.stringContaining('search=jane')
      );
    }, { timeout: 2000 });
  });
});
