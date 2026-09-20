import { validateApiBaseUrl } from './env';

test.each([undefined, '', '/api', 'http://api.test', 'https://localhost', 'https://127.0.0.1', 'https://[::1]', 'https://user:pass@api.test', 'https://api.test/api', 'https://api.test/?token=x'])('rejects invalid endpoint %s', value => {
  expect(() => validateApiBaseUrl(value)).toThrow();
});
test('normalizes a valid HTTPS origin', () => {
  expect(validateApiBaseUrl('https://api.example.test/')).toBe('https://api.example.test');
});
