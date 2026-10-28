import React, { useCallback, useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';
import './BaseModal.css';

interface BaseModalProps {
  title?: React.ReactNode;
  onClose: () => void;
  /** Clicking the overlay also closes (default true) */
  closeOnOverlayClick?: boolean;
  /** Extra class for the content wrapper, e.g. 'permissions-modal' */
  contentClassName?: string;
  children: React.ReactNode;
}

const FOCUSABLE =
  'a[href], button:not([disabled]), textarea, input, select, [tabindex]:not([tabindex="-1"])';

/**
 * Shared modal: renders into a portal, traps focus, closes on Escape and
 * (optionally) on overlay click. Replaces the per-file modal-overlay /
 * modal-content CSS copies.
 */
export const BaseModal: React.FC<BaseModalProps> = ({
  title,
  onClose,
  closeOnOverlayClick = true,
  contentClassName,
  children
}) => {
  const contentRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const previouslyFocused = document.activeElement as HTMLElement | null;
    const overflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.stopPropagation();
        onClose();
        return;
      }
      if (event.key === 'Tab' && contentRef.current) {
        // Simple focus trap: cycle within the modal
        const focusable = Array.from(contentRef.current.querySelectorAll<HTMLElement>(FOCUSABLE));
        if (focusable.length === 0) {
          return;
        }
        const first = focusable[0];
        const last = focusable[focusable.length - 1];
        if (!first || !last) {
          return;
        }
        if (event.shiftKey && document.activeElement === first) {
          event.preventDefault();
          last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
          event.preventDefault();
          first.focus();
        }
      }
    };

    document.addEventListener('keydown', handleKeyDown, true);
    // Move focus into the modal
    window.setTimeout(() => {
      const focusable = contentRef.current?.querySelectorAll<HTMLElement>(FOCUSABLE);
      (focusable && focusable.length > 0 ? focusable[0] : contentRef.current)?.focus();
    }, 0);

    return () => {
      document.removeEventListener('keydown', handleKeyDown, true);
      document.body.style.overflow = overflow;
      previouslyFocused?.focus();
    };
  }, [onClose]);

  const handleOverlayClick = useCallback(
    (event: React.MouseEvent) => {
      if (closeOnOverlayClick && event.target === event.currentTarget) {
        onClose();
      }
    },
    [closeOnOverlayClick, onClose]
  );

  return createPortal(
    <div className="modal-overlay" onClick={handleOverlayClick}>
      <div
        ref={contentRef}
        className={`modal-content${contentClassName ? ` ${contentClassName}` : ''}`}
        role="dialog"
        aria-modal="true"
        aria-label={typeof title === 'string' ? title : undefined}
        tabIndex={-1}
      >
        {title !== undefined && (
          <div className="modal-header">
            <h2>{title}</h2>
            <button className="close-button" onClick={onClose} aria-label="Close">
              &times;
            </button>
          </div>
        )}
        {children}
      </div>
    </div>,
    document.body
  );
};

export default BaseModal;
