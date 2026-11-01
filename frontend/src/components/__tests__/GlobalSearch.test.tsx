import { vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { I18nextProvider } from 'react-i18next';
import i18n from '../../i18n';
import { GlobalSearch } from '../GlobalSearch';

vi.mock('../../utils/api');

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
  test('renders the overlay in a body portal so the header cannot cover it', () => {
    renderSearch();

    fireEvent.click(screen.getByRole('button', { name: /search/i }));

    const dialog = screen.getByRole('dialog');
    const overlay = dialog.parentElement;
    expect(overlay).toHaveClass('global-search-overlay');
    expect(overlay?.parentElement).toBe(document.body);
    expect(overlay?.closest('.shell-header')).toBeNull();
  });

  test('Ctrl+K opens the palette and Escape closes it', () => {
    renderSearch();

    fireEvent.keyDown(window, { key: 'k', ctrlKey: true });
    expect(screen.getByRole('dialog')).toBeInTheDocument();

    fireEvent.keyDown(screen.getByRole('textbox'), { key: 'Escape' });
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
