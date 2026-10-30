import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { apiClient } from '../utils/api';
import { TdbMeasurement } from '../types';
import TdbSliceCard, { SliceMarker } from '../components/evaluation/TdbSliceCard';
import './EvaluationPage.css';

interface EvaluationCase {
  id: number;
  patientId: number | null;
  patientName: string | null;
  title: string;
  imageKey: string;
  annotationsJson: string | null;
  recommendation: string | null;
  outcome?: string | null;
  side?: string | null;
  posture?: string | null;
  conditions?: string | null;
  tdb?: TdbMeasurement | null;
  evaluatedAtUtc: string | null;
  isEvaluated: boolean;
}

interface Marker extends SliceMarker {
  slice: number;
}

const readMarkers = (json: string | null): Marker[] => {
  if (!json) return [];
  try {
    const parsed = JSON.parse(json) as Partial<Marker>[];
    if (!Array.isArray(parsed)) return [];
    return parsed
      .filter((item) => typeof item?.x === 'number' && typeof item?.y === 'number')
      .map((item) => ({
        slice: typeof item.slice === 'number' ? item.slice : 0,
        x: Number(item.x),
        y: Number(item.y),
        note: typeof item.note === 'string' ? item.note : ''
      }));
  } catch {
    return [];
  }
};

/** Placeholder renderings for the seeded mock image keys. */
const MockImage: React.FC<{ imageKey: string }> = ({ imageKey }) => {
  if (imageKey === 'hand') {
    return (
      <svg viewBox="0 0 300 220" role="img" aria-label="RTG ruky" className="mock-image">
        <rect width="300" height="220" fill="#0b1a26" rx="8" />
        <g stroke="#9fb8c9" strokeWidth="5" strokeLinecap="round" fill="none">
          <path d="M60 200 L80 150 L95 105" />
          <path d="M95 105 L60 60" />
          <path d="M95 105 L95 45" />
          <path d="M95 105 L130 50" />
          <path d="M95 105 L160 70" />
          <path d="M80 150 L120 185" />
        </g>
      </svg>
    );
  }
  return (
    <svg viewBox="0 0 300 220" role="img" aria-label="RTG hrudníku" className="mock-image">
      <rect width="300" height="220" fill="#0b1a26" rx="8" />
      <ellipse cx="150" cy="110" rx="55" ry="85" fill="#13293a" />
      <ellipse cx="150" cy="110" rx="14" ry="80" fill="#1d3d52" />
      <g stroke="#9fb8c9" strokeWidth="6" fill="none" strokeLinecap="round">
        {[0, 1, 2, 3].map((i) => (
          <path key={`l${i}`} d={`M105 ${70 + i * 25} Q 105 ${85 + i * 25} 120 ${90 + i * 25}`} />
        ))}
        {[0, 1, 2, 3].map((i) => (
          <path key={`r${i}`} d={`M195 ${70 + i * 25} Q 195 ${85 + i * 25} 180 ${90 + i * 25}`} />
        ))}
      </g>
    </svg>
  );
};

