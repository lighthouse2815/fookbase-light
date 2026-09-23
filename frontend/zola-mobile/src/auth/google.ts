import * as Crypto from 'expo-crypto';
import * as SecureStore from 'expo-secure-store';
import * as WebBrowser from 'expo-web-browser';
import { getApiBaseUrl } from '../config/env';
import { apiRequest } from '../api/client';
import { getSessionGeneration } from './session';
import type { LoginResponse } from '../api/auth';

const transactionKey = 'zola.google-transaction';
type Transaction = { verifier: string; state: string; createdAt: number };
export type GoogleResult = { kind: 'link'; url: string } | { kind: 'session'; response: LoginResponse; generation: number };
let completion: { key: string; generation: number; promise: Promise<GoogleResult> } | null = null;
export function callbackUrl() {
  const value = process.env.EXPO_PUBLIC_MOBILE_CALLBACK_URL;
  if (!value) throw new Error('Đăng nhập Google chưa được cấu hình cho ứng dụng này.');
  const url = new URL(value);
  if (url.protocol !== 'https:' || url.search || url.hash || url.username || url.password) throw new Error('Địa chỉ callback Google không hợp lệ.');
  return url.toString();
}
export function isGoogleCallback(value: string) {
  try { const url = new URL(value); const expected = new URL(callbackUrl()); return url.origin === expected.origin && url.pathname === expected.pathname; } catch { return false; }
}
export async function beginGoogleLogin(): Promise<string | null> {
  const redirect = callbackUrl(); completion = null;
  const random = () => Array.from(Crypto.getRandomBytes(32), b => b.toString(16).padStart(2, '0')).join('');
  const transaction: Transaction = { verifier: random(), state: random(), createdAt: Date.now() };
  const challenge = (await Crypto.digestStringAsync(Crypto.CryptoDigestAlgorithm.SHA256, transaction.verifier, { encoding: Crypto.CryptoEncoding.BASE64 })).replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
  await SecureStore.setItemAsync(transactionKey, JSON.stringify(transaction));
  const query = new URLSearchParams({ challenge, state: transaction.state });
  const result = await WebBrowser.openAuthSessionAsync(`${getApiBaseUrl()}/api/auth/google/mobile/start?${query}`, redirect);
  if (result.type === 'success') return result.url;
  await SecureStore.deleteItemAsync(transactionKey); return null;
}
export async function completeGoogleLogin(value: string, password?: string): Promise<GoogleResult> {
  if (!isGoogleCallback(value)) throw new Error('Liên kết đăng nhập không hợp lệ.');
  const generation = getSessionGeneration();
  const url = new URL(value); const raw = await SecureStore.getItemAsync(transactionKey);
  if (!raw) {
    if (completion?.key === value && completion.generation === generation) return completion.promise;
    throw new Error('Giao dịch đăng nhập không còn tồn tại. Hãy bắt đầu lại.');
  }
  const transaction = JSON.parse(raw) as Transaction;
  const code = url.searchParams.get('code'); const state = url.searchParams.get('state'); const mode = url.searchParams.get('mode');
  if (!transaction.verifier || state !== transaction.state || Date.now() - transaction.createdAt > 300_000 || !code || code.length > 4096 || !['link','login'].includes(mode ?? '') || url.searchParams.getAll('code').length !== 1 || url.searchParams.getAll('state').length !== 1) throw new Error('Đăng nhập Google không hợp lệ hoặc đã hết hạn.');
  if (mode === 'link' && !password) return { kind:'link', url:value };
  if (completion?.key === value && completion.generation === generation) return completion.promise;
  const promise = apiRequest<LoginResponse>(`/api/auth/google/mobile/${mode === 'link' ? 'link' : 'exchange'}`, { method:'POST', body:JSON.stringify({ code, verifier:transaction.verifier, password }) })
    .then(async response => { await SecureStore.deleteItemAsync(transactionKey); return { kind:'session' as const, response, generation }; })
    .catch(error => { completion = null; throw error; });
  completion = { key:value, generation, promise }; return promise;
}
