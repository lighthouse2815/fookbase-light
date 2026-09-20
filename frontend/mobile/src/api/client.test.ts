import { apiRequest } from './client';
import { getSession, getSessionGeneration, refreshSession } from '../auth/session';
jest.mock('../auth/session', () => ({ getSession: jest.fn(), getSessionGeneration: jest.fn(), refreshSession: jest.fn() }));
jest.mock('../config/env', () => ({ getApiBaseUrl: () => 'https://api.example.test' }));
beforeEach(() => { jest.clearAllMocks(); global.fetch = jest.fn(); jest.mocked(getSessionGeneration).mockReturnValue(0); });
test('does not attach API bearer token to external URLs', async () => {
  await expect(apiRequest('https://elsewhere.test')).rejects.toThrow(); expect(fetch).not.toHaveBeenCalled();
});
test('handles empty 204 responses', async () => {
  jest.mocked(fetch).mockResolvedValue({ ok: true, status: 204 } as Response);
  await expect(apiRequest('/api/posts/x', { method: 'DELETE' })).resolves.toBeUndefined();
});
test('does not replay mutations after ambiguous network failure', async () => {
  jest.mocked(fetch).mockRejectedValue(new TypeError('lost connection'));
  await expect(apiRequest('/api/posts', { method: 'POST', body: '{}' })).rejects.toThrow();
  expect(fetch).toHaveBeenCalledTimes(1); expect(refreshSession).not.toHaveBeenCalled();
});
test('retries a 401 once using refreshed token', async () => {
  jest.mocked(getSession).mockReturnValue({ accessToken: 'expired' } as ReturnType<typeof getSession>);
  jest.mocked(refreshSession).mockResolvedValue('fresh');
  jest.mocked(fetch).mockResolvedValueOnce({ status: 401 } as Response).mockResolvedValueOnce({ ok: true, status: 200, json: async () => ({ items: [] }) } as Response);
  await apiRequest('/api/feed'); expect(fetch).toHaveBeenCalledTimes(2);
  const headers = jest.mocked(fetch).mock.calls[1][1]?.headers as Headers;
  expect(headers.get('Authorization')).toBe('Bearer fresh');
});
