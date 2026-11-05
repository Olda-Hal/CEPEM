import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import BaseModal from '../BaseModal';
import { apiClient, ApiError } from '../../utils/api';
import {
  Patient,
  PatientMergePreview,
  PatientMergeResult,
  PatientSearchResponse
} from '../../types';
import './MergePatientModal.css';

interface MergePatientModalProps {
  isOpen: boolean;
  sourcePatientId: number;
  sourcePatientName: string;
  onClose: () => void;
  onMerged: (targetPatientId: number) => void;
}

/** Admin tool: move all records from a duplicate patient into the record kept. */
export const MergePatientModal: React.FC<MergePatientModalProps> = ({
  isOpen,
  sourcePatientId,
  sourcePatientName,
  onClose,
  onMerged
}) => {
  const { t, i18n } = useTranslation();
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<Patient[]>([]);
  const [searching, setSearching] = useState(false);
  const [target, setTarget] = useState<Patient | null>(null);
  const [preview, setPreview] = useState<PatientMergePreview | null>(null);
  const [loadingPreview, setLoadingPreview] = useState(false);
  const [merging, setMerging] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isOpen) {
      setQuery('');
      setResults([]);
      setTarget(null);
      setPreview(null);
      setError(null);
    }
  }, [isOpen]);

  useEffect(() => {
    const term = query.trim();
    if (!isOpen || term.length < 2) {
      setResults([]);
      return;
    }

    let cancelled = false;
    const timer = window.setTimeout(async () => {
      setSearching(true);
      try {
        const params = new URLSearchParams({
          page: '0',
          limit: '10',
          search: term,
          sortBy: 'lastName',
          sortOrder: 'asc'
        });
        const response = await apiClient.get<PatientSearchResponse>(
          `/api/patients/search?${params}`
        );
        if (!cancelled) {
          setResults(response.patients.filter((patient) => patient.id !== sourcePatientId));
        }
      } catch (searchError) {
        console.error('Patient search failed', searchError);
        if (!cancelled) {
          setResults([]);
        }
      } finally {
        if (!cancelled) {
          setSearching(false);
        }
      }
    }, 300);

    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [isOpen, query, sourcePatientId]);

  const selectTarget = async (patient: Patient) => {
    setTarget(patient);
    setResults([]);
    setQuery('');
    setPreview(null);
    setError(null);
    setLoadingPreview(true);
    try {
      setPreview(
        await apiClient.get<PatientMergePreview>(
          `/api/patients/merge/preview?sourcePatientId=${sourcePatientId}&targetPatientId=${patient.id}`
        )
      );
    } catch (previewError) {
      console.error('Merge preview failed', previewError);
      setError(t('patients.merge.previewFailed'));
    } finally {
      setLoadingPreview(false);
    }
  };

  const handleMerge = async () => {
    if (!target) return;
    setMerging(true);
    setError(null);
    try {
      const result = await apiClient.post<PatientMergeResult>('/api/patients/merge', {
        sourcePatientId,
        targetPatientId: target.id
      });
      onMerged(result.targetPatientId);
    } catch (mergeError) {
      console.error('Merge failed', mergeError);
      setError(mergeError instanceof ApiError ? mergeError.message : t('patients.merge.failed'));
    } finally {
      setMerging(false);
    }
  };

  const formatDate = (value: string) => new Date(value).toLocaleDateString(i18n.language);

  return (
    <BaseModal
      title={t('patients.merge.title')}
      onClose={onClose}
      contentClassName="merge-patient-modal"
    >
      <p className="merge-patient-warning">
        {t('patients.merge.description', { name: sourcePatientName })}
      </p>

      {error && <div className="error-message">{error}</div>}

      <label className="merge-patient-search">
        <span>{t('patients.merge.searchTarget')}</span>
        <input
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder={t('patients.merge.searchPlaceholder')}
          autoFocus
        />
      </label>

      {searching && <p className="merge-patient-hint">{t('common.loading')}</p>}
      {!searching && query.trim().length >= 2 && results.length === 0 && (
        <p className="merge-patient-hint">{t('patients.merge.noPatients')}</p>
      )}

      {results.length > 0 && (
        <ul className="merge-patient-results">
          {results.map((patient) => (
            <li key={patient.id}>
              <button type="button" onClick={() => void selectTarget(patient)}>
                <strong>{patient.fullName}</strong>
                <span>
                  UID {patient.uid} · {formatDate(patient.birthDate)}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}

      {target && (
        <div className="merge-patient-target">
          <p>
            {t('patients.merge.targetSelected')}: <strong>{target.fullName}</strong> (
            {t('patients.fields.uid')} {target.uid})
          </p>

          {loadingPreview && <p>{t('common.loading')}</p>}
          {preview && (
            <div className="merge-patient-preview">
              <p>{t('patients.merge.previewDescription')}</p>
              <ul>
                <li>{t('patients.merge.counts.events', { count: preview.events })}</li>
                <li>{t('patients.merge.counts.visits', { count: preview.visits })}</li>
                <li>{t('patients.merge.counts.documents', { count: preview.documents })}</li>
                <li>
                  {t('patients.merge.counts.formSubmissions', {
                    count: preview.formSubmissions
                  })}
                </li>
                <li>{t('patients.merge.counts.consents', { count: preview.consents })}</li>
                <li>
                  {t('patients.merge.counts.evaluationCases', {
                    count: preview.evaluationCases
                  })}
                </li>
                <li>
                  {t('patients.merge.counts.reservationSlots', {
                    count: preview.reservationSlots
                  })}
                </li>
                <li>{t('patients.merge.counts.emailLogs', { count: preview.emailLogs })}</li>
              </ul>
            </div>
          )}
        </div>
      )}

      <div className="modal-footer">
        <button type="button" className="btn btn-secondary" onClick={onClose} disabled={merging}>
          {t('common.cancel')}
        </button>
        <button
          type="button"
          className="btn btn-danger"
          onClick={() => void handleMerge()}
          disabled={!preview || merging}
        >
          {merging ? t('common.saving') : t('patients.merge.confirm')}
        </button>
      </div>
    </BaseModal>
  );
};

export default MergePatientModal;
