import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { AppState, FlatList, type AppStateStatus } from 'react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import ConversationScreen from '../../app/conversations/[conversationId]';
import { messengerApi, type Message } from '../../api/messages';
let mockFocused = true;
jest.mock('expo-router', () => ({ router: { push: jest.fn() }, useLocalSearchParams: () => ({ conversationId: 'c1' }) }));
jest.mock('expo-router/react-navigation', () => ({ useIsFocused: () => mockFocused }));
jest.mock('react-native-safe-area-context', () => ({ SafeAreaView: require('react-native').View }));
jest.mock('../../auth/AuthProvider', () => ({ useAuth: () => ({ session: { user: { id: 'u1' } } }) }));
jest.mock('../../api/media', () => ({ pickMedia: jest.fn(), uploadMedia: jest.fn() }));
jest.mock('../../components/MediaView', () => ({ MediaView: () => null }));
jest.mock('../../api/messages', () => ({ messengerApi: { conversation: jest.fn(), messages: jest.fn(), read: jest.fn(), send: jest.fn() } }));
const older = { id: 'm1', conversationId: 'c1', senderUserId: 'u2', content: 'Tin trước', createdAtUtc: '2026-10-05T00:00:00Z', readAtUtc: null, type: 'text', replyToMessageId: null, replyTo: null, editedAtUtc: null, deletedAtUtc: null, attachments: [], reactions: [], story: null } as Message;
const newer = { ...older, id: 'm2', content: 'Tin mới', createdAtUtc: '2026-10-05T01:00:00Z' };
let appChange: (state: AppStateStatus) => void;
beforeEach(() => {
  jest.useFakeTimers(); jest.clearAllMocks(); mockFocused = true; AppState.currentState = 'active';
  jest.spyOn(AppState, 'addEventListener').mockImplementation((_type, listener) => { appChange = listener; return { remove: jest.fn() }; });
  jest.mocked(messengerApi.conversation).mockResolvedValue({ id: 'c1', title: 'Hội thoại', participants: [] } as never);
  jest.mocked(messengerApi.messages).mockResolvedValue({ items: [older, newer], hasMore: true, nextCursor: 'previous' });
  jest.mocked(messengerApi.read).mockResolvedValue(undefined);
});
afterEach(() => { act(() => jest.runOnlyPendingTimers()); jest.useRealTimers(); jest.restoreAllMocks(); });
function setup() {
  const cache = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity }, mutations: { gcTime: Infinity } } });
  const tree = () => <QueryClientProvider client={cache}><ConversationScreen /></QueryClientProvider>;
  const view = render(tree());
  return { ...view, cache, refresh: () => view.rerender(tree()), visible: (...items: Message[]) => fireEvent(view.UNSAFE_getByType(FlatList), 'onViewableItemsChanged', { viewableItems: items.map(item => ({ item, isViewable: true })), changed: [] }) };
}
test('loaded messages outside viewport are not marked read', async () => {
  setup(); await screen.findByText('Tin mới');
  expect(messengerApi.read).not.toHaveBeenCalled();
});
test('marks only the newest visible incoming message read once', async () => {
  const view = setup(); await screen.findByText('Tin mới'); view.visible(older);
  await waitFor(() => expect(messengerApi.read).toHaveBeenCalledWith('c1', 'm1'));
  view.visible(older); expect(messengerApi.read).toHaveBeenCalledTimes(1);
});
test('unfocused screen waits until focused to mark a visible message', async () => {
  mockFocused = false; const view = setup(); await screen.findByText('Tin mới'); view.visible(older);
  expect(messengerApi.read).not.toHaveBeenCalled(); mockFocused = true; view.refresh();
  await waitFor(() => expect(messengerApi.read).toHaveBeenCalledWith('c1', 'm1'));
});
test('background screen waits for active AppState', async () => {
  const view = setup(); await screen.findByText('Tin mới'); act(() => appChange('background')); view.visible(older);
  expect(messengerApi.read).not.toHaveBeenCalled(); act(() => appChange('active'));
  await waitFor(() => expect(messengerApi.read).toHaveBeenCalledWith('c1', 'm1'));
});
test('does not send simultaneous read requests when visible messages change', async () => {
  let finish!: () => void; jest.mocked(messengerApi.read).mockImplementationOnce(() => new Promise(resolve => { finish = resolve; }));
  const view = setup(); await screen.findByText('Tin mới'); view.visible(older); view.visible(newer);
  expect(messengerApi.read).toHaveBeenCalledTimes(1); await act(async () => finish());
  await waitFor(() => expect(messengerApi.read).toHaveBeenLastCalledWith('c1', 'm2'));
});
test('failed read keeps unread cache and offers retry without hiding history', async () => {
  jest.mocked(messengerApi.read).mockRejectedValueOnce(new Error('Không ghi nhận được'));
  const view = setup(); view.cache.setQueryData(['conversations'], { unreadCount: 2 });
  await screen.findByText('Tin mới'); view.visible(older); await screen.findByText('Không ghi nhận được');
  expect(view.cache.getQueryState(['conversations'])?.isInvalidated).toBe(false);
  expect(screen.getByText('Tin trước')).toBeTruthy(); fireEvent.press(screen.getByText('Thử lại'));
  await waitFor(() => expect(messengerApi.read).toHaveBeenCalledTimes(2));
  await waitFor(() => expect(view.cache.getQueryState(['conversations'])?.isInvalidated).toBe(true));
});
test('history load failure preserves loaded messages and allows retry', async () => {
  setup(); await screen.findByText('Tin mới');
  jest.mocked(messengerApi.messages).mockRejectedValueOnce(new Error('Lỗi lịch sử'));
  fireEvent.press(screen.getByText('Tin nhắn trước')); await screen.findByText('Lỗi lịch sử');
  expect(screen.getByText('Tin mới')).toBeTruthy(); fireEvent.press(screen.getByText('Thử lại'));
  await waitFor(() => expect(messengerApi.messages).toHaveBeenCalledTimes(3));
});

test('own visible messages never trigger a read request', async () => {
  const own = { ...newer, senderUserId: 'u1' };
  jest.mocked(messengerApi.messages).mockResolvedValue({ items: [older, own], hasMore: false, nextCursor: null });
  const view = setup(); await screen.findByText('Tin mới'); view.visible(own);
  expect(messengerApi.read).not.toHaveBeenCalled();
});

test('reads distinct visible messages sharing a timestamp without moving backwards', async () => {
  const sameTime = { ...newer, createdAtUtc: older.createdAtUtc };
  jest.mocked(messengerApi.messages).mockResolvedValue({ items: [sameTime, older], hasMore: false, nextCursor: null });
  const view = setup(); await screen.findByText('Tin mới'); view.visible(older);
  await waitFor(() => expect(messengerApi.read).toHaveBeenCalledWith('c1', 'm1'));
  view.visible(sameTime); await waitFor(() => expect(messengerApi.read).toHaveBeenCalledWith('c1', 'm2'));
  view.visible(older); expect(messengerApi.read).toHaveBeenCalledTimes(2);
});
