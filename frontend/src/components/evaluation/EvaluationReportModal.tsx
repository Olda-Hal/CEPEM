import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import BaseModal from '../BaseModal';
import { apiClient } from '../../utils/api';
import { EvaluationCase, EvaluationReview } from '../../types';
import TdbSliceCard, { SliceMarker } from './TdbSliceCard';
import './EvaluationReportModal.css';
import '../../pages/EvaluationPage.css';

interface ReviewMarker extends SliceMarker {
  slice: number;
}

const REVIEW_COLORS = ['#dc2626', '#2563eb', '#059669', '#d97706', '#7c3aed'];

const parseMarkers = (json?: string | null): ReviewMarker[] => {
  if (!json) return [];
  try {
    const parsed = JSON.parse(json) as Partial<ReviewMarker>[];
    if (!Array.isArray(parsed)) return [];
    return parsed
      .filter((marker) => typeof marker?.x === 'number' && typeof marker?.y === 'number')
      .map((marker) => ({
        slice: typeof marker.slice === 'number' ? marker.slice : 0,
        x: Number(marker.x),
        y: Number(marker.y),
        note: typeof marker.note === 'string' ? marker.note : ''
      }));
  } catch {
    return [];
  }
};

interface EvaluationReportModalProps {
  caseId: number;
  onClose: () => void;
}

/**
 * Read-only interactive report stored in the patient visit: all seven scans,
 * every doctor's markers (colour per reviewer) and all comments.
 */
export const EvaluationReportModal: React.FC<EvaluationReportModalProps> = ({
  caseId,
  onClose
}) => {
  const { t } = useTranslation();
  const [report, setReport] = useState<EvaluationCase | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      setLoading(true);
      try {
        const data = await apiClient.get<EvaluationCase>(`/api/evaluations/cases/${caseId}/report`);
        if (!cancelled) setReport(data);
      } catch (err) {
        console.error('Failed to load evaluation report', err);
        if (!cancelled) setError(t('evaluation.reportLoadFailed'));
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    void load();
    return () => {
      cancelled = true;
    };
  }, [caseId, t]);

  const reviews = useMemo(() => report?.reviews ?? [], [report]);

  const markersByReview = useMemo(() => {
    const byReview = new Map<number, ReviewMarker[]>();
    reviews.forEach((review) => byReview.set(review.id, parseMarkers(review.annotationsJson)));
    return byReview;
  }, [reviews]);

  const allMarkers = useMemo(() => {
    let counter = 0;
    const result: Array<ReviewMarker & { color: string; number: number }> = [];
    reviews.forEach((review, index) => {
      const color = REVIEW_COLORS[index % REVIEW_COLORS.length] ?? REVIEW_COLORS[0] ?? '#dc2626';
      for (const marker of markersByReview.get(review.id) ?? []) {
        counter += 1;
        result.push({ ...marker, color, number: counter });
      }
    });
    return result;
  }, [reviews, markersByReview]);

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

  const reviewColor = (index: number) =>
    REVIEW_COLORS[index % REVIEW_COLORS.length] ?? REVIEW_COLORS[0] ?? '#dc2626';

  const formatDate = (value: string) => new Date(value).toLocaleString();

  return (
    <BaseModal
      title={t('evaluation.reportTitle')}
      onClose={onClose}
      contentClassName="evaluation-report-modal"
    >
      <div className="evaluation-report-body">
        {loading ? (
          <p>{t('common.loading')}</p>
        ) : error || !report || !report.tdb ? (
          <div className="alert alert-error">{error ?? t('evaluation.reportLoadFailed')}</div>
        ) : (
          <>
            <div className="tdb-meta">
              <span className="tdb-pill">
                {sideLabel(report.side) ?? t('evaluation.side.unknown')}
              </span>
              <span className="tdb-pill">
                {postureLabel(report.posture) ?? t('evaluation.posture.unknown')}
              </span>
              <span className="muted small">
                {t('evaluation.reviewCount', { count: report.reviewCount })}
              </span>
            </div>

            <div className="tdb-scan-row">
              {report.tdb.slices.map((slice) => (
                <TdbSliceCard
                  key={slice.index}
                  slice={slice}
                  measurement={report.tdb!}
                  markers={allMarkers
                    .filter((marker) => marker.slice === slice.index)
                    .map((marker) => ({
                      x: marker.x,
                      y: marker.y,
                      note: marker.note,
                      number: marker.number,
                      color: marker.color
                    }))}
                  readonly
                  onAddMarker={() => undefined}
                />
              ))}
            </div>

            <div className="evaluation-report-reviews">
              {reviews.map((review: EvaluationReview, index: number) => {
                const reviewMarkers = markersByReview.get(review.id) ?? [];
                return (
                  <article
                    key={review.id}
                    className="evaluation-review-card"
                    style={{ borderLeftColor: reviewColor(index) }}
                  >
                    <header>
                      <span
                        className="review-color-dot"
                        style={{ background: reviewColor(index) }}
                      />
                      <strong>{review.doctorName ?? t('evaluation.unknownDoctor')}</strong>
                      <span className="muted small">{formatDate(review.evaluatedAtUtc)}</span>
                      <span
                        className={`tdb-outcome-badge ${
                          review.outcome === 'OK' ? 'ok' : 'findings'
                        }`}
                      >
                        {review.outcome === 'OK'
                          ? t('evaluation.outcomeOk')
                          : t('evaluation.outcomeFindings')}
                      </span>
                    </header>

                    {review.recommendation && (
                      <p className="evaluation-review-recommendation">{review.recommendation}</p>
                    )}

                    {reviewMarkers.length > 0 ? (
                      <ul className="evaluation-review-findings">
                        {reviewMarkers.map((marker, markerIndex) => (
                          <li key={markerIndex}>
                            <span className="marker-index">
                              {t('evaluation.slice', { index: marker.slice + 1 })}
                            </span>
                            {marker.note || t('evaluation.noMarkerNote')}
                          </li>
                        ))}
                      </ul>
                    ) : (
                      <p className="muted small">{t('evaluation.noFindings')}</p>
                    )}
                  </article>
                );
              })}
            </div>
          </>
        )}
      </div>
    </BaseModal>
  );
};

export default EvaluationReportModal;
