export interface ApiErrorBody {
  code?: string;
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly body: ApiErrorBody,
  ) {
    super(message);
    this.name = 'ApiError';
  }

  get fieldErrors(): string[] {
    const errors = this.body.errors ?? {};
    return Object.values(errors).flat();
  }
}

let onUnauthorized: (() => void) | null = null;

/** Registered by the auth provider so any 401 session expiry bounces to login. */
export function setUnauthorizedHandler(handler: (() => void) | null): void {
  onUnauthorized = handler;
}

export async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {}),
    },
    ...init,
  });

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  const body = text.length > 0 ? (JSON.parse(text) as unknown) : null;

  if (!response.ok) {
    const parsed = (body ?? {}) as ApiErrorBody;
    if (response.status === 401 && onUnauthorized) {
      onUnauthorized();
    }
    throw new ApiError(parsed.detail ?? parsed.title ?? `Request failed (${response.status}).`, response.status, parsed);
  }

  return body as T;
}

export const json = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, data?: unknown) =>
    request<T>(path, { method: 'POST', body: data === undefined ? undefined : JSON.stringify(data) }),
  put: <T>(path: string, data?: unknown) =>
    request<T>(path, { method: 'PUT', body: data === undefined ? undefined : JSON.stringify(data) }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
};

/** Formats an ApiError into user-facing text, preferring field-level messages. */
export function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    const fields = error.fieldErrors;
    if (fields.length > 0) {
      return fields.join('; ');
    }
    return error.message;
  }
  return error instanceof Error ? error.message : 'An unexpected error occurred.';
}