import { isGoogleCallback } from '../auth/google';

export function redirectSystemPath({ path }: { path: string; initial: boolean }) {
  try {
    if (isGoogleCallback(path)) return '/auth/callback';
    const url = new URL(path, 'fookbase-light://app');
    if (url.protocol !== 'fookbase-light:') return '/';
    // Arbitrary external paths cannot open a screen with unintended parameters.
    const destination = url.hostname === 'app' ? url.pathname : `/${url.hostname}${url.pathname}`;
    return /^\/(posts|profile|groups|conversations|stories)\/[a-zA-Z0-9-]+$/.test(destination) ? destination : '/';
  } catch { return '/'; }
}
