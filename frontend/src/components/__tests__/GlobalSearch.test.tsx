import { vi, type Mocked } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { BrowserRouter, MemoryRouter, useLocation } from 'react-router-dom';
import { I18nextProvider } from 'react-i18next';
import i18n from '../../i18n';
import { GlobalSearch } from '../GlobalSearch';
import { apiClient } from '../../utils/api';

vi.mock('../../utils/api');
const mockApiClient = apiClient as Mocked<typeof apiClient>;

const LocationProbe = () => {
  const location = useLocation();
  return <div data-testid="location">{location.pathname}</div>;
};

const renderSearch = () =>
  render(
    <BrowserRouter>
      <I18nextProvider i18n={i18n}>
        <div className="shell-header">
          <div className="shell-header-right">
            <GlobalSearch />
          </div>
        </div>
      </I18nextProvider>
    </BrowserRouter>
  );

describe('GlobalSearch', () => {
  beforeEach(() => {
    mockApiClient.get.mockReset();
    mockApiClient.get.mockResolvedValue({
      query: 'eva',
      patients: [
        {
          id: 5,
          uid: 'U-5',
          firstName: 'Eva',
          lastName: 'Kralova',
          birthDate: '1990-01-01',
          email: 'eva@example.com',
          phoneNumber: null
        }
      ],
      visits: [],
      documents: [],
      evaluations: [],
      employees: []
    });
  });

  test('renders the overlay in a body portal so the header cannot cover it', () => {
    renderSearch();

    fireEvent.click(screen.getByRole('button', { name: /search/i }));

    const dialog = screen.getByRole('dialog');
    const overlay = dialog.parentElement;
    expect(overlay).toHaveClass('global-search-overlay');
    expect(overlay?.parentElement).toBe(document.body);
    expect(overlay?.closest('.shell-header')).toBeNull();
  });

  test('typing a term renders grouped results', async () => {
    renderSearch();
    fireEvent.click(screen.getByRole('button', { name: /search/i }));

    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'eva' } });

    expect(await screen.findByText('Eva Kralova')).toBeInTheDocument();
    expect(screen.getByText(/UID U-5/)).toBeInTheDocument();
    expect(mockApiClient.get).toHaveBeenCalledWith('/api/search?q=eva&limit=5');
  });

  test('clicking a result navigates and closes the overlay', async () => {
    render(
      <MemoryRouter initialEntries={['/']}>
        <I18nextProvider i18n={i18n}>
          <GlobalSearch />
          <LocationProbe />
        </I18nextProvider>
      </MemoryRouter>
    );
    fireEvent.click(screen.getByRole('button', { name: /search/i }));
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'eva' } });
    fireEvent.click(await screen.findByText('Eva Kralova'));

    expect(screen.getByTestId('location')).toHaveTextContent('/patients/5');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  test('shows an error when the search request fails', async () => {
    mockApiClient.get.mockRejectedValue(new Error('offline'));
    renderSearch();
    fireEvent.click(screen.getByRole('button', { name: /search/i }));

    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'eva' } });

    expect(await screen.findByText('Search failed.')).toBeInTheDocument();
  });

  test('Ctrl+K opens the palette and Escape closes it', () => {
    renderSearch();

    fireEvent.keyDown(window, { key: 'k', ctrlKey: true });
    expect(screen.getByRole('dialog')).toBeInTheDocument();

    fireEvent.keyDown(screen.getByRole('textbox'), { key: 'Escape' });
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
