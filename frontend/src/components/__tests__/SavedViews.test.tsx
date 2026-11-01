import { vi, type Mocked } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from '../../i18n';
import { apiClient } from '../../utils/api';
import { SavedViews } from '../SavedViews';

vi.mock('../../utils/api');
const mockApiClient = apiClient as Mocked<typeof apiClient>;

const views = [
  {
    id: 1,
    surface: 'patients',
    name: 'Only deleted',
    filtersJson: '{"search":"","showDeleted":true}',
    isDefault: false,
    updatedAtUtc: '2026-11-01T10:00:00Z'
  },
  {
    id: 2,
    surface: 'patients',
    name: 'Daily list',
    filtersJson: '{"search":"novak","showDeleted":false}',
    isDefault: true,
    updatedAtUtc: '2026-11-01T10:00:00Z'
  }
];

const renderViews = (
  onApply = vi.fn(),
  props: Partial<React.ComponentProps<typeof SavedViews>> = {}
) =>
  render(
    <I18nextProvider i18n={i18n}>
      <SavedViews
        surface="patients"
        filters={{ search: 'current', showDeleted: false }}
        onApply={onApply}
        {...props}
      />
    </I18nextProvider>
  );

describe('SavedViews', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
    mockApiClient.get.mockReset();
    mockApiClient.post.mockReset();
    mockApiClient.delete.mockReset();
    mockApiClient.get.mockResolvedValue(views);
    vi.spyOn(window, 'confirm').mockReturnValue(true);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  test('loads views and applies the stored default once', async () => {
    const onApply = vi.fn();
    renderViews(onApply);

    await waitFor(() => expect(onApply).toHaveBeenCalledTimes(1));
    expect(onApply).toHaveBeenCalledWith({ search: 'novak', showDeleted: false });
    expect(mockApiClient.get).toHaveBeenCalledWith('/api/savedviews?surface=patients');
  });

  test('does not auto-apply the default when disabled', async () => {
    const onApply = vi.fn();
    renderViews(onApply, { autoApplyDefault: false });

    await screen.findByText(/Daily list/);
    expect(onApply).not.toHaveBeenCalled();
  });

  test('selecting a view applies its filters', async () => {
    const onApply = vi.fn();
    renderViews(onApply, { autoApplyDefault: false });
    await screen.findByText(/Daily list/);
    onApply.mockClear();

    fireEvent.change(screen.getByRole('combobox'), { target: { value: '1' } });

    expect(onApply).toHaveBeenCalledWith({ search: '', showDeleted: true });
  });

  test('saving posts the current filters and reloads', async () => {
    mockApiClient.post.mockResolvedValue({
      id: 3,
      surface: 'patients',
      name: 'My view',
      filtersJson: '{"search":"current","showDeleted":false}',
      isDefault: false,
      updatedAtUtc: '2026-11-01T10:00:00Z'
    });
    const onApply = vi.fn();
    renderViews(onApply, { autoApplyDefault: false });
    await screen.findByText(/Daily list/);
    mockApiClient.get.mockClear();

    fireEvent.click(screen.getByRole('button', { name: /save view/i }));
    const dialog = await screen.findByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText(/view name/i), {
      target: { value: 'My view' }
    });
    fireEvent.click(within(dialog).getByRole('button', { name: /save view/i }));

    await waitFor(() => {
      expect(mockApiClient.post).toHaveBeenCalledWith('/api/savedviews', {
        surface: 'patients',
        name: 'My view',
        filtersJson: JSON.stringify({ search: 'current', showDeleted: false }),
        isDefault: false
      });
    });
    await waitFor(() => expect(mockApiClient.get).toHaveBeenCalled());
  });

  test('delete only affects the selected view after confirmation', async () => {
    mockApiClient.delete.mockResolvedValue(undefined);
    renderViews(vi.fn(), { autoApplyDefault: false });
    await screen.findByText(/Daily list/);

    fireEvent.change(screen.getByRole('combobox'), { target: { value: '1' } });
    fireEvent.click(screen.getByRole('button', { name: /^delete$/i }));

    await waitFor(() => {
      expect(window.confirm).toHaveBeenCalledWith('Delete the saved view "Only deleted"?');
      expect(mockApiClient.delete).toHaveBeenCalledWith('/api/savedviews/1');
    });
  });

  test('set default posts to the default endpoint', async () => {
    mockApiClient.post.mockResolvedValue(undefined);
    renderViews(vi.fn(), { autoApplyDefault: false });
    await screen.findByText(/Daily list/);
    mockApiClient.get.mockClear();

    fireEvent.change(screen.getByRole('combobox'), { target: { value: '1' } });
    fireEvent.click(screen.getByRole('button', { name: /set as default/i }));

    await waitFor(() => {
      expect(mockApiClient.post).toHaveBeenCalledWith('/api/savedviews/1/default', {});
    });
  });

  test('shows a load error instead of an empty list', async () => {
    mockApiClient.get.mockRejectedValue(new Error('boom'));
    renderViews();

    expect(await screen.findByText('Could not load saved views.')).toBeInTheDocument();
  });
});
