import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import BaseModal from '../BaseModal';
import { IntakeDigitisationRegion, IntakeDigitisationResult } from '../../types';
import './IntakeDigitisePreviewModal.css';

interface IntakeDigitisePreviewModalProps {
  file: File;
  result: IntakeDigitisationResult;
  onClose: () => void;
}

const displayValue = (
  region: IntakeDigitisationRegion,
  t: (key: string, options?: Record<string, unknown>) => string
): string => {
  if (region.value === null || region.value === undefined || region.value === '') {
    return t('intakeDigitise.noValue');
  }
  if (region.value === 'marked') return t('intakeDigitise.marked');
  if (region.value === 'empty') return t('intakeDigitise.empty');
  return String(region.value);
};

/**
 * Side-by-side comparison of the uploaded scan and what the AI read.
 * Detected fields are drawn as outline-only boxes over the page image; the
 * OCR text and accepted values live in a separate panel, so nothing covers
 * the original content.
 */
export const IntakeDigitisePreviewModal: React.FC<IntakeDigitisePreviewModalProps> = ({
  file,
  result,
  onClose
}) => {
  const { t } = useTranslation();
  const [pageIndex, setPageIndex] = useState(0);

  const pdfUrl = useMemo(() => URL.createObjectURL(file), [file]);
  useEffect(() => () => URL.revokeObjectURL(pdfUrl), [pdfUrl]);

  const pages = result.previewPages ?? [];
  const currentPage = pages[pageIndex] ?? pages[0] ?? null;
  const pageNumber = currentPage?.page ?? pageIndex;
  const regions = (result.regions ?? []).filter((region) => region.page === pageNumber);
  const pageText = result.pageTexts?.[pageNumber] ?? '';

  const fieldLabel = (field: string): string => t(`patientForm.${field}`, { defaultValue: field });

  return (
    <BaseModal
      title={t('intakeDigitise.previewTitle')}
      onClose={onClose}
      contentClassName="intake-preview-modal"
    >
      <div className="intake-preview-toolbar">
        <span className={`intake-preview-badge${result.isIntakeForm ? ' ok' : ' warning'}`}>
          {result.isIntakeForm ? t('intakeDigitise.detected') : t('intakeDigitise.notDetected')}
        </span>
        <span className="muted small">
          {t('intakeDigitise.confidence', {
            value: Math.round((result.confidence ?? 0) * 100)
          })}
        </span>
        <a className="btn btn-secondary btn-sm" href={pdfUrl} target="_blank" rel="noreferrer">
          {t('intakeDigitise.openPdf')}
        </a>
      </div>

      {!result.isIntakeForm && (
        <p className="intake-preview-hint">{t('intakeDigitise.notDetectedHint')}</p>
      )}

      {pages.length > 1 && (
        <div className="intake-preview-pages" role="tablist">
          {pages.map((page, index) => (
            <button
              key={page.page}
              type="button"
              role="tab"
              aria-selected={index === pageIndex}
              className={`intake-preview-page-tab${index === pageIndex ? ' active' : ''}`}
              onClick={() => setPageIndex(index)}
            >
              {t('intakeDigitise.page', { page: page.page + 1 })}
            </button>
          ))}
        </div>
      )}

      <div className="intake-preview-layout">
        <div className="intake-preview-canvas">
          {currentPage ? (
            <div className="intake-preview-image-wrap">
              <img
                className="intake-preview-image"
                src={`data:image/jpeg;base64,${currentPage.image}`}
                alt={t('intakeDigitise.page', { page: pageNumber + 1 })}
              />
              {regions.map((region, index) => (
                <span
                  key={`${region.field}-${index}`}
                  className={`intake-region-box${region.warning ? ' warning' : ''}`}
                  style={{
                    left: `${region.x * 100}%`,
                    top: `${region.y * 100}%`,
                    width: `${region.width * 100}%`,
                    height: `${region.height * 100}%`
                  }}
                  title={`${fieldLabel(region.field)}: ${displayValue(region, t)}`}
                  aria-label={`${fieldLabel(region.field)}: ${displayValue(region, t)}`}
                />
              ))}
            </div>
          ) : (
            <p className="muted">{t('intakeDigitise.previewUnavailable')}</p>
          )}
        </div>

        <div className="intake-preview-panel">
          <h3>{t('intakeDigitise.fields')}</h3>
          {regions.length === 0 ? (
            <p className="muted">{t('intakeDigitise.noFields')}</p>
          ) : (
            <ul className="intake-region-list">
              {regions.map((region, index) => (
                <li
                  key={`${region.field}-${index}`}
                  className={`intake-region-item${region.warning ? ' warning' : ''}`}
                >
                  <div className="intake-region-header">
                    <strong>{fieldLabel(region.field)}</strong>
                    {region.warning && (
                      <span className="intake-region-warning">{t('intakeDigitise.warning')}</span>
                    )}
                  </div>
                  <dl className="intake-region-values">
                    <dt>{t('intakeDigitise.value')}</dt>
                    <dd>{displayValue(region, t)}</dd>
                    <dt>{t('intakeDigitise.raw')}</dt>
                    <dd className="intake-region-raw">
                      {region.text || t('intakeDigitise.noValue')}
                    </dd>
                    {region.confidence !== null && (
                      <>
                        <dt>{t('intakeDigitise.ocrConfidence')}</dt>
                        <dd>{Math.round(region.confidence)} %</dd>
                      </>
                    )}
                  </dl>
                </li>
              ))}
            </ul>
          )}

          <details className="intake-preview-text">
            <summary>{t('intakeDigitise.rawText')}</summary>
            <pre>{pageText || t('intakeDigitise.noValue')}</pre>
          </details>
        </div>
      </div>
    </BaseModal>
  );
};

export default IntakeDigitisePreviewModal;
