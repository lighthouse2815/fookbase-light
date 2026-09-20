import { useEffect, type PropsWithChildren } from 'react';
import { AppState } from 'react-native';
import { HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { useAuth } from '../auth/AuthProvider';
import { getSession, refreshSession } from '../auth/session';
import { getApiBaseUrl } from '../config/env';
export function RealtimeProvider({ children }: PropsWithChildren) {
  const { session } = useAuth(); const cache = useQueryClient();
  useEffect(() => {
    if (!session) return;
    let disposed = false;
    const hubs = ['messages', 'notifications'].map(name => new HubConnectionBuilder().withUrl(`${getApiBaseUrl()}/hubs/${name}`, { accessTokenFactory: () => getSession()?.accessToken ?? '' }).withAutomaticReconnect().build());
    const update = () => { if (!disposed) void cache.invalidateQueries(); };
    const events = ['MessageReceived', 'MessagesRead', 'MessageEdited', 'MessageDeleted', 'NotificationReceived'];
    hubs.forEach(hub => { events.forEach(event => hub.on(event, update)); hub.onreconnected(update); });
    const start = async () => {
      try {
        await refreshSession();
        if (disposed || getSession()?.user.id !== session.user.id) return;
        await Promise.all(hubs.filter(h => h.state === HubConnectionState.Disconnected).map(h => h.start()));
        update();
      } catch { /* HTTP screens retain errors and manual refresh when realtime is unavailable. */ }
    };
    void start();
    const listener = AppState.addEventListener('change', value => {
      if (value === 'active') void start();
      else hubs.forEach(h => { void h.stop().catch(() => {}); });
    });
    return () => { disposed = true; listener.remove(); hubs.forEach(h => { events.forEach(event => h.off(event, update)); void h.stop().catch(() => {}); }); };
  }, [session?.user.id, cache]);
  return children;
}
