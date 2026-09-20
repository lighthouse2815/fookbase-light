import * as SecureStore from 'expo-secure-store';
import type { AuthenticationResponse } from '../api/auth';
import { getApiBaseUrl } from '../config/env';

const key = 'fookbase.refresh-token';
export type SessionState = 'loading' | 'anonymous' | 'authenticated' | 'offline' | 'storage-error';
let session: AuthenticationResponse | null = null;
let state: SessionState = 'loading';
let generation = 0;
let refresh: Promise<string | null> | null = null;
let storageQueue: Promise<unknown> = Promise.resolve();
const listeners = new Set<() => void>();
export const getSession = () => session;
export const getSessionState = () => state;
export const getSessionGeneration = () => generation;
export function subscribeSession(listener: () => void) { listeners.add(listener); return () => { listeners.delete(listener); }; }
function emit(next: SessionState) { state = next; listeners.forEach(fn => fn()); }
function write(operation: () => Promise<void>) {
  const next = storageQueue.then(operation, operation);
  storageQueue = next.catch(() => {});
  return next;
}
export async function saveSession(value: AuthenticationResponse, expected = generation) {
  if (!value.accessToken || !value.refreshToken || !value.user?.id) throw new Error('Phiên đăng nhập không hợp lệ.');
  await write(async () => {
    if (generation !== expected) return;
    try { await SecureStore.setItemAsync(key, value.refreshToken); }
    catch { session = null; emit('storage-error'); throw new Error('Không thể lưu phiên an toàn. Vui lòng thử lại.'); }
    if (generation === expected) { session = value; emit('authenticated'); }
  });
}
export async function clearSession() {
  generation++;
  session = null;
  emit('anonymous');
  try { await write(() => SecureStore.deleteItemAsync(key)); }
  catch { emit('storage-error'); throw new Error('Chưa xóa được phiên trên thiết bị. Hãy thử đăng xuất lại.'); }
}
export function refreshSession(): Promise<string | null> {
  if (refresh) return refresh;
  const expected = generation;
  refresh = (async () => {
    let token: string | null;
    try { await storageQueue; token = await SecureStore.getItemAsync(key); }
    catch { emit('storage-error'); throw new Error('Không đọc được phiên an toàn.'); }
    if (generation !== expected) return null;
    if (!token) { session = null; emit('anonymous'); return null; }
    let response: Response;
    try {
      response = await fetch(`${getApiBaseUrl()}/api/auth/refresh`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ refreshToken: token }),
      });
    } catch { if (generation === expected) emit('offline'); throw new Error('Không thể kết nối máy chủ. Phiên vẫn được giữ để thử lại.'); }
    if (generation !== expected) return null;
    if (response.status === 401) { await clearSession(); return null; }
    if (!response.ok) { emit('offline'); throw new Error('Máy chủ chưa thể làm mới phiên. Vui lòng thử lại.'); }
    const value = await response.json() as AuthenticationResponse;
    await saveSession(value, expected);
    return expected === generation ? value.accessToken : null;
  })().finally(() => { refresh = null; });
  return refresh;
}
export async function initializeSession() { await refreshSession(); }
