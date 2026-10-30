import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { apiClient } from '../utils/api';
import './EvaluationPage.css';

interface EvaluationCase {
  id: number;
  patientId: number | null;
  patientName: string | null;
  title: string;
  imageKey: string;
  annotationsJson: string | null;
  recommendation: string | null;
  evaluatedAtUtc: string | null;
  isEvaluated: boolean;
}

interface Marker {
  x: number;
  y: number;
  note: string;
}

const readMarkers = (json: string | null): Marker[] => {
  if (!json) return [];
  try {
    const parsed = JSON.parse(json) as Marker[];
    return Array.isArray(parsed) ? parsed : [];
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

  const selectCase = (c: EvaluationCase) => {
    setSelected(c);
    setMarkers(readMarkers(c.annotationsJson));
    setRecommendation(c.recommendation ?? '');
    setSuccess(null);
    setError(null);
  };

  const addMarker = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!selected || selected.isEvaluated) return;
    const rect = (e.currentTarget as HTMLDivElement).getBoundingClientRect();
    const x = (e.clientX - rect.left) / rect.width;
    const y = (e.clientY - rect.top) / rect.height;
    setMarkers((prev) => [...prev, { x: Number(x.toFixed(3)), y: Number(y.toFixed(3)), note: '' }]);
  };

  const updateMarkerNote = (index: number, note: string) => {
    setMarkers((prev) => prev.map((m, i) => (i === index ? { ...m, note } : m)));
  };

  const removeMarker = (index: number) => {
    setMarkers((prev) => prev.filter((_, i) => i !== index));
  };

  const handleEvaluate = async () => {
    if (!selected) return;
    setSaving(true);
    setError(null);
    setSuccess(null);
    try {
      const saved = await apiClient.post<EvaluationCase>(
        `/api/evaluations/cases/${selected.id}/evaluate`,
        {
          annotationsJson: JSON.stringify(markers),
          recommendation: recommendation || null
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

  if (loading) {
    return (
      <div className="evaluation-page">
        <h1 className="page-title">{t('evaluation.title')}</h1>
        <div>{t('common.loading')}</div>
      </div>
    );
  }

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
              onClick={() => selectCase(c)}
            >
              <strong>{c.title}</strong>
              <span className="muted">{c.patientName ?? t('evaluation.noPatient')}</span>
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
                  onClick={() => selectCase(c)}
                >
                  <strong>{c.title}</strong>
                  <span className="muted">{c.patientName ?? t('evaluation.noPatient')}</span>
                </button>
              ))}
            </>
          )}
        </aside>

        <section className="evaluation-detail">
          {!selected ? (
            <p className="muted">{t('evaluation.pickCase')}</p>
          ) : (
            <>
              <h2>{selected.title}</h2>
              <p className="muted">{selected.patientName ?? t('evaluation.noPatient')}</p>

              <div
                className={`evaluation-canvas${selected.isEvaluated ? ' readonly' : ''}`}
                onClick={addMarker}
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
                <button className="btn btn-primary" onClick={handleEvaluate} disabled={saving}>
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
