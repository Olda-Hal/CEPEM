import React from 'react';
import { useTranslation } from 'react-i18next';
import { PatientDetail } from '../../types';
import { formatDateTime, formatFileSize } from '../../utils/format';

interface PatientDocumentsSectionProps {
  documents: PatientDetail['documents'];
  uploading: boolean;
  onAdd: () => void;
  onDocumentView: (documentId: number) => void;
  onDocumentDelete: (documentId: number) => void;
}

/** The patient documents card on the detail page. */
export const PatientDocumentsSection: React.FC<PatientDocumentsSectionProps> = ({
  documents,
  uploading,
  onAdd,
  onDocumentView,
  onDocumentDelete,
}) => {
  const { t, i18n } = useTranslation();

  return (
    <div className="patient-documents-card">
      <div className="card-header">
        <h2>{t('patients.documents.title')} ({documents.length})</h2>
        <button
          className="add-document-btn"
          onClick={onAdd}
          disabled={uploading}
        >
          {t('patients.documents.addDocument')}
        </button>
      </div>

      {documents.length === 0 ? (
        <div className="no-data">
          <p>{t('patients.documents.noDocuments')}</p>
        </div>
      ) : (
        <div className="documents-list">
          {documents.map((document) => (
            <div key={document.id} className="document-item">
              <div
                className="document-info"
                onClick={() => onDocumentView(document.id)}
              >
                <svg viewBox="0 0 24 24" width="24" height="24" className="document-icon">
                  <path d="M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zM6 20V4h7v5h5v11H6z"/>
                </svg>
                <div className="document-details">
                  <div className="document-name">{document.fileName}</div>
                  <div className="document-meta">
                    <span className="document-date">{formatDateTime(document.uploadedAt, i18n.language)}</span>
                    <span className="document-size">{formatFileSize(document.fileSize)}</span>
                  </div>
                </div>
              </div>
              <button
                className="delete-document-btn"
                onClick={() => onDocumentDelete(document.id)}
                title={t('patients.documents.deleteDocument')}
              >
                <svg viewBox="0 0 24 24" width="20" height="20">
                  <path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"/>
                </svg>
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

export default PatientDocumentsSection;
