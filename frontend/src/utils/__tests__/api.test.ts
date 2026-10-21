import { vi } from 'vitest';
import { apiClient } from '../api';

describe('API Client', () => {
  beforeEach(() => {
    // Clear localStorage before each test
    localStorage.clear();
    // Reset fetch mock
    globalThis.fetch = vi.fn();
  });

  afterEach(() => {
    vi.resetAllMocks();
  });

  test('includes auth token in request headers when available', async () => {
    const mockToken = 'test-jwt-token';
    localStorage.setItem('authToken', mockToken);

    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({ data: 'test' }),
      headers: {
        get: vi.fn().mockReturnValue('application/json')
      }
    });

    await apiClient.get('/test-endpoint');

    expect(globalThis.fetch).toHaveBeenCalledWith(
      expect.stringContaining('/test-endpoint'),
      expect.objectContaining({
        headers: expect.objectContaining({
          'Authorization': `Bearer ${mockToken}`
        })
      })
    );
  });

  test('makes GET request without auth token when not available', async () => {
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({ data: 'test' }),
      headers: {
        get: vi.fn().mockReturnValue('application/json')
      }
    });

    await apiClient.get('/test-endpoint');

    expect(globalThis.fetch).toHaveBeenCalledWith(
      expect.stringContaining('/test-endpoint'),
      expect.objectContaining({
        method: 'GET',
        headers: expect.objectContaining({
          'Content-Type': 'application/json'
        })
      })
    );
  });

  test('makes POST request with data', async () => {
    const testData = { name: 'test' };
    
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({ success: true }),
      headers: {
        get: vi.fn().mockReturnValue('application/json')
      }
    });

    await apiClient.post('/test-endpoint', testData);

    expect(globalThis.fetch).toHaveBeenCalledWith(
      expect.stringContaining('/test-endpoint'),
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify(testData),
        headers: expect.objectContaining({
          'Content-Type': 'application/json'
        })
      })
    );
  });

  test('throws error for non-ok responses', async () => {
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValue({
      ok: false,
      status: 404,
      statusText: 'Not Found'
    });

    await expect(apiClient.get('/test-endpoint')).rejects.toThrow();
  });
});
