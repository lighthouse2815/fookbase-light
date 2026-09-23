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
    const events = ['MessageReceived', 'MessagesRead', 'MessageEdited', 'MessageDeleted', 'NotificationReceived', 'ConversationCreated', 'ConversationUpdated', 'ParticipantAdded', 'ParticipantRemoved'];
    hubs.forEach(hub => { events.forEach(event => hub.on(event, update)); hub.onreconnected(update); });
    const removed = (event: { conversationId: string; userId: string }) => { if (event.userId === session.user.id) { cache.removeQueries({ queryKey: ['messages', session.user.id, event.conversationId] }); cache.removeQueries({ queryKey: ['conversation', session.user.id, event.conversationId] }); } };
    hubs[0].on('ParticipantRemoved', removed);
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
    return () => { disposed = true; listener.remove(); hubs[0].off('ParticipantRemoved', removed); hubs.forEach(h => { events.forEach(event => h.off(event, update)); void h.stop().catch(() => {}); }); };
  }, [session?.user.id, cache]);
  return children;
}