const EvaluationPage: React.FC = () => {
  const { t } = useTranslation();
  const [cases, setCases] = useState<EvaluationCase[]>([]);
  const [selected, setSelected] = useState<EvaluationCase | null>(null);
  const [markers, setMarkers] = useState<Marker[]>([]);
  const [recommendation, setRecommendation] = useState('');
  const [loading, setLoading] = useState(true);
  const [loadingCase, setLoadingCase] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const all = await apiClient.get<EvaluationCase[]>(
          '/api/evaluations/cases?includeEvaluated=true'
        );
        setCases(all);
      } catch (err) {
        console.error('Failed to load evaluation cases', err);
        setError(t('evaluation.loadFailed'));
      } finally {
        setLoading(false);
      }
    };
    void load();
  }, [t]);

  const openCases = useMemo(() => cases.filter((c) => !c.isEvaluated), [cases]);
  const doneCases = useMemo(() => cases.filter((c) => c.isEvaluated), [cases]);

  const selectCase = async (c: EvaluationCase) => {
    setSelected(c);
    setMarkers(readMarkers(c.annotationsJson));
    setRecommendation(c.recommendation ?? '');
    setSuccess(null);
    setError(null);

    if (c.imageKey === 'tdb' && !c.tdb) {
      setLoadingCase(true);
      try {
        const detail = await apiClient.get<EvaluationCase>(`/api/evaluations/cases/${c.id}`);
        setSelected(detail);
        setMarkers(readMarkers(detail.annotationsJson));
        setRecommendation(detail.recommendation ?? '');
      } catch (err) {
        console.error('Failed to load evaluation case detail', err);
        setError(t('evaluation.loadFailed'));
      } finally {
        setLoadingCase(false);
      }
    }
  };

  const addMarker = (slice: number, x: number, y: number) => {
    if (!selected || selected.isEvaluated) return;
    setMarkers((prev) => [...prev, { slice, x, y, note: '' }]);
  };

  const addMockMarker = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!selected || selected.isEvaluated) return;
    const rect = (e.currentTarget as HTMLDivElement).getBoundingClientRect();
    const x = (e.clientX - rect.left) / rect.width;
    const y = (e.clientY - rect.top) / rect.height;
    addMarker(0, Number(x.toFixed(3)), Number(y.toFixed(3)));
  };

  const updateMarkerNote = (index: number, note: string) => {
    setMarkers((prev) => prev.map((m, i) => (i === index ? { ...m, note } : m)));
  };

  const removeMarker = (index: number) => {
    setMarkers((prev) => prev.filter((_, i) => i !== index));
  };

  const handleEvaluate = async (outcome: 'OK' | 'Findings' | null) => {
    if (!selected) return;

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
    setSuccess(null);
    try {
      const saved = await apiClient.post<EvaluationCase>(
        `/api/evaluations/cases/${selected.id}/evaluate`,
        {
          annotationsJson: JSON.stringify(markers),
          recommendation: recommendation || null,
          ...(outcome ? { outcome } : {})
        }
      );
      setCases((prev) => prev.map((c) => (c.id === saved.id ? saved : c)));
      setSelected(saved);
      setMarkers(readMarkers(saved.annotationsJson));
      setSuccess(t('evaluation.saved'));
    } catch (err) {
      console.error('Failed to save evaluation', err);
      setError(t('evaluation.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  const caseSubtitle = (c: EvaluationCase): string => {
    if (c.imageKey === 'tdb') {
      const parts = [sideLabel(c.side), postureLabel(c.posture)].filter((part): part is string =>
        Boolean(part)
      );
      return parts.length > 0 ? parts.join(' · ') : t('evaluation.measurement');
    }
    return c.patientName ?? t('evaluation.noPatient');
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

  if (loading) {
    return (
      <div className="evaluation-page">
        <h1 className="page-title">{t('evaluation.title')}</h1>
        <div>{t('common.loading')}</div>
      </div>
    );
  }

  const tdb = selected?.imageKey === 'tdb' ? selected.tdb : null;
  const isTdb = Boolean(tdb);

  return (
    <div className="evaluation-page">
      <h1 className="page-title">{t('evaluation.title')}</h1>
      <p className="evaluation-subtitle">{t('evaluation.subtitle')}</p>

      {error && <div className="alert alert-error">{error}</div>}
      {success && <div className="alert alert-success">{success}</div>}

      <div className="evaluation-layout">
        <aside className="evaluation-list">
          <h3>
            {t('evaluation.open')} ({openCases.length})
          </h3>
          {openCases.length === 0 && <p className="muted">{t('evaluation.noneOpen')}</p>}
          {openCases.map((c) => (
            <button
              key={c.id}
              className={`evaluation-card${selected?.id === c.id ? ' active' : ''}`}
              onClick={() => void selectCase(c)}
            >
              <strong>{c.title}</strong>
              <span className="muted">{caseSubtitle(c)}</span>
            </button>
          ))}

          {doneCases.length > 0 && (
            <>
              <h3>
                {t('evaluation.done')} ({doneCases.length})
              </h3>
              {doneCases.map((c) => (
                <button
                  key={c.id}
                  className={`evaluation-card done${selected?.id === c.id ? ' active' : ''}`}
                  onClick={() => void selectCase(c)}
                >
                  <strong>
                    {c.title}
                    {c.outcome && (
                      <span
                        className={`tdb-outcome-badge ${c.outcome === 'OK' ? 'ok' : 'findings'}`}
                      >
                        {c.outcome === 'OK'
                          ? t('evaluation.outcomeOk')
                          : t('evaluation.outcomeFindings')}
                      </span>
                    )}
                  </strong>
                  <span className="muted">{caseSubtitle(c)}</span>
                </button>
              ))}
            </>
          )}
        </aside>

        <section className="evaluation-detail">
          {!selected ? (
            <p className="muted">{t('evaluation.pickCase')}</p>
          ) : loadingCase ? (
            <p className="muted">{t('common.loading')}</p>
          ) : isTdb && tdb ? (
            <>
              <h2>{selected.title}</h2>
              <div className="tdb-meta">
                <span className="tdb-pill">
                  {sideLabel(selected.side) ?? t('evaluation.side.unknown')}
                </span>
                <span className="tdb-pill">
                  {postureLabel(selected.posture) ?? t('evaluation.posture.unknown')}
                </span>
                {selected.conditions && (
                  <span className="muted small">
                    {t('evaluation.conditions')}: {selected.conditions}
                  </span>
                )}
              </div>
              <p className="muted small">{t('evaluation.anonymisedHint')}</p>

              <div className="tdb-slice-grid">
                {tdb.slices.map((slice) => (
                  <TdbSliceCard
                    key={slice.index}
                    slice={slice}
                    measurement={tdb}
                    markers={markers
                      .map((marker, index) => ({ ...marker, number: index + 1 }))
                      .filter((marker) => marker.slice === slice.index)}
                    readonly={selected.isEvaluated}
                    onAddMarker={(x, y) => addMarker(slice.index, x, y)}
                  />
                ))}
              </div>

              <p className="muted small">{t('evaluation.markHintSlice')}</p>

              {markers.length > 0 && (
                <ul className="evaluation-markers">
                  {markers.map((m, i) => (
                    <li key={i}>
                      <span className="marker-index">
                        #{i + 1} · {t('evaluation.slice', { index: m.slice + 1 })}
                      </span>
                      <input
                        type="text"
                        value={m.note}
                        placeholder={t('evaluation.markerNotePlaceholder')}
                        onChange={(e) => updateMarkerNote(i, e.target.value)}
                        disabled={selected.isEvaluated}
                      />
                      {!selected.isEvaluated && (
                        <button className="btn btn-sm btn-danger" onClick={() => removeMarker(i)}>
                          {t('evaluation.removeMarker')}
                        </button>
                      )}
                    </li>
                  ))}
                </ul>
              )}

              <label className="evaluation-recommendation-label" htmlFor="recommendation">
                {t('evaluation.recommendation')}
              </label>
              <textarea
                id="recommendation"
                rows={4}
                value={recommendation}
                onChange={(e) => setRecommendation(e.target.value)}
                disabled={selected.isEvaluated}
                placeholder={t('evaluation.recommendationPlaceholder')}
              />

              {selected.isEvaluated ? (
                <>
                  <p className={`tdb-outcome ${selected.outcome === 'OK' ? 'ok' : 'findings'}`}>
                    {selected.outcome === 'OK'
                      ? t('evaluation.outcomeOkLong')
                      : t('evaluation.outcomeFindingsLong')}
                  </p>
                  <p className="muted small">
                    {t('evaluation.evaluatedAt', {
                      date: new Date(selected.evaluatedAtUtc ?? '').toLocaleString()
                    })}
                  </p>
                </>
              ) : (
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
              )}
            </>
          ) : (
            <>
              <h2>{selected.title}</h2>
              <p className="muted">{selected.patientName ?? t('evaluation.noPatient')}</p>

              <div
                className={`evaluation-canvas${selected.isEvaluated ? ' readonly' : ''}`}
                onClick={addMockMarker}
                role="presentation"
              >
                <MockImage imageKey={selected.imageKey} />
                {markers.map((m, i) => (
                  <span
                    key={i}
                    className="evaluation-marker"
                    style={{ left: `${m.x * 100}%`, top: `${m.y * 100}%` }}
                    title={m.note || `#${i + 1}`}
                  >
                    {i + 1}
                  </span>
                ))}
              </div>
              <p className="muted small">{t('evaluation.markHint')}</p>

              {markers.length > 0 && (
                <ul className="evaluation-markers">
                  {markers.map((m, i) => (
                    <li key={i}>
                      <span className="marker-index">#{i + 1}</span>
                      <input
                        type="text"
                        value={m.note}
                        placeholder={t('evaluation.markerNotePlaceholder')}
                        onChange={(e) => updateMarkerNote(i, e.target.value)}
                        disabled={selected.isEvaluated}
                      />
                      {!selected.isEvaluated && (
                        <button className="btn btn-sm btn-danger" onClick={() => removeMarker(i)}>
                          {t('evaluation.removeMarker')}
                        </button>
                      )}
                    </li>
                  ))}
                </ul>
              )}

              <label className="evaluation-recommendation-label" htmlFor="recommendation">
                {t('evaluation.recommendation')}
              </label>
              <textarea
                id="recommendation"
                rows={4}
                value={recommendation}
                onChange={(e) => setRecommendation(e.target.value)}
                disabled={selected.isEvaluated}
                placeholder={t('evaluation.recommendationPlaceholder')}
              />

              {selected.isEvaluated ? (
                <p className="muted small">
                  {t('evaluation.evaluatedAt', {
                    date: new Date(selected.evaluatedAtUtc ?? '').toLocaleString()
                  })}
                </p>
              ) : (
                <button
                  className="btn btn-primary"
                  onClick={() => void handleEvaluate(null)}
                  disabled={saving}
                >
                  {saving ? t('evaluation.saving') : t('evaluation.evaluate')}
                </button>
              )}
            </>
          )}
        </section>
      </div>
    </div>
  );
};

export default EvaluationPage;
