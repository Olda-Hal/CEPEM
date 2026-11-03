import React from 'react';
import i18n from '../i18n';
import './ErrorBoundary.css';

interface ErrorBoundaryState {
  hasError: boolean;
}

/**
 * Last-resort UI guard: a render error or a failed lazy chunk must not leave a
 * blank page. The user gets an explanation and a reload action.
 */
export class ErrorBoundary extends React.Component<
  { children: React.ReactNode },
  ErrorBoundaryState
> {
  state: ErrorBoundaryState = { hasError: false };

  static getDerivedStateFromError(): ErrorBoundaryState {
    return { hasError: true };
  }

  componentDidCatch(error: Error, info: React.ErrorInfo): void {
    console.error('Unhandled UI error', error, info);
  }

  render(): React.ReactNode {
    if (!this.state.hasError) {
      return this.props.children;
    }

    return (
      <div className="error-boundary" role="alert">
        <h1>{i18n.t('errors.boundaryTitle')}</h1>
        <p>{i18n.t('errors.boundaryDescription')}</p>
        <button type="button" onClick={() => window.location.reload()}>
          {i18n.t('errors.reload')}
        </button>
      </div>
    );
  }
}
