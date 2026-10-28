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

/** Single source of truth for the stored auth token. */
export const getAuthToken = (): string | null => localStorage.getItem('authToken');

class ApiClient {
  private baseUrl: string;

  constructor(baseUrl: string) {
    this.baseUrl = baseUrl;
  }

  private getAuthHeaders(isFormData = false): HeadersInit {
    const token = getAuthToken();
    return {
      ...(token && { Authorization: `Bearer ${token}` }),
      ...(!isFormData && { 'Content-Type': 'application/json' }),
    };
  }

  private async handleResponse<T>(response: Response, endpoint: string): Promise<T> {
    if (!response.ok) {
      const body = await this.parseBody(response);

      if (response.status === 401 && !endpoint.startsWith('/api/auth/login')) {
        window.dispatchEvent(new CustomEvent(UNAUTHORIZED_EVENT));
      }

      throw new ApiError(response.status, extractErrorMessage(body, response.status), body);
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
    const response = await fetch(this.url(endpoint), {
      method: 'GET',
      headers: this.getAuthHeaders(),
    });
    return this.handleResponse<T>(response, endpoint);
  }

  async post<T>(endpoint: string, data?: unknown): Promise<T> {
    const response = await fetch(this.url(endpoint), {
      method: 'POST',
      headers: this.getAuthHeaders(),
      body: data !== undefined ? JSON.stringify(data) : undefined,
    });
    return this.handleResponse<T>(response, endpoint);
  }

  async put<T>(endpoint: string, data?: unknown): Promise<T> {
    const response = await fetch(this.url(endpoint), {
      method: 'PUT',
      headers: this.getAuthHeaders(),
      body: data !== undefined ? JSON.stringify(data) : undefined,
    });
    return this.handleResponse<T>(response, endpoint);
  }

  async patch<T>(endpoint: string, data?: unknown): Promise<T> {
    const response = await fetch(this.url(endpoint), {
      method: 'PATCH',
      headers: this.getAuthHeaders(),
      body: data !== undefined ? JSON.stringify(data) : undefined,
    });
    return this.handleResponse<T>(response, endpoint);
  }

  async delete<T>(endpoint: string): Promise<T> {
    const response = await fetch(this.url(endpoint), {
      method: 'DELETE',
      headers: this.getAuthHeaders(),
    });
    return this.handleResponse<T>(response, endpoint);
  }

  async postFormData<T>(endpoint: string, formData: FormData): Promise<T> {
    const response = await fetch(this.url(endpoint), {
      method: 'POST',
      headers: this.getAuthHeaders(true),
      body: formData,
    });
    return this.handleResponse<T>(response, endpoint);
  }

  async getBlob(endpoint: string): Promise<Blob> {
    const response = await fetch(this.url(endpoint), {
      method: 'GET',
      headers: this.getAuthHeaders(),
    });

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
