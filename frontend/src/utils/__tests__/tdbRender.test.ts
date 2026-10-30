import { decodeFloat32Base64, renderSlicePixels, upscalePlane } from '../tdbRender';

const toBase64 = (values: Float32Array): string => {
  const bytes = new Uint8Array(values.buffer);
  let binary = '';
  for (const byte of bytes) {
    binary += String.fromCharCode(byte);
  }
  return btoa(binary);
};

describe('tdbRender', () => {
  test('decodes little-endian float32 base64', () => {
    const values = new Float32Array([1.5, -2.25, 0]);
    const decoded = decodeFloat32Base64(toBase64(values));

    expect(decoded.length).toBe(3);
    expect(decoded[0]).toBeCloseTo(1.5, 5);
    expect(decoded[1]).toBeCloseTo(-2.25, 5);
    expect(decoded[2]).toBe(0);
  });

  test('Lanczos upscale keeps a constant plane constant', () => {
    const source = 36;
    const values = new Float32Array(source * source).fill(0.42);

    const scaled = upscalePlane(values, source);

    expect(scaled.length).toBe(288 * 288);
    for (const value of scaled) {
      expect(value).toBeCloseTo(0.42, 5);
    }
  });

  test('Lanczos upscale matches the MEIK reit_resample reference', () => {
    const source = 36;
    const values = new Float32Array(source * source);
    for (let row = 0; row < source; row++) {
      for (let col = 0; col < source; col++) {
        values[row * source + col] = (row * source + col) % 100;
      }
    }

    const scaled = upscalePlane(values, source);

    // Expected values generated with meikpro/core/planes.upscale36.
    expect(scaled[0]).toBeCloseTo(0, 4);
    expect(scaled[287]).toBeCloseTo(35, 4);
    expect(scaled[100 * 288 + 200]).toBeCloseTo(70.0686, 3);
    expect(scaled[287 * 288 + 287]).toBeCloseTo(95, 4);
    expect(scaled[150 * 288 + 30]).toBeCloseTo(71.78985, 3);
  });

  test('palette output matches MEIK visual.render', () => {
    const source = 36;
    const values = new Float32Array(source * source);
    for (let row = 0; row < source; row++) {
      for (let col = 0; col < source; col++) {
        values[row * source + col] = (row * source + col) % 100;
      }
    }

    const pixels = renderSlicePixels(
      { values: toBase64(values), width: source, height: source },
      { windowLow: 0, windowHigh: 99 }
    );
    const rgbAt = (row: number, col: number): number[] =>
      Array.from(pixels.slice((row * 288 + col) * 4, (row * 288 + col) * 4 + 3));

    // Expected values generated with meikpro/core/visual.render.
    expect(rgbAt(100, 100)).toEqual([255, 143, 0]);
    expect(rgbAt(150, 30)).toEqual([255, 207, 0]);
    expect(rgbAt(50, 200)).toEqual([0, 191, 255]);
    expect(rgbAt(200, 50)).toEqual([255, 143, 0]);
    expect(rgbAt(287, 120)).toEqual([255, 15, 0]);
    expect(rgbAt(0, 120)).toEqual([0, 127, 255]);
    expect(rgbAt(280, 144)).toEqual([191, 255, 79]);
  });
});
