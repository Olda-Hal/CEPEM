import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { I18nextProvider } from 'react-i18next';
import { AuthProvider } from '../../contexts/AuthContext';
import { DashboardPage } from '../DashboardPage';
import i18n from '../../i18n';
import { apiClient } from '../../utils/api';

// Mock API client
jest.mock('../../utils/api');
const mockApiClient = apiClient as jest.Mocked<typeof apiClient>;

// Mock user data
const mockUser = {
  id: 1,
  firstName: 'John',
  lastName: 'Doe',
  email: 'john.doe@example.com',
  uid: 'EMP001',
  active: true,
  fullName: 'John Doe'
};

// Mock dashboard stats
const mockStats = {
  totalEmployees: 5,
  systemStatus: 'Active',
  lastLogin: '2024-01-15T10:30:00Z'
};

const renderWithProviders = (component: React.ReactElement) => {
  return render(
    <BrowserRouter>
      <I18nextProvider i18n={i18n}>
        <AuthProvider>
          {component}
        </AuthProvider>
      </I18nextProvider>
    </BrowserRouter>
  );
};

describe('DashboardPage', () => {
  beforeEach(() => {
    mockApiClient.get.mockClear();
  });

  test('renders dashboard title', async () => {
    mockApiClient.get.mockResolvedValue(mockStats);
    
    renderWithProviders(<DashboardPage />);
    
    expect(screen.getByRole('heading', { name: /Center for Preventive Medicine/i })).toBeInTheDocument();
  });

  test('loads and displays dashboard stats', async () => {
    mockApiClient.get.mockResolvedValue(mockStats);
    
    renderWithProviders(<DashboardPage />);
    
    await waitFor(() => {
      expect(mockApiClient.get).toHaveBeenCalledWith('/api/doctors/dashboard-stats');
    });
  });

  test('displays loading state initially', () => {
    mockApiClient.get.mockImplementation(() => new Promise(() => {})); // Never resolves
    
    renderWithProviders(<DashboardPage />);
    
    expect(screen.getByText(/Loading.../i)).toBeInTheDocument();
  });

  test('handles API error gracefully', async () => {
    mockApiClient.get.mockRejectedValue(new Error('API Error'));
    
    const consoleSpy = jest.spyOn(console, 'error').mockImplementation();
    
    renderWithProviders(<DashboardPage />);
    
    await waitFor(() => {
      expect(consoleSpy).toHaveBeenCalled();
    });
    
    consoleSpy.mockRestore();
  });

  test('displays coming soon buttons as disabled', () => {
    mockApiClient.get.mockResolvedValue(mockStats);
    
    renderWithProviders(<DashboardPage />);
    
    const comingSoonButtons = screen.getAllByText(/Coming Soon/i);
    comingSoonButtons.forEach(button => {
      expect(button).toBeDisabled();
    });
  });
});
