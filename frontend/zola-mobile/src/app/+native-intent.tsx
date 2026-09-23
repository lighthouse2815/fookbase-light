import { isGoogleCallback } from '../auth/google';

export function redirectSystemPath({ path }: { path: string; initial: boolean }) {
  try {
    if (isGoogleCallback(path)) return '/auth/callback';
    const url = new URL(path, 'zola://app');
    if (url.protocol !== 'zola:') return '/';
    // Arbitrary external paths cannot open a screen with unintended parameters.
    const destination = url.hostname === 'app' ? url.pathname : `/${url.hostname}${url.pathname}`;
    return /^\/conversations\/[a-zA-Z0-9-]+$/.test(destination) ? destination : '/';
  } catch { return '/'; }
}
