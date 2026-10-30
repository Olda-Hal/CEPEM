import { TdbMeasurement, TdbSlice } from '../types';

/**
 * Rendering of MEIK .tdb display planes, matching the original MEIKPRO
 * seven-panel renderer:
 *  - the stored 36x36 float planes are upscaled 288x288 with the exact
 *    normalized Lanczos-3 kernel (core/planes.py in MEIK_remake),
 *  - one shared window = global min/max of all seven planes,
 *  - 32 effective gradations from the 64-colour red→yellow→cyan→blue table
 *    (the renderer uses only even palette entries),
 *  - the 18x18 breast silhouette masks the outside, rows are flipped.
 */

const IMAGE_SIZE = 288;
const CELL_GRID = 18;
const CELL_SIZE = IMAGE_SIZE / CELL_GRID;

// pal64[::2] — the table the original draws with (32 effective colours).
const PALETTE: ReadonlyArray<readonly [number, number, number]> = [
  [143, 0, 0],
  [175, 0, 0],
  [207, 0, 0],
  [239, 0, 0],
  [255, 15, 0],
  [255, 47, 0],
  [255, 79, 0],
  [255, 111, 0],
  [255, 143, 0],
  [255, 175, 0],
  [255, 207, 0],
  [255, 239, 0],
  [255, 255, 15],
  [223, 255, 47],
  [191, 255, 79],
  [159, 255, 111],
  [127, 255, 143],
  [95, 255, 175],
  [63, 255, 207],
  [31, 255, 239],
  [0, 255, 255],
  [0, 223, 255],
  [0, 191, 255],
  [0, 159, 255],
  [0, 127, 255],
  [0, 95, 255],
  [0, 63, 255],
  [0, 31, 255],
  [0, 0, 255],
  [0, 0, 223],
  [0, 0, 191],
  [0, 0, 159]
] as const;

const SILHOUETTE_ROWS = [
  'XXXXXX......XXXXXX',
  'XXXX..........XXXX',
  'XXX............XXX',
  'XX..............XX',
  'X................X',
  'X................X',
  '..................',
  '..................',
  '..................',
  '..................',
  '..................',
  '..................',
  'X................X',
  'X................X',
  'XX..............XX',
  'XXX............XXX',
  'XXXX..........XXXX',
  'XXXXXX......XXXXXX'
];

const SILHOUETTE = SILHOUETTE_ROWS.map((row) => row.split('').map((ch) => ch === '.'));

const kernelCache = new Map<string, number[][]>();

function lanczosKernel(source: number, target: number): number[][] {
  const key = `${source}:${target}`;
  const cached = kernelCache.get(key);
  if (cached) {
    return cached;
  }
  const matrix: number[][] = [];
  for (let j = 0; j < target; j++) {
    const x = (j * (source - 1)) / (target - 1);
    const low = Math.max(0, Math.ceil(x - 3));
    const high = Math.min(source - 1, Math.floor(x + 3));
    const row = new Array<number>(source).fill(0);
    let sum = 0;
    for (let k = low; k <= high; k++) {
      const delta = k - x;
      const weight = sinc(delta) * sinc(delta / 3);
      row[k] = weight;
      sum += weight;
    }
    if (sum !== 0) {
      for (let k = low; k <= high; k++) {
        row[k] = (row[k] ?? 0) / sum;
      }
    }
    matrix.push(row);
  }
  kernelCache.set(key, matrix);
  return matrix;
}

function sinc(value: number): number {
  if (value === 0) return 1;
  const angle = Math.PI * value;
  return Math.sin(angle) / angle;
}

/** 36x36 -> 288x288 normalized Lanczos-3, clamped to the input range. */
export function upscalePlane(values: Float32Array, source: number): Float32Array {
  const target = IMAGE_SIZE;
  const kernel = lanczosKernel(source, target);

  let low = Infinity;
  let high = -Infinity;
  for (const value of values) {
    if (Number.isFinite(value)) {
      if (value < low) low = value;
      if (value > high) high = value;
    }
  }
  if (!Number.isFinite(low) || !Number.isFinite(high)) {
    low = 0;
    high = 1;
  }

  // Horizontal pass: source rows -> target columns.
  const temp = new Float64Array(source * target);
  for (let row = 0; row < source; row++) {
    const base = row * source;
    for (let col = 0; col < target; col++) {
      let sum = 0;
      const weights = kernel[col] as number[];
      for (let k = 0; k < source; k++) {
        const w = weights[k] ?? 0;
        if (w !== 0) sum += w * (values[base + k] ?? 0);
      }
      temp[row * target + col] = sum;
    }
  }

  // Vertical pass: source rows -> target rows.
  const out = new Float32Array(target * target);
  for (let row = 0; row < target; row++) {
    const weights = kernel[row] as number[];
    for (let col = 0; col < target; col++) {
      let sum = 0;
      for (let k = 0; k < source; k++) {
        const w = weights[k] ?? 0;
        if (w !== 0) sum += w * (temp[k * target + col] ?? 0);
      }
      out[row * target + col] = Math.min(high, Math.max(low, sum));
    }
  }
  return out;
}

