import React, { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { TdbMeasurement, TdbSlice } from '../../types';
import { drawTdbSlice } from '../../utils/tdbRender';

export interface SliceMarker {
  x: number;
  y: number;
  note: string;
  /** Global marker number (shared with the findings list). */
  number?: number;
  /** Marker colour, used by the multi-doctor report. */
  color?: string;
}

interface TdbSliceCardProps {
  slice: TdbSlice;
  measurement: Pick<TdbMeasurement, 'windowLow' | 'windowHigh'>;
  markers: SliceMarker[];
  readonly: boolean;
  onAddMarker: (x: number, y: number) => void;
}

const formatValue = (value: number): string => {
  if (!Number.isFinite(value)) return '–';
  return Math.abs(value) >= 1000 ? Math.round(value).toLocaleString() : value.toFixed(2);
};

/**
 * One anonymised TDB slice: the rendered tomogram, click-to-mark findings and
 * the distribution of measured values underneath.
 */
export const TdbSliceCard: React.FC<TdbSliceCardProps> = ({
  slice,
  measurement,
  markers,
  readonly,
  onAddMarker
}) => {
  const { t } = useTranslation();
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    if (canvasRef.current) {
      drawTdbSlice(canvasRef.current, slice, measurement);
    }
  }, [slice, measurement]);

  const handleClick = (event: React.MouseEvent<HTMLDivElement>) => {
    if (readonly) return;
    const rect = event.currentTarget.getBoundingClientRect();
    const x = Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width));
    const y = Math.min(1, Math.max(0, (event.clientY - rect.top) / rect.height));
    onAddMarker(Number(x.toFixed(4)), Number(y.toFixed(4)));
  };

  const counts = slice.histogram.counts;
  const peak = Math.max(1, ...counts);

  return (
    <article className="tdb-slice-card">
      <header className="tdb-slice-header">
        <strong>
          {t('evaluation.slice', { index: slice.index + 1 })}
          <span className="tdb-slice-depth">{slice.depthMm} mm</span>
        </strong>
        <span className="muted small">
          {t('evaluation.sliceValues', {
            min: formatValue(slice.min),
            mean: formatValue(slice.mean),
            max: formatValue(slice.max)
          })}
        </span>
      </header>

      <div
        className={`tdb-slice-canvas${readonly ? ' readonly' : ''}`}
        onClick={handleClick}
        role="presentation"
      >
        <canvas ref={canvasRef} className="tdb-slice-image" />
        {markers.map((marker, index) => (
          <span
            key={`${marker.x}-${marker.y}-${index}`}
            className="evaluation-marker"
            style={{
              left: `${marker.x * 100}%`,
              top: `${marker.y * 100}%`,
              ...(marker.color ? { background: marker.color } : {})
            }}
            title={marker.note || `#${marker.number ?? index + 1}`}
          >
            {marker.number ?? index + 1}
          </span>
        ))}
      </div>

      <div className="tdb-histogram" aria-label={t('evaluation.histogram')}>
        {counts.map((count, index) => (
          <span
            key={index}
            className="tdb-histogram-bar"
            style={{ height: `${Math.max((count / peak) * 100, count > 0 ? 4 : 0)}%` }}
            title={`${formatValue(slice.histogram.edges[index] ?? 0)} – ${formatValue(
              slice.histogram.edges[index + 1] ?? 0
            )}: ${count}`}
          />
        ))}
      </div>
      <div className="tdb-histogram-axis muted small">
        <span>{formatValue(slice.histogram.edges[0] ?? 0)}</span>
        <span>{t('evaluation.histogram')}</span>
        <span>{formatValue(slice.histogram.edges[slice.histogram.edges.length - 1] ?? 0)}</span>
      </div>
    </article>
  );
};

export default TdbSliceCard;
