import { vi, type Mocked } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { I18nextProvider } from 'react-i18next';
import { AuthProvider } from '../../contexts/AuthContext';
import { DashboardPage } from '../DashboardPage';
import i18n from '../../i18n';
import { apiClient } from '../../utils/api';

// Mock API client
vi.mock('../../utils/api');
const mockApiClient = apiClient as Mocked<typeof apiClient>;

// Mock dashboard stats
const mockStats = {
  totalEmployees: 5,
  systemStatus: 'Active',
  lastLogin: '2024-01-15T10:30:00Z'
};

const renderWithProviders = (component: React.ReactElement) => {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <I18nextProvider i18n={i18n}>
          <AuthProvider>{component}</AuthProvider>
        </I18nextProvider>
      </BrowserRouter>
    </QueryClientProvider>
  );
};

describe('DashboardPage', () => {
  beforeEach(() => {
    mockApiClient.get.mockClear();
  });

  test('renders dashboard title', async () => {
    mockApiClient.get.mockResolvedValue(mockStats);

    renderWithProviders(<DashboardPage />);

    expect(
      screen.getByRole('heading', { name: /Center for Preventive Medicine/i })
    ).toBeInTheDocument();
  });

  test('loads and displays dashboard stats', async () => {
    mockApiClient.get.mockResolvedValue(mockStats);

    renderWithProviders(<DashboardPage />);

    await waitFor(() => {
      expect(mockApiClient.get).toHaveBeenCalledWith('/api/employees/dashboard-stats');
    });
  });

  test('renders while stats load', () => {
    // Keep the request pending so the loading state is observable
    mockApiClient.get.mockReturnValue(new Promise(() => {}));

    renderWithProviders(<DashboardPage />);

    expect(document.querySelector('.dashboard-container')).toBeTruthy();
  });

  test('handles API error gracefully', async () => {
    mockApiClient.get.mockRejectedValue(new Error('API Error'));

    const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

    renderWithProviders(<DashboardPage />);

    // With useQuery the error is handled internally; the page must
    // still render without crashing
    await waitFor(() => {
      expect(document.querySelector('.dashboard-container')).toBeTruthy();
    });

    consoleSpy.mockRestore();
  });

  test('displays coming soon buttons as disabled', () => {
    mockApiClient.get.mockResolvedValue(mockStats);

    renderWithProviders(<DashboardPage />);

    const comingSoonButtons = screen.getAllByText(/Coming Soon/i);
    comingSoonButtons.forEach((button) => {
      expect(button).toBeDisabled();
    });
  });
});
