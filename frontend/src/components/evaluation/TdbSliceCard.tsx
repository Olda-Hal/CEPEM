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
  measurement: Pick<TdbMeasurement, 'windowLow' | 'windowHigh' | 'histogramLow' | 'histogramHigh'>;
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

  // All seven histograms share the measurement-wide axis. Fall back to the
  // slice edges for cases stored before the shared axis existed.
  const hasSharedAxis =
    Number.isFinite(measurement.histogramLow) &&
    Number.isFinite(measurement.histogramHigh) &&
    measurement.histogramHigh > measurement.histogramLow;
  const axisLow = hasSharedAxis ? measurement.histogramLow : (slice.histogram.edges[0] ?? 0);
  const axisHigh = hasSharedAxis
    ? measurement.histogramHigh
    : (slice.histogram.edges[slice.histogram.edges.length - 1] ?? axisLow + 1);
  const axisTicks = [axisLow, (axisLow + axisHigh) / 2, axisHigh];

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

      <div className="tdb-histogram" role="img" aria-label={t('evaluation.histogram')}>
        <svg viewBox="0 0 100 40" preserveAspectRatio="none" className="tdb-histogram-svg">
          {[0, 25, 50, 75, 100].map((x) => (
            <line key={`v${x}`} x1={x} y1={0} x2={x} y2={40} className="tdb-hist-grid" />
          ))}
          {[0, 10, 20, 30, 40].map((y) => (
            <line key={`h${y}`} x1={0} y1={y} x2={100} y2={y} className="tdb-hist-grid" />
          ))}
          {counts.map((count, index) => {
            const width = 100 / Math.max(counts.length, 1);
            const height = (count / peak) * 38;
            return (
              <rect
                key={index}
                x={index * width + 0.15}
                y={40 - height}
                width={Math.max(width - 0.3, 0.3)}
                height={height}
                className="tdb-histogram-bar"
              >
                <title>
                  {`${formatValue(slice.histogram.edges[index] ?? 0)} – ${formatValue(
                    slice.histogram.edges[index + 1] ?? 0
                  )}: ${count}`}
                </title>
              </rect>
            );
          })}
        </svg>
      </div>
      <div className="tdb-histogram-axis muted small">
        {axisTicks.map((tick, index) => (
          <span key={index}>{formatValue(tick)}</span>
        ))}
      </div>
    </article>
  );
};

export default TdbSliceCard;