export function decodeFloat32Base64(base64: string): Float32Array {
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }
  return new Float32Array(bytes.buffer);
}

function nearestSquare(values: Float32Array, source: number): Float32Array {
  const out = new Float32Array(IMAGE_SIZE * IMAGE_SIZE);
  for (let row = 0; row < IMAGE_SIZE; row++) {
    const sourceRow = Math.min(Math.floor((row * source) / IMAGE_SIZE), source - 1);
    for (let col = 0; col < IMAGE_SIZE; col++) {
      const sourceCol = Math.min(Math.floor((col * source) / IMAGE_SIZE), source - 1);
      out[row * IMAGE_SIZE + col] = values[sourceRow * source + sourceCol] ?? 0;
    }
  }
  return out;
}

/** RGBA pixels of one rendered slice (288x288, row-flipped, masked). */
export function renderSlicePixels(
  slice: Pick<TdbSlice, 'values' | 'width' | 'height'>,
  measurement: Pick<TdbMeasurement, 'windowLow' | 'windowHigh'>
): Uint8ClampedArray {
  const values = decodeFloat32Base64(slice.values);
  const source = slice.width;
  const plane =
    slice.width === slice.height && slice.width > 0
      ? upscalePlane(values, source)
      : nearestSquare(values, source);

  const low = measurement.windowLow;
  let high = measurement.windowHigh;
  if (!Number.isFinite(low) || !Number.isFinite(high) || high <= low) {
    high = low + 1;
  }

  const data = new Uint8ClampedArray(IMAGE_SIZE * IMAGE_SIZE * 4);
  for (let displayRow = 0; displayRow < IMAGE_SIZE; displayRow++) {
    // The original writes bottom-up bitmaps, i.e. flips the rows.
    const row = IMAGE_SIZE - 1 - displayRow;
    const cellRow = Math.min(Math.floor(displayRow / CELL_SIZE), CELL_GRID - 1);
    for (let col = 0; col < IMAGE_SIZE; col++) {
      const cellCol = Math.min(Math.floor(col / CELL_SIZE), CELL_GRID - 1);
      const offset = (displayRow * IMAGE_SIZE + col) * 4;

      if (!SILHOUETTE[cellRow]?.[cellCol]) {
        data[offset] = 11;
        data[offset + 1] = 10;
        data[offset + 2] = 10;
        data[offset + 3] = 255;
        continue;
      }

      const value = plane[row * IMAGE_SIZE + col] ?? 0;
      let u = Number.isFinite(value) ? (value - low) / (high - low) : 0;
      u = Math.min(1, Math.max(0, u));
      let index = Math.floor(64 * u);
      if (index >= 64) index = 0; // the v == max pixel wraps to palette[0]
      index -= index % 2; // only even 64-table entries are used
      const [r, g, b] = PALETTE[index / 2] ?? [143, 0, 0];
      data[offset] = r;
      data[offset + 1] = g;
      data[offset + 2] = b;
      data[offset + 3] = 255;
    }
  }

  return data;
}

/** Draw one slice into a 288x288 canvas using the measurement-wide window. */
export function drawTdbSlice(
  canvas: HTMLCanvasElement,
  slice: TdbSlice,
  measurement: Pick<TdbMeasurement, 'windowLow' | 'windowHigh'>
): void {
  const context = canvas.getContext('2d');
  if (!context) return;
  canvas.width = IMAGE_SIZE;
  canvas.height = IMAGE_SIZE;
  const image = context.createImageData(IMAGE_SIZE, IMAGE_SIZE);
  image.data.set(renderSlicePixels(slice, measurement));
  context.putImageData(image, 0, 0);
}
