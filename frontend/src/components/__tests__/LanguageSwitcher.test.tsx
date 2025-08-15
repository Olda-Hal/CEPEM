import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import { LanguageSwitcher } from '../LanguageSwitcher';
import i18n from '../../i18n';

const renderWithI18n = (component: React.ReactElement) => {
  return render(
    <I18nextProvider i18n={i18n}>
      {component}
    </I18nextProvider>
  );
};

describe('LanguageSwitcher', () => {
  test('renders language options', () => {
    renderWithI18n(<LanguageSwitcher />);
    
    expect(screen.getByText(/Čeština/)).toBeInTheDocument();
    expect(screen.getByText(/English/)).toBeInTheDocument();
  });

  test('changes language when option is selected', () => {
    renderWithI18n(<LanguageSwitcher />);
    
    const select = screen.getByRole('combobox');
    fireEvent.change(select, { target: { value: 'en' } });
    
    expect(i18n.language).toBe('en');
  });

  test('shows current language as selected', () => {
    renderWithI18n(<LanguageSwitcher />);
    
    const select = screen.getByRole('combobox') as HTMLSelectElement;
    expect(select.value).toBe(i18n.language);
  });

  test('applies correct CSS classes', () => {
    renderWithI18n(<LanguageSwitcher />);
    
    const switcher = screen.getByRole('combobox').parentElement;
    expect(switcher).toHaveClass('language-switcher');
  });
});
