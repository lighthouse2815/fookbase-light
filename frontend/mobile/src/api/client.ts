import { getApiBaseUrl } from '../config/env';
import { getSession, getSessionGeneration, refreshSession } from '../auth/session';

export class ApiError extends Error {
  constructor(message: string, public readonly status: number) { super(message); this.name = 'ApiError'; }
}
export async function apiRequest<T>(path: string, init: RequestInit = {}): Promise<T> {
  if (!path.startsWith('/api/') || path.includes('://')) throw new ApiError('Đường dẫn API không hợp lệ.', 0);
  const generation = getSessionGeneration();
  const send = (token: string | null) => {
    const headers = new Headers(init.headers);
    headers.set('Accept', 'application/json');
    if (init.body && !(init.body instanceof FormData)) headers.set('Content-Type', 'application/json');
    if (token) headers.set('Authorization', `Bearer ${token}`);
    return fetch(`${getApiBaseUrl()}${path}`, { ...init, headers });
  };
  let response: Response;
  try {
    const token = getSession()?.accessToken ?? null;
    response = await send(token);
    if (response.status === 401 && token && !/^\/api\/auth\/(login|refresh|logout|registration(?:\/|$)|google(?:\/|$)|2fa\/verify|password\/(forgot|reset))/.test(path)) {
      const next = await refreshSession();
      if (next && generation === getSessionGeneration()) response = await send(next);
    }
  } catch (error) {
    if (error instanceof Error && error.name === 'AbortError') throw error;
    throw new ApiError(error instanceof Error ? error.message : 'Không thể kết nối máy chủ.', 0);
  }
  if (generation !== getSessionGeneration()) throw new ApiError('Phiên đăng nhập đã thay đổi.', 401);
  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    throw new ApiError(problem?.detail ?? problem?.title ?? 'Yêu cầu không thành công.', response.status);
  }
  return response.status === 204 ? undefined as T : response.json();
}
