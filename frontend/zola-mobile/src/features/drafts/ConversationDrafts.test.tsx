import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Alert } from 'react-native';
import ConversationScreen from '../../app/conversations/[conversationId]';
import { clearSession, saveSession } from '../../auth/session';
import { pickMedia, uploadMedia } from '../../api/media';
import { messengerApi } from '../../api/messages';
let mockConversationId = 'c1';
let mockPrevented = false;
const mockDispatch = jest.fn();
jest.mock('expo-router', () => ({ router: { push: jest.fn() }, useLocalSearchParams: () => ({ conversationId: mockConversationId }), useNavigation: () => ({ dispatch: mockDispatch }), useFocusEffect: jest.fn() }));
jest.mock('expo-router/react-navigation', () => ({ useIsFocused: () => true, usePreventRemove: (prevent: boolean) => { mockPrevented = prevent; } }));
jest.mock('expo-secure-store', () => ({ setItemAsync: jest.fn(), deleteItemAsync: jest.fn() }));
jest.mock('react-native-safe-area-context', () => ({ SafeAreaView: require('react-native').View }));
jest.mock('../../auth/AuthProvider', () => ({ useAuth: () => ({ session: require('../../auth/session').getSession() }) }));
jest.mock('../../api/media', () => ({ pickMedia: jest.fn(), uploadMedia: jest.fn() }));
jest.mock('../../components/MediaView', () => ({ MediaView: () => null }));
jest.mock('expo-video', () => ({ useVideoPlayer: () => ({ pause: jest.fn() }), VideoView: require('react-native').View }));
jest.mock('../../api/messages', () => ({ messengerApi: { conversation: jest.fn(), messages: jest.fn(), read: jest.fn(), send: jest.fn() } }));
const account = (id: string) => ({ user: { id, email: null, phoneNumber: null, username: id, emailConfirmed: false, phoneNumberConfirmed: false, roles: [] }, accessToken: 'access', refreshToken: 'refresh', accessTokenExpiresAt: '2030-01-01', refreshTokenExpiresAt: '2031-01-01' });
const file = { uri: 'file:///photo.jpg', name: 'photo.jpg', mimeType: 'image/jpeg', sizeBytes: 1024 };
function setup() {
  const cache = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity }, mutations: { gcTime: Infinity } } });
  return render(<QueryClientProvider client={cache}><ConversationScreen /></QueryClientProvider>);
}
beforeEach(async () => {
  jest.useFakeTimers(); jest.clearAllMocks(); mockConversationId = 'c1'; mockPrevented = false; await clearSession(); await saveSession(account('u1'));
  jest.mocked(messengerApi.conversation).mockResolvedValue({ id: 'c1', title: 'Hội thoại', participants: [] } as never);
  jest.mocked(messengerApi.messages).mockResolvedValue({ items: [], hasMore: false, nextCursor: null });
  jest.mocked(messengerApi.send).mockResolvedValue({ id: 'm1' } as never);
  jest.mocked(pickMedia).mockResolvedValue(file); jest.mocked(uploadMedia).mockResolvedValue('media-1'); jest.spyOn(Alert, 'alert');
});
afterEach(async () => { await act(async () => { jest.runOnlyPendingTimers(); }); jest.useRealTimers(); jest.restoreAllMocks(); });
test('conversation drafts restore text and attachment without leaking to another conversation', async () => {
  let view = setup(); fireEvent.changeText(screen.getByLabelText('Tin nhắn'), 'Tin nháp'); fireEvent.press(screen.getByText('Đính kèm')); await screen.findByText('Bỏ photo.jpg');
  expect(mockPrevented).toBe(true); view.unmount(); mockConversationId = 'c2'; view = setup(); expect(screen.getByLabelText('Tin nhắn').props.value).toBe(''); view.unmount();
  mockConversationId = 'c1'; setup(); expect(screen.getByLabelText('Tin nhắn').props.value).toBe('Tin nháp'); expect(screen.getByText('Bỏ photo.jpg')).toBeTruthy();
});
test('canceling chat picker preserves attachment and failed send survives reopening', async () => {
  let view = setup(); fireEvent.changeText(screen.getByLabelText('Tin nhắn'), 'Thử lại'); fireEvent.press(screen.getByText('Đính kèm')); await screen.findByText('Bỏ photo.jpg');
  jest.mocked(pickMedia).mockResolvedValue(null); fireEvent.press(screen.getByText('Đính kèm')); await act(async () => {}); expect(screen.getByText('Bỏ photo.jpg')).toBeTruthy();
  jest.mocked(messengerApi.send).mockRejectedValueOnce(new Error('Lỗi gửi tin')); fireEvent.press(screen.getByText('Gửi')); await screen.findByText('Lỗi gửi tin'); view.unmount();
  view = setup(); expect(screen.getByLabelText('Tin nhắn').props.value).toBe('Thử lại'); expect(screen.getByText('Bỏ photo.jpg')).toBeTruthy();
  fireEvent.press(screen.getByText('Gửi')); await waitFor(() => expect(screen.getByLabelText('Tin nhắn').props.value).toBe(''));
  expect(uploadMedia).toHaveBeenCalledTimes(1); expect(messengerApi.send).toHaveBeenLastCalledWith('c1', 'Thử lại', ['media-1']); view.unmount();
  setup(); expect(screen.getByLabelText('Tin nhắn').props.value).toBe(''); expect(screen.queryByText('Bỏ photo.jpg')).toBeNull();
});
test('account switching and logout clear chat drafts', async () => {
  let view = setup(); fireEvent.changeText(screen.getByLabelText('Tin nhắn'), 'Bí mật'); view.unmount(); await saveSession(account('u2'));
  view = setup(); expect(screen.getByLabelText('Tin nhắn').props.value).toBe(''); view.unmount(); await saveSession(account('u1'));
  view = setup(); expect(screen.getByLabelText('Tin nhắn').props.value).toBe(''); fireEvent.changeText(screen.getByLabelText('Tin nhắn'), 'Bí mật khác'); view.unmount();
  await clearSession(); await saveSession(account('u1')); setup(); expect(screen.getByLabelText('Tin nhắn').props.value).toBe('');
});
test('upload finishing after account switch never sends under the new session or restores cleared drafts', async () => {
  let finish!: (id: string) => void; jest.mocked(uploadMedia).mockImplementationOnce(() => new Promise(resolve => { finish = resolve; }));
  const view = setup(); fireEvent.changeText(screen.getByLabelText('Tin nhắn'), 'Cũ'); fireEvent.press(screen.getByText('Đính kèm')); await screen.findByText('Bỏ photo.jpg'); fireEvent.press(screen.getByText('Gửi'));
  await waitFor(() => expect(uploadMedia).toHaveBeenCalledTimes(1)); view.unmount(); await saveSession(account('u2')); await saveSession(account('u1'));
  await act(async () => finish('media-1')); expect(messengerApi.send).not.toHaveBeenCalled(); setup(); expect(screen.getByLabelText('Tin nhắn').props.value).toBe('');
});
