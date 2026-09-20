import { createContext, useContext, useEffect, useSyncExternalStore, type PropsWithChildren } from 'react';
import { AppState } from 'react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { getSession, getSessionState, initializeSession, refreshSession, subscribeSession } from './session';

export const queryClient = new QueryClient({ defaultOptions: { queries: { retry: 1, staleTime: 30_000 }, mutations: { retry: false } } });
const AuthContext = createContext({ session: getSession(), state: getSessionState() });
export const useAuth = () => useContext(AuthContext);
export function AuthProvider({ children }: PropsWithChildren) {
  const session = useSyncExternalStore(subscribeSession, getSession);
  const state = useSyncExternalStore(subscribeSession, getSessionState);
  useEffect(() => { void initializeSession().catch(() => {}); }, []);
  useEffect(() => {
    void queryClient.cancelQueries();
    queryClient.clear();
  }, [session?.user.id]);
  useEffect(() => {
    if (!session) return;
    const delay = Math.max(10_000, new Date(session.accessTokenExpiresAt).getTime() - Date.now() - 60_000);
    const timer = setTimeout(() => { void refreshSession().catch(() => {}); }, Math.min(delay, 2_147_483_647));
    const listener = AppState.addEventListener('change', value => {
      if (value === 'active') void refreshSession().then(() => queryClient.invalidateQueries()).catch(() => {});
    });
    return () => { clearTimeout(timer); listener.remove(); };
  }, [session]);
  return <AuthContext.Provider value={{ session, state }}><QueryClientProvider client={queryClient}>{children}</QueryClientProvider></AuthContext.Provider>;
}
