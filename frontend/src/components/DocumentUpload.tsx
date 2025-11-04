import React, { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import './DocumentUpload.css';

interface DocumentUploadProps {
  onUpload: (file: File) => void;
  onCancel: () => void;
}

const MAX_FILE_SIZE_MB = 10;
const MAX_FILE_SIZE_BYTES = MAX_FILE_SIZE_MB * 1024 * 1024;

export const DocumentUpload: React.FC<DocumentUploadProps> = ({ onUpload, onCancel }) => {
  const { t } = useTranslation();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [uploadSource, setUploadSource] = useState<'file' | 'scanner' | null>(null);

  const handleFileSelect = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    validateAndSetFile(file);
  };

  const validateAndSetFile = (file: File) => {
    setError(null);

    if (file.type !== 'application/pdf') {
      setError(t('patients.documents.errors.mustBePdf'));
      return;
    }

    if (file.size > MAX_FILE_SIZE_BYTES) {
      setError(t('patients.documents.errors.fileTooLarge', { maxSize: MAX_FILE_SIZE_MB }));
      return;
    }

    setSelectedFile(file);
  };

  const handleScanDocument = async () => {
    try {
      // Check if Web TWAIN or scanner API is available
      // For now, we'll simulate scanner support with file picker
      // In production, integrate with a scanner library like Dynamsoft Web TWAIN
      if (fileInputRef.current) {
        fileInputRef.current.click();
        setUploadSource('scanner');
      }
    } catch (err) {
      console.error('Error accessing scanner:', err);
      setError(t('patients.documents.errors.scannerNotAvailable'));
    }
  };

  const handleFileUpload = () => {
    if (fileInputRef.current) {
      fileInputRef.current.click();
      setUploadSource('file');
    }
  };

  const handleConfirmUpload = () => {
    if (selectedFile) {
      onUpload(selectedFile);
    }
  };

  const formatFileSize = (bytes: number): string => {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(2) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
  };

  return (
    <div className="document-upload-overlay">
      <div className="document-upload-modal">
        <div className="modal-header">
          <h2>{t('patients.documents.uploadDocument')}</h2>
          <button className="close-button" onClick={onCancel}>
            <svg viewBox="0 0 24 24" width="24" height="24">
              <path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/>
            </svg>
          </button>
        </div>

        <div className="modal-content">
          {error && (
            <div className="error-message">
              <svg viewBox="0 0 24 24" width="20" height="20">
                <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z"/>
              </svg>
              {error}
            </div>
          )}

          {!selectedFile ? (
            <div className="upload-options">
              <div className="upload-option-description">
                <p>{t('patients.documents.selectUploadMethod')}</p>
                <p className="file-limits">
                  {t('patients.documents.fileRequirements', { maxSize: MAX_FILE_SIZE_MB })}
                </p>
              </div>

              <div className="upload-buttons">
                <button className="upload-option-button" onClick={handleScanDocument}>
                  <svg viewBox="0 0 24 24" width="48" height="48">
                    <path d="M19.8 10.7L4.2 5l-.7 1.9L17.6 12H5c-1.1 0-2 .9-2 2v4c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2v-5.5c0-.8-.5-1.6-1.2-1.8zM19 18H5v-4h14v4zM6 15h2v2H6v-2z"/>
                  </svg>
                  <span>{t('patients.documents.scanDocument')}</span>
                </button>

                <button className="upload-option-button" onClick={handleFileUpload}>
                  <svg viewBox="0 0 24 24" width="48" height="48">
                    <path d="M9 16h6v-6h4l-7-7-7 7h4zm-4 2h14v2H5z"/>
                  </svg>
                  <span>{t('patients.documents.uploadFile')}</span>
                </button>
              </div>

              <input
                ref={fileInputRef}
                type="file"
                accept="application/pdf"
                onChange={handleFileSelect}
                style={{ display: 'none' }}
              />
            </div>
          ) : (
            <div className="file-preview">
              <div className="file-info">
                <svg viewBox="0 0 24 24" width="48" height="48" className="pdf-icon">
                  <path d="M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zM6 20V4h7v5h5v11H6z"/>
                </svg>
                <div className="file-details">
                  <h3>{selectedFile.name}</h3>
                  <p>{formatFileSize(selectedFile.size)}</p>
                  <p className="upload-method">
                    {uploadSource === 'scanner' 
                      ? t('patients.documents.scannedDocument') 
                      : t('patients.documents.uploadedFile')}
                  </p>
                </div>
              </div>

              <div className="preview-actions">
                <button className="button-secondary" onClick={() => setSelectedFile(null)}>
                  {t('common.cancel')}
                </button>
                <button className="button-primary" onClick={handleConfirmUpload}>
                  {t('patients.documents.confirmUpload')}
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
