import * as SecureStore from 'expo-secure-store';
import { clearSession, getSession, getSessionState, initializeSession, refreshSession, saveSession } from './session';

jest.mock('expo-secure-store', () => ({ getItemAsync: jest.fn(), setItemAsync: jest.fn(), deleteItemAsync: jest.fn() }));
jest.mock('../config/env', () => ({ getApiBaseUrl: () => 'https://api.example.test' }));
const value = { user: { id: 'u1', email: null, phoneNumber: null, username: 'tester', emailConfirmed: false, phoneNumberConfirmed: false, roles: [] }, accessToken: 'access', refreshToken: 'refresh', accessTokenExpiresAt: '2030-01-01', refreshTokenExpiresAt: '2031-01-01' };
beforeEach(async () => {
  jest.clearAllMocks();
  jest.mocked(SecureStore.deleteItemAsync).mockResolvedValue();
  jest.mocked(SecureStore.setItemAsync).mockResolvedValue();
  await clearSession();
  global.fetch = jest.fn();
});
test('starts anonymous without refresh token', async () => {
  jest.mocked(SecureStore.getItemAsync).mockResolvedValue(null);
  await initializeSession();
  expect(getSession()).toBeNull(); expect(getSessionState()).toBe('anonymous'); expect(fetch).not.toHaveBeenCalled();
});
test('network failure preserves stored refresh token', async () => {
  jest.mocked(SecureStore.getItemAsync).mockResolvedValue('refresh');
  jest.mocked(fetch).mockRejectedValue(new TypeError('offline'));
  jest.mocked(SecureStore.deleteItemAsync).mockClear();
  await expect(refreshSession()).rejects.toThrow();
  expect(SecureStore.deleteItemAsync).not.toHaveBeenCalled(); expect(getSessionState()).toBe('offline');
});
test('concurrent refresh requests share one HTTP request', async () => {
  jest.mocked(SecureStore.getItemAsync).mockResolvedValue('refresh');
  jest.mocked(fetch).mockResolvedValue({ ok: true, status: 200, json: async () => value } as Response);
  await Promise.all([refreshSession(), refreshSession(), refreshSession()]);
  expect(fetch).toHaveBeenCalledTimes(1); expect(getSession()?.user.id).toBe('u1');
});
test('logout invalidates refresh response arriving later', async () => {
  jest.mocked(SecureStore.getItemAsync).mockResolvedValue('refresh');
  let finish!: (v: Response) => void;
  jest.mocked(fetch).mockImplementation(() => new Promise(resolve => { finish = resolve; }));
  const pending = refreshSession();
  while (!finish) await Promise.resolve();
  await clearSession();
  finish({ ok: true, status: 200, json: async () => value } as Response);
  await pending; expect(getSession()).toBeNull();
});
test('secure storage failure does not authenticate', async () => {
  jest.mocked(SecureStore.setItemAsync).mockRejectedValue(new Error('locked'));
  await expect(saveSession(value)).rejects.toThrow();
  expect(getSession()).toBeNull(); expect(getSessionState()).toBe('storage-error');
});
test('unauthorized refresh removes persisted session', async () => {
  jest.mocked(SecureStore.getItemAsync).mockResolvedValue('revoked');
  jest.mocked(fetch).mockResolvedValue({ ok: false, status: 401 } as Response);
  await refreshSession(); expect(SecureStore.deleteItemAsync).toHaveBeenCalled(); expect(getSessionState()).toBe('anonymous');
});
