import { completeGoogleLogin, isGoogleCallback } from './google';
import * as SecureStore from 'expo-secure-store';
import { getSessionGeneration } from './session';
import { apiRequest } from '../api/client';
jest.mock('./session',()=>({getSessionGeneration:jest.fn(()=>0)}));
jest.mock('expo-crypto',()=>({}));
jest.mock('expo-web-browser',()=>({}));
jest.mock('expo-secure-store',()=>({ getItemAsync:jest.fn(), deleteItemAsync:jest.fn() }));
jest.mock('../api/client',()=>({ apiRequest:jest.fn() }));
beforeEach(()=>{process.env.EXPO_PUBLIC_MOBILE_CALLBACK_URL='https://app.example.test/auth/callback';jest.clearAllMocks();});
test('rejects a callback from another domain',async()=>{
  expect(isGoogleCallback('https://evil.test/auth/callback')).toBe(false);
  await expect(completeGoogleLogin('https://evil.test/auth/callback')).rejects.toThrow();
  expect(apiRequest).not.toHaveBeenCalled();
});
test('wrong state does not exchange a completion',async()=>{
  jest.mocked(SecureStore.getItemAsync).mockResolvedValue(JSON.stringify({verifier:'v'.repeat(64),state:'correct',createdAt:Date.now()}));
  await expect(completeGoogleLogin('https://app.example.test/auth/callback?code=c&state=wrong&mode=login')).rejects.toThrow();
  expect(apiRequest).not.toHaveBeenCalled();
});
test('password link remains pending until password is provided',async()=>{
  jest.mocked(SecureStore.getItemAsync).mockResolvedValue(JSON.stringify({verifier:'v'.repeat(64),state:'correct',createdAt:Date.now()}));
  const url='https://app.example.test/auth/callback?code=c&state=correct&mode=link';
  await expect(completeGoogleLogin(url)).resolves.toEqual({kind:'link',url});
  expect(apiRequest).not.toHaveBeenCalled(); expect(SecureStore.deleteItemAsync).not.toHaveBeenCalled();
});

test('cannot replay a completed browser callback after logout', async () => {
  const url='https://app.example.test/auth/callback?code=single-use&state=correct&mode=login';
  jest.mocked(getSessionGeneration).mockReturnValue(0);
  jest.mocked(SecureStore.getItemAsync).mockResolvedValue(JSON.stringify({verifier:'v'.repeat(64),state:'correct',createdAt:Date.now()}));
  jest.mocked(apiRequest).mockResolvedValue({accessToken:'access'});
  await completeGoogleLogin(url);
  jest.mocked(SecureStore.getItemAsync).mockResolvedValue(null);
  jest.mocked(getSessionGeneration).mockReturnValue(1);
  await expect(completeGoogleLogin(url)).rejects.toThrow('Giao dịch');
  expect(apiRequest).toHaveBeenCalledTimes(1);
});
test('expired transaction does not exchange a code',async()=>{
  jest.mocked(SecureStore.getItemAsync).mockResolvedValue(JSON.stringify({verifier:'v'.repeat(64),state:'correct',createdAt:Date.now()-300001}));
  await expect(completeGoogleLogin('https://app.example.test/auth/callback?code=expired&state=correct&mode=login')).rejects.toThrow();
  expect(apiRequest).not.toHaveBeenCalled();
});
