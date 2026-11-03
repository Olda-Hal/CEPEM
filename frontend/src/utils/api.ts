/**
 * Structured API error: carries the HTTP status and the best available
 * message from the response body ({ error }, { Error }, ProblemDetails
 * title, or a plain string).
 */
export class ApiError extends Error {
  readonly status: number;
  readonly body?: unknown;

  constructor(status: number, message: string, body?: unknown) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.body = body;
  }
}

/**
 * Dispatched when an API call returns 401 outside the login endpoint,
 * i.e. an expired or invalid token mid-session. AuthContext listens and
 * logs the user out.
 */
export const UNAUTHORIZED_EVENT = 'cepem:unauthorized';

const API_BASE_URL = import.meta.env.VITE_API_URL || '';

/**
 * Tab-scoped token used while a SysAdmin previews the system as another
 * employee. Keeping it in sessionStorage means other tabs (and the admin's
 * own localStorage session) keep working with the administrator's identity.
 */
export const IMPERSONATION_TOKEN_KEY = 'cepem:impersonationToken';
export const REFRESH_TOKEN_KEY = 'authRefreshToken';

/** Single source of truth for the active auth token. */
export const getAuthToken = (): string | null =>
  sessionStorage.getItem(IMPERSONATION_TOKEN_KEY) ?? localStorage.getItem('authToken');

let refreshInFlight: Promise<boolean> | null = null;

/**
 * Exchange the stored refresh token for a new access token. Concurrent 401s
 * share one request so a page with several queries does not rotate the
 * session multiple times. Preview tokens are intentionally not refreshed:
 * they fall back to the administrator's own session.
 */
export const refreshSession = (): Promise<boolean> => {
  if (sessionStorage.getItem(IMPERSONATION_TOKEN_KEY)) {
    return Promise.resolve(false);
  }

  const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
  if (!refreshToken) {
    return Promise.resolve(false);
  }

  if (!refreshInFlight) {
    refreshInFlight = (async () => {
      try {
        const response = await fetch(`${API_BASE_URL}/api/auth/refresh`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refreshToken })
        });
        if (!response.ok) {
          return false;
        }

        const data = (await response.json()) as {
          token?: string;
          refreshToken?: string;
        };
        if (!data.token || !data.refreshToken) {
          return false;
        }

        localStorage.setItem('authToken', data.token);
        localStorage.setItem(REFRESH_TOKEN_KEY, data.refreshToken);
        return true;
      } catch {
        return false;
      } finally {
        refreshInFlight = null;
      }
    })();
  }

  return refreshInFlight;
};

class ApiClient {
  private baseUrl: string;

  constructor(baseUrl: string) {
    this.baseUrl = baseUrl;
  }

  private getAuthHeaders(isFormData = false): HeadersInit {
    const token = getAuthToken();
    return {
      ...(token && { Authorization: `Bearer ${token}` }),
      ...(!isFormData && { 'Content-Type': 'application/json' })
    };
  }

  private async fetchWithAuth(
    endpoint: string,
    init: RequestInit,
    isFormData = false,
    allowRefresh = true
  ): Promise<Response> {
    const send = () =>
      fetch(this.url(endpoint), {
        ...init,
        headers: {
          ...(this.getAuthHeaders(isFormData) as Record<string, string>),
          ...((init.headers as Record<string, string> | undefined) ?? {})
        }
      });

    let response = await send();
    if (
      response.status === 401 &&
      allowRefresh &&
      !endpoint.startsWith('/api/auth/login') &&
      endpoint !== '/api/auth/refresh' &&
      (await refreshSession())
    ) {
      response = await send();
    }

    return response;
  }

  private async handleResponse<T>(response: Response, endpoint: string): Promise<T> {
    if (!response.ok) {
      const body = await this.parseBody(response);

      if (response.status === 401 && !endpoint.startsWith('/api/auth/login')) {
        window.dispatchEvent(new CustomEvent(UNAUTHORIZED_EVENT));
      }

      throw new ApiError(response.status, extractErrorMessage(body, response.status), body);
    }

    // 204 means "nothing to return" (e.g. no next evaluation measurement).
    if (response.status === 204) {
      return null as T;
    }

    const contentType = response.headers.get('Content-Type');
    if (contentType && contentType.includes('application/json')) {
      return response.json();
    }

    return response.text() as unknown as T;
  }

  private async parseBody(response: Response): Promise<unknown> {
    try {
      const text = await response.text();
      if (!text) {
        return undefined;
      }
      try {
        return JSON.parse(text);
      } catch {
        return text;
      }
    } catch {
      return undefined;
    }
  }

  private url(endpoint: string): string {
    return `${this.baseUrl}${endpoint}`;
  }

  async get<T>(endpoint: string): Promise<T> {
    const response = await this.fetchWithAuth(endpoint, { method: 'GET' });
    return this.handleResponse<T>(response, endpoint);
  }

  async post<T>(endpoint: string, data?: unknown): Promise<T> {
    const response = await this.fetchWithAuth(endpoint, {
      method: 'POST',
      body: data !== undefined ? JSON.stringify(data) : undefined
    });
    return this.handleResponse<T>(response, endpoint);
  }

  async put<T>(endpoint: string, data?: unknown): Promise<T> {
    const response = await this.fetchWithAuth(endpoint, {
      method: 'PUT',
      body: data !== undefined ? JSON.stringify(data) : undefined
    });
    return this.handleResponse<T>(response, endpoint);
  }

  async patch<T>(endpoint: string, data?: unknown): Promise<T> {
    const response = await this.fetchWithAuth(endpoint, {
      method: 'PATCH',
      body: data !== undefined ? JSON.stringify(data) : undefined
    });
    return this.handleResponse<T>(response, endpoint);
  }

  async delete<T>(endpoint: string): Promise<T> {
    const response = await this.fetchWithAuth(endpoint, { method: 'DELETE' });
    return this.handleResponse<T>(response, endpoint);
  }

  async postFormData<T>(endpoint: string, formData: FormData): Promise<T> {
    const response = await this.fetchWithAuth(endpoint, { method: 'POST', body: formData }, true);
    return this.handleResponse<T>(response, endpoint);
  }

  async getBlob(endpoint: string): Promise<Blob> {
    const response = await this.fetchWithAuth(endpoint, { method: 'GET' });

    if (!response.ok) {
      const body = await this.parseBody(response);
      throw new ApiError(response.status, extractErrorMessage(body, response.status), body);
    }

    return response.blob();
  }
}

function extractErrorMessage(body: unknown, status: number): string {
  if (typeof body === 'string' && body.trim()) {
    return body;
  }
  if (body && typeof body === 'object') {
    const record = body as Record<string, unknown>;
    for (const key of ['error', 'Error', 'title', 'message', 'Message']) {
      const value = record[key];
      if (typeof value === 'string' && value.trim()) {
        return value;
      }
    }
  }
  return `Request failed with status ${status}`;
}

export const apiClient = new ApiClient(API_BASE_URL);
