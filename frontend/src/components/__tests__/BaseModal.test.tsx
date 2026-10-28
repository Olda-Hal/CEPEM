import React, { useState } from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { vi } from 'vitest';
import { BaseModal } from '../BaseModal';

const Harness: React.FC = () => {
  const [value, setValue] = useState('');
  return (
    <BaseModal title="Harness" onClose={() => {}}>
      <input
        data-testid="typed-input"
        value={value}
        onChange={(e) => setValue(e.target.value)}
      />
    </BaseModal>
  );
};

describe('BaseModal', () => {
  test('renders children inside a dialog', () => {
    render(
      <BaseModal title="Test modal" onClose={() => {}}>
        <p>modal body</p>
      </BaseModal>
    );

    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText('modal body')).toBeInTheDocument();
    expect(screen.getByText('Test modal')).toBeInTheDocument();
  });

  test('dialog is labelled by its title', () => {
    render(
      <BaseModal title="Accessibility title" onClose={() => {}}>
        <p>content</p>
      </BaseModal>
    );

    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveAttribute('aria-modal', 'true');
    expect(dialog).toHaveAttribute('aria-label', 'Accessibility title');
  });

  test('closes when the close button is clicked', () => {
    const onClose = vi.fn();
    render(
      <BaseModal title="Test" onClose={onClose}>
        <p>content</p>
      </BaseModal>
    );

    fireEvent.click(screen.getByRole('button', { name: /close/i }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  test('closes on Escape key', () => {
    const onClose = vi.fn();
    render(
      <BaseModal title="Test" onClose={onClose}>
        <p>content</p>
      </BaseModal>
    );

    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  test('closes on overlay click by default', () => {
    const onClose = vi.fn();
    render(
      <BaseModal title="Test" onClose={onClose}>
        <p>content</p>
      </BaseModal>
    );

    fireEvent.click(document.querySelector('.modal-overlay')!);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  test('does not close on overlay click when disabled', () => {
    const onClose = vi.fn();
    render(
      <BaseModal title="Test" onClose={onClose} closeOnOverlayClick={false}>
        <p>content</p>
      </BaseModal>
    );

    fireEvent.click(document.querySelector('.modal-overlay')!);
    expect(onClose).not.toHaveBeenCalled();
  });

  test('does not close when clicking inside the content', () => {
    const onClose = vi.fn();
    render(
      <BaseModal title="Test" onClose={onClose}>
        <button>inside</button>
      </BaseModal>
    );

    fireEvent.click(screen.getByText('inside'));
    expect(onClose).not.toHaveBeenCalled();
  });

  test('does NOT steal focus back when the parent re-renders after typing', () => {
    render(<Harness />);

    const input = screen.getByTestId('typed-input');
    input.focus();
    expect(document.activeElement).toBe(input);

    // Each keystroke re-renders the parent with a fresh onClose identity;
    // previously the mount effect re-fired and yanked focus back to the
    // first focusable element (the close button).
    fireEvent.change(input, { target: { value: 'abc' } });
    fireEvent.change(input, { target: { value: 'abcd' } });

    expect(document.activeElement).toBe(input);
    expect(input).toHaveValue('abcd');
  });

  test('locks body scroll while open', () => {
    const { unmount } = render(
      <BaseModal title="Test" onClose={() => {}}>
        <p>content</p>
      </BaseModal>
    );

    expect(document.body.style.overflow).toBe('hidden');
    unmount();
    expect(document.body.style.overflow).not.toBe('hidden');
  });
});
