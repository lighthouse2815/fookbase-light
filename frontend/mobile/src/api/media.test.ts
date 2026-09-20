import { uploadMedia } from './media';
import { apiRequest } from './client';
import { fetch as nativeFetch } from 'expo/fetch';
jest.mock('./client', () => ({ apiRequest: jest.fn() }));
jest.mock('expo/fetch', () => ({ fetch: jest.fn() }));
jest.mock('expo-file-system', () => ({ File: class extends Blob { uri: string; constructor(uri: string) { super([new Uint8Array([1])], { type: 'image/jpeg' }); this.uri = uri; } } }));
jest.mock('expo-image-picker', () => ({ launchImageLibraryAsync: jest.fn() }));
const file = { uri: 'file:///photo.jpg', name: 'photo.jpg', mimeType: 'image/jpeg', sizeBytes: 100 };
beforeEach(() => { jest.clearAllMocks(); });
test('intent failure stops before upload', async () => {
  jest.mocked(apiRequest).mockRejectedValueOnce(new Error('too large'));
  await expect(uploadMedia(file)).rejects.toThrow('too large'); expect(nativeFetch).not.toHaveBeenCalled();
});
test('failed upload never completes media', async () => {
  jest.mocked(apiRequest).mockResolvedValueOnce({ mediaId: 'm1', uploadUrl: 'https://storage.test/upload', uploadMethod: 'POST', uploadParameters: { signature: 'signed' } });
  jest.mocked(nativeFetch).mockResolvedValueOnce({ ok: false } as Awaited<ReturnType<typeof nativeFetch>>);
  await expect(uploadMedia(file)).rejects.toThrow(); expect(apiRequest).toHaveBeenCalledTimes(1);
});
test('only returns media after server completes upload', async () => {
  jest.mocked(apiRequest).mockResolvedValueOnce({ mediaId: 'm1', uploadUrl: 'https://storage.test/upload', uploadMethod: 'POST', uploadParameters: {} }).mockResolvedValueOnce({ id: 'm1' });
  jest.mocked(nativeFetch).mockResolvedValueOnce({ ok: true } as Awaited<ReturnType<typeof nativeFetch>>);
  await expect(uploadMedia(file)).resolves.toBe('m1');
  expect(apiRequest).toHaveBeenLastCalledWith('/api/media/m1/complete', { method: 'POST' });
});
