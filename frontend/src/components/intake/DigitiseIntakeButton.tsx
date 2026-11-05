import React, { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { apiClient, ApiError } from '../../utils/api';
import { useAuth } from '../../contexts/AuthContext';
import { hasRole, isAdmin } from '../../utils/roles';
import { useToast } from '../../contexts/ToastContext';
import { IntakeDigitisationResult } from '../../types';
import { IntakeDigitisePreviewModal } from './IntakeDigitisePreviewModal';
import './DigitiseIntakeButton.css';

interface DigitiseIntakeButtonProps {
  onExtracted: (fields: Record<string, unknown>) => void;
}

/**
 * Digitizer-only shortcut: upload a scan of the paper form, let the AI
 * service read it and prefill the digital form for review. The last upload
 * can be reopened as a side-by-side PDF/detection comparison.
 */
export const DigitiseIntakeButton: React.FC<DigitiseIntakeButtonProps> = ({ onExtracted }) => {
  const { t } = useTranslation();
  const { user } = useAuth();
  const toast = useToast();
  const inputRef = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [sourceFile, setSourceFile] = useState<File | null>(null);
  const [result, setResult] = useState<IntakeDigitisationResult | null>(null);
  const [showPreview, setShowPreview] = useState(false);

  if (!isAdmin(user) && !hasRole(user, 'Digitiser')) {
    return null;
  }

  const handleFile = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;

    const formData = new FormData();
    formData.append('file', file, file.name);
    setBusy(true);
    setResult(null);
    setSourceFile(null);
    try {
      const extraction = await apiClient.postFormData<IntakeDigitisationResult>(
        '/api/ai/intake/extract',
        formData
      );
      setResult(extraction);
      setSourceFile(file);

      if (!extraction.isIntakeForm) {
        toast.error(t('intakeDigitise.notIntake'));
        return;
      }

      onExtracted(extraction.fields);
      const hasWarnings =
        extraction.warnings.includes('handwriting_review') ||
        extraction.warnings.includes('vision_model_failed');
      if (hasWarnings) {
        toast.info(t('intakeDigitise.reviewWarning'));
      } else {
        toast.success(t('intakeDigitise.success'));
      }
    } catch (error) {
      console.error('Intake digitisation failed', error);
      toast.error(error instanceof ApiError ? error.message : t('intakeDigitise.failed'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="digitise-intake">
      <input
        ref={inputRef}
        type="file"
        accept="application/pdf,.pdf"
        hidden
        onChange={(event) => void handleFile(event)}
      />
      <div>
        <strong>{t('intakeDigitise.title')}</strong>
        <p>{t('intakeDigitise.description')}</p>
      </div>
      <div className="digitise-intake-actions">
        <button
          type="button"
          className="btn btn-secondary"
          disabled={busy}
          onClick={() => inputRef.current?.click()}
        >
          {busy ? t('intakeDigitise.uploading') : t('intakeDigitise.button')}
        </button>
        {result && sourceFile && (
          <button type="button" className="btn btn-secondary" onClick={() => setShowPreview(true)}>
            {t('intakeDigitise.previewButton')}
          </button>
        )}
      </div>

      {showPreview && result && sourceFile && (
        <IntakeDigitisePreviewModal
          file={sourceFile}
          result={result}
          onClose={() => setShowPreview(false)}
        />
      )}
    </div>
  );
};

export default DigitiseIntakeButton;
