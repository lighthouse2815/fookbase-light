import { act, render, waitFor } from '@testing-library/react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RealtimeProvider } from './RealtimeProvider';

const mockHubs: { on: jest.Mock; off: jest.Mock; start: jest.Mock; stop: jest.Mock; onreconnected: jest.Mock; state: string }[] = [];
jest.mock('@microsoft/signalr', () => ({
  HubConnectionState: { Disconnected: 'disconnected' },
  HubConnectionBuilder: class {
    withUrl() { return this; } withAutomaticReconnect() { return this; }
    build() { const hub = { on: jest.fn(), off: jest.fn(), start: jest.fn().mockResolvedValue(undefined), stop: jest.fn().mockResolvedValue(undefined), onreconnected: jest.fn(), state: 'disconnected' }; mockHubs.push(hub); return hub; }
  },
}));
jest.mock('../auth/AuthProvider', () => ({ useAuth: () => ({ session: { user: { id: 'u1' } } }) }));
jest.mock('../auth/session', () => ({ getSession: () => ({ user: { id: 'u1' }, accessToken: 'token' }), refreshSession: jest.fn().mockResolvedValue('token') }));
jest.mock('../config/env', () => ({ getApiBaseUrl: () => 'https://api.example.test' }));
test('unmount removes both hubs and every registered handler', async () => {
  const cache = new QueryClient();
  const view = render(<QueryClientProvider client={cache}><RealtimeProvider><></></RealtimeProvider></QueryClientProvider>);
  await waitFor(() => expect(mockHubs[0].start).toHaveBeenCalledTimes(1));
  await act(async () => view.unmount());
  expect(mockHubs).toHaveLength(2);
  for (const hub of mockHubs) { expect(hub.stop).toHaveBeenCalled(); expect(hub.off).toHaveBeenCalledTimes(hub.on.mock.calls.length); }
});

test('membership removal clears cached private messages', async () => {
  mockHubs.length = 0;
  const cache = new QueryClient();
  cache.setQueryData(['messages', 'u1', 'c1'], { secret: true });
  const view = render(<QueryClientProvider client={cache}><RealtimeProvider><></></RealtimeProvider></QueryClientProvider>);
  await waitFor(() => expect(mockHubs[0].start).toHaveBeenCalled());
  const handlers = mockHubs[0].on.mock.calls.filter(([name]) => name === 'ParticipantRemoved');
  await act(async () => handlers.forEach(([, handler]) => handler({ conversationId: 'c1', userId: 'u1' })));
  expect(cache.getQueryData(['messages', 'u1', 'c1'])).toBeUndefined();
  expect(mockHubs[0].on.mock.calls.some(([name]) => name === 'ConversationCreated')).toBe(true);
  view.unmount(); cache.clear();
});
