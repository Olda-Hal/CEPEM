import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { apiClient, ApiError } from '../utils/api';
import { EvaluationCase } from '../types';
import TdbSliceCard, { SliceMarker } from '../components/evaluation/TdbSliceCard';
import './EvaluationPage.css';

interface Marker extends SliceMarker {
  slice: number;
}

const EvaluationPage: React.FC = () => {
  const { t } = useTranslation();
  const [current, setCurrent] = useState<EvaluationCase | null>(null);
  const [markers, setMarkers] = useState<Marker[]>([]);
  const [recommendation, setRecommendation] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [allDone, setAllDone] = useState(false);

  const loadNext = useCallback(async () => {
    setLoading(true);
    setError(null);
    setAllDone(false);
    try {
      const next = await apiClient.get<EvaluationCase | null>('/api/evaluations/next');
      if (!next) {
        setCurrent(null);
        setMarkers([]);
        setRecommendation('');
        setAllDone(true);
        return;
      }
      setCurrent(next);
      setMarkers([]);
      setRecommendation('');
    } catch (err) {
      console.error('Failed to load the next evaluation measurement', err);
      setError(t('evaluation.loadFailed'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void loadNext();
  }, [loadNext]);

  const addMarker = (slice: number, x: number, y: number) => {
    if (!current) return;
    setMarkers((prev) => [...prev, { slice, x, y, note: '' }]);
  };

  const updateMarkerNote = (index: number, note: string) => {
    setMarkers((prev) => prev.map((marker, i) => (i === index ? { ...marker, note } : marker)));
  };

  const removeMarker = (index: number) => {
    setMarkers((prev) => prev.filter((_, i) => i !== index));
  };

  const handleEvaluate = async (outcome: 'OK' | 'Findings') => {
    if (!current) return;

    if (outcome === 'OK' && markers.length > 0) {
      if (!window.confirm(t('evaluation.okConfirmWithMarkers'))) {
        return;
      }
    }

    if (outcome === 'Findings' && markers.length === 0 && !recommendation.trim()) {
      setError(t('evaluation.needFindings'));
      return;
    }

    setSaving(true);
    setError(null);
    try {
      await apiClient.post(`/api/evaluations/cases/${current.id}/evaluate`, {
        annotationsJson: JSON.stringify(markers),
        recommendation: recommendation || null,
        outcome
      });
      await loadNext();
    } catch (err) {
      console.error('Failed to save evaluation', err);
      const message =
        err instanceof ApiError && err.message ? err.message : t('evaluation.saveFailed');
      setError(message);
      // A conflict means this measurement is no longer available to us.
      if (err instanceof ApiError && err.status === 409) {
        await loadNext();
        setError(message);
      }
    } finally {
      setSaving(false);
    }
  };

  const sideLabel = (side?: string | null): string | null => {
    if (side === 'Left') return t('evaluation.side.left');
    if (side === 'Right') return t('evaluation.side.right');
    return null;
  };

  const postureLabel = (posture?: string | null): string | null => {
    if (posture === 'Lying') return t('evaluation.posture.lying');
    if (posture === 'Standing') return t('evaluation.posture.standing');
    if (posture === 'Sitting') return t('evaluation.posture.sitting');
    return null;
  };

  const slices = current?.tdb?.slices ?? [];

  return (
    <div className="evaluation-page">
      <h1 className="page-title">{t('evaluation.title')}</h1>

      {error && <div className="alert alert-error">{error}</div>}

      {loading ? (
        <div>{t('common.loading')}</div>
      ) : allDone || !current ? (
        <div className="evaluation-empty">
          <p>{t('evaluation.allDone')}</p>
          <button className="btn btn-secondary" onClick={() => void loadNext()}>
            {t('evaluation.tryAgain')}
          </button>
        </div>
      ) : (
        <>
          <div className="tdb-meta">
            <span className="tdb-pill">
              {sideLabel(current.side) ?? t('evaluation.side.unknown')}
            </span>
            <span className="tdb-pill">
              {postureLabel(current.posture) ?? t('evaluation.posture.unknown')}
            </span>
          </div>

          {slices.length > 0 ? (
            <div className="tdb-scan-row">
              {slices.map((slice) => (
                <TdbSliceCard
                  key={slice.index}
                  slice={slice}
                  measurement={current.tdb!}
                  markers={markers
                    .map((marker, index) => ({ ...marker, number: index + 1 }))
                    .filter((marker) => marker.slice === slice.index)}
                  readonly={saving}
                  onAddMarker={(x, y) => addMarker(slice.index, x, y)}
                />
              ))}
            </div>
          ) : (
            <p className="muted">{t('evaluation.loadFailed')}</p>
          )}

          <p className="muted small">{t('evaluation.markHintSlice')}</p>

          {markers.length > 0 && (
            <ul className="evaluation-markers">
              {markers.map((marker, index) => (
                <li key={index}>
                  <span className="marker-index">
                    #{index + 1} · {t('evaluation.slice', { index: marker.slice + 1 })}
                  </span>
                  <input
                    type="text"
                    value={marker.note}
                    placeholder={t('evaluation.markerNotePlaceholder')}
                    onChange={(event) => updateMarkerNote(index, event.target.value)}
                    disabled={saving}
                  />
                  <button
                    className="btn btn-sm btn-danger"
                    onClick={() => removeMarker(index)}
                    disabled={saving}
                  >
                    {t('evaluation.removeMarker')}
                  </button>
                </li>
              ))}
            </ul>
          )}

          <label className="evaluation-recommendation-label" htmlFor="recommendation">
            {t('evaluation.recommendation')}
          </label>
          <textarea
            id="recommendation"
            rows={3}
            value={recommendation}
            onChange={(event) => setRecommendation(event.target.value)}
            disabled={saving}
            placeholder={t('evaluation.recommendationPlaceholder')}
          />

          <div className="tdb-actions">
            <button
              className="btn btn-secondary"
              onClick={() => void handleEvaluate('OK')}
              disabled={saving}
            >
              {t('evaluation.markOk')}
            </button>
            <button
              className="btn btn-primary"
              onClick={() => void handleEvaluate('Findings')}
              disabled={saving}
            >
              {saving ? t('evaluation.saving') : t('evaluation.finishFindings')}
            </button>
          </div>
        </>
      )}
    </div>
  );
};

export default EvaluationPage;
