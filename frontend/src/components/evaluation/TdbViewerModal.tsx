import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import BaseModal from '../BaseModal';
import { apiClient } from '../../utils/api';
import { TdbMeasurement } from '../../types';
import TdbSliceCard from './TdbSliceCard';
import './EvaluationReportModal.css';
import '../../pages/EvaluationPage.css';

interface TdbViewerModalProps {
  patientId: number;
  documentId: number;
  fileName: string;
  onClose: () => void;
}

/**
 * Read-only view of a raw .tdb measurement attached to a visit. The backend
 * parses the file into the seven display planes; the browser renders them with
 * the same MEIK-style pipeline as Vyhodnocování.
 */
export const TdbViewerModal: React.FC<TdbViewerModalProps> = ({
  patientId,
  documentId,
  fileName,
  onClose
}) => {
  const { t } = useTranslation();
  const [measurement, setMeasurement] = useState<TdbMeasurement | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const data = await apiClient.get<TdbMeasurement>(
          `/api/patients/${patientId}/documents/${documentId}/tdb`
        );
        if (!cancelled) {
          setMeasurement(data);
        }
      } catch (err) {
        console.error('Failed to load the .tdb measurement', err);
        if (!cancelled) {
          setError(t('visits.tdbViewFailed'));
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };
    void load();
    return () => {
      cancelled = true;
    };
  }, [documentId, patientId, t]);

  return (
    <BaseModal
      title={t('visits.tdbViewerTitle', { fileName })}
      onClose={onClose}
      contentClassName="evaluation-report-modal"
    >
      <div className="evaluation-report-body">
        {loading ? (
          <p>{t('common.loading')}</p>
        ) : error || !measurement ? (
          <div className="alert alert-error">{error ?? t('visits.tdbViewFailed')}</div>
        ) : (
          <>
            <div className="tdb-meta">
              <span className="tdb-pill">
                {t('visits.tdbLayers', { count: measurement.slices.length })}
              </span>
              <span className="muted small">
                {t('visits.tdbNoise', { value: measurement.noiseLevel.toFixed(2) })}
              </span>
            </div>

            <div className="tdb-scan-row">
              {measurement.slices.map((slice) => (
                <TdbSliceCard
                  key={slice.index}
                  slice={slice}
                  measurement={measurement}
                  markers={[]}
                  readonly
                  onAddMarker={() => undefined}
                />
              ))}
            </div>

            <p className="muted small">{t('visits.tdbReadonlyHint')}</p>
          </>
        )}
      </div>
    </BaseModal>
  );
};

export default TdbViewerModal;
