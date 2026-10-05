import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { Alert } from 'react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import CreatePost from '../../app/posts/create';
import CreateMedia from '../../app/media/create';
import { clearSession, saveSession } from '../../auth/session';
import { pickMedia, uploadMedia } from '../../api/media';
import { postsApi } from '../../api/posts';
import { router } from 'expo-router';
import { storiesApi } from '../../api/stories';
let mockParams: { groupId?: string; kind?: string } = {};
let mockPrevented = false;
let mockLeave: (event: { data: { action: object } }) => void;
const mockDispatch = jest.fn();
jest.mock('expo-router', () => ({ router: { replace: jest.fn() }, useLocalSearchParams: () => mockParams, useNavigation: () => ({ dispatch: mockDispatch }) }));
jest.mock('expo-router/react-navigation', () => ({ usePreventRemove: (prevent: boolean, callback: typeof mockLeave) => { mockPrevented = prevent; mockLeave = callback; } }));
jest.mock('expo-secure-store', () => ({ setItemAsync: jest.fn(), deleteItemAsync: jest.fn() }));
jest.mock('react-native-safe-area-context', () => ({ SafeAreaView: require('react-native').View }));
jest.mock('../../auth/AuthProvider', () => ({ useAuth: () => ({ session: require('../../auth/session').getSession() }) }));
jest.mock('../../api/media', () => ({ pickMedia: jest.fn(), uploadMedia: jest.fn() }));
jest.mock('../../api/posts', () => ({ postsApi: { create: jest.fn() } }));
jest.mock('../../api/groups', () => ({ groupsApi: { createPost: jest.fn() } }));
jest.mock('../../api/stories', () => ({ storiesApi: { create: jest.fn() } }));
jest.mock('../../api/reels', () => ({ reelsApi: { create: jest.fn() } }));
jest.mock('expo-video', () => ({ useVideoPlayer: () => ({ pause: jest.fn() }), VideoView: require('react-native').View }));
const account = (id: string) => ({ user: { id, email: null, phoneNumber: null, username: id, emailConfirmed: false, phoneNumberConfirmed: false, roles: [] }, accessToken: 'access', refreshToken: 'refresh', accessTokenExpiresAt: '2030-01-01', refreshTokenExpiresAt: '2031-01-01' });
const photo = { uri: 'file:///photo.jpg', name: 'photo.jpg', mimeType: 'image/jpeg', sizeBytes: 1024 };
function setup(Component = CreatePost) {
  const cache = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity }, mutations: { gcTime: Infinity } } });
  const tree = () => <QueryClientProvider client={cache}><Component /></QueryClientProvider>;
  const view = render(tree()); return { ...view, refresh: () => view.rerender(tree()) };
}
beforeEach(async () => {
  jest.clearAllMocks(); mockParams = {}; mockPrevented = false;
  await clearSession(); await saveSession(account('u1'));
  jest.mocked(pickMedia).mockResolvedValue(photo);
  jest.mocked(uploadMedia).mockResolvedValue('media-1');
  jest.mocked(postsApi.create).mockResolvedValue({ id: 'p1' } as never);
  jest.mocked(storiesApi.create).mockResolvedValue({ id: 's1' } as never);
  jest.spyOn(Alert, 'alert');
});
afterEach(async () => { await act(async () => { await new Promise(resolve => setTimeout(resolve, 30)); }); jest.restoreAllMocks(); });
test('post drafts restore text, privacy and files after leaving and remain isolated by group', async () => {
  let view = setup(); fireEvent.changeText(screen.getByLabelText('Bạn đang nghĩ gì?'), 'Bản nháp');
  fireEvent.press(screen.getByText('Chỉ mình tôi')); fireEvent.press(screen.getByText('Thêm ảnh / video')); await screen.findByText('Bỏ photo.jpg');
  expect(mockPrevented).toBe(true);
  act(() => mockLeave({ data: { action: { type: 'GO_BACK' } } }));
  const buttons = jest.mocked(Alert.alert).mock.calls.at(-1)?.[2];
  act(() => buttons?.find(button => button.text === 'Rời và giữ bản nháp')?.onPress?.());
  expect(mockDispatch).toHaveBeenCalledWith({ type: 'GO_BACK' }); view.unmount();
  mockParams = { groupId: 'g1' }; view = setup(); expect(screen.getByLabelText('Bạn đang nghĩ gì?').props.value).toBe(''); view.unmount();
  mockParams = {}; setup(); expect(screen.getByLabelText('Bạn đang nghĩ gì?').props.value).toBe('Bản nháp'); expect(screen.getByText('Bỏ photo.jpg')).toBeTruthy();
  fireEvent.press(screen.getByText('Đăng bài')); await waitFor(() => expect(postsApi.create).toHaveBeenCalledWith({ content: 'Bản nháp', privacy: 'onlyMe', mediaIds: ['media-1'] }));
});
test('logout and switching accounts erase old post drafts', async () => {
  let view = setup(); fireEvent.changeText(screen.getByLabelText('Bạn đang nghĩ gì?'), 'Riêng tư'); view.unmount();
  await saveSession(account('u2')); view = setup(); expect(screen.getByLabelText('Bạn đang nghĩ gì?').props.value).toBe(''); view.unmount();
  await saveSession(account('u1')); view = setup(); expect(screen.getByLabelText('Bạn đang nghĩ gì?').props.value).toBe('');
  fireEvent.changeText(screen.getByLabelText('Bạn đang nghĩ gì?'), 'Sau đăng nhập'); view.unmount();
  await clearSession(); await saveSession(account('u1')); setup(); expect(screen.getByLabelText('Bạn đang nghĩ gì?').props.value).toBe('');
});
test('failed post keeps draft and successful retry clears it', async () => {
  jest.mocked(postsApi.create).mockRejectedValueOnce(new Error('Lỗi đăng bài'));
  let view = setup(); fireEvent.changeText(screen.getByLabelText('Bạn đang nghĩ gì?'), 'Nội dung');
  fireEvent.press(screen.getByText('Đăng bài')); await screen.findByText('Lỗi đăng bài'); view.unmount();
  view = setup(); expect(screen.getByLabelText('Bạn đang nghĩ gì?').props.value).toBe('Nội dung');
  fireEvent.press(screen.getByText('Đăng bài')); await waitFor(() => expect(postsApi.create).toHaveBeenCalledTimes(2));
  await waitFor(() => expect(screen.getByLabelText('Bạn đang nghĩ gì?').props.value).toBe('')); view.unmount();
  setup(); expect(screen.getByLabelText('Bạn đang nghĩ gì?').props.value).toBe('');
});
test('canceled media replacement preserves selected file and story draft stays separate from reel', async () => {
  mockParams = { kind: 'story' }; let view = setup(CreateMedia);
  fireEvent.changeText(screen.getByLabelText('Chú thích'), 'Tin nháp'); fireEvent.press(screen.getByText('Chọn ảnh / video')); await screen.findByText('Đổi photo.jpg');
  jest.mocked(pickMedia).mockResolvedValue(null); fireEvent.press(screen.getByText('Đổi photo.jpg')); await act(async () => {});
  expect(screen.getByText('Đổi photo.jpg')).toBeTruthy(); view.unmount();
  mockParams = { kind: 'reel' }; view = setup(CreateMedia); expect(screen.getByLabelText('Chú thích').props.value).toBe(''); expect(screen.getByText('Chọn ảnh / video')).toBeTruthy(); view.unmount();
  mockParams = { kind: 'story' }; setup(CreateMedia); expect(screen.getByLabelText('Chú thích').props.value).toBe('Tin nháp'); expect(screen.getByText('Đổi photo.jpg')).toBeTruthy();
});
test('media upload locks caption and privacy and reports only completed file progress', async () => {
  mockParams = { kind: 'story' }; let finish!: (id: string) => void;
  jest.mocked(uploadMedia).mockImplementationOnce(() => new Promise(resolve => { finish = resolve; }));
  setup(CreateMedia); fireEvent.press(screen.getByText('Chọn ảnh / video')); await screen.findByText('Đổi photo.jpg'); fireEvent.press(screen.getByText('Đăng'));
  await waitFor(() => expect(screen.getByLabelText('Chú thích').props.editable).toBe(false));
  expect(screen.getByRole('button', { name: 'Công khai' })).toBeDisabled(); expect(screen.getByText('Đang tải tệp 1/1 · Đã xong 0/1')).toBeTruthy();
  await act(async () => finish('media-1')); await waitFor(() => expect(storiesApi.create).toHaveBeenCalledWith('media-1', '', 'friends'));
});
test('explicit discard removes stored media draft', async () => {
  mockParams = { kind: 'story' }; const view = setup(CreateMedia); fireEvent.changeText(screen.getByLabelText('Chú thích'), 'Bỏ tin');
  fireEvent.press(screen.getByText('Bỏ bản nháp')); const buttons = jest.mocked(Alert.alert).mock.calls.at(-1)?.[2];
  act(() => buttons?.find(button => button.text === 'Bỏ bản nháp')?.onPress?.()); view.unmount(); setup(CreateMedia);
  expect(screen.getByLabelText('Chú thích').props.value).toBe('');
});

test('late post success after account switch preserves a new draft and does not navigate', async () => {
  let finish!: (value: { id: string }) => void;
  jest.mocked(postsApi.create).mockImplementationOnce(() => new Promise(resolve => { finish = resolve as never; }));
  const view = setup(); fireEvent.changeText(screen.getByLabelText('Bạn đang nghĩ gì?'), 'Cũ'); fireEvent.press(screen.getByText('Đăng bài'));
  await waitFor(() => expect(postsApi.create).toHaveBeenCalledTimes(1)); await act(async () => { await saveSession(account('u2')); }); view.refresh();
  fireEvent.changeText(screen.getByLabelText('Bạn đang nghĩ gì?'), 'Mới'); await act(async () => finish({ id: 'old-post' }));
  expect(screen.getByLabelText('Bạn đang nghĩ gì?').props.value).toBe('Mới'); expect(router.replace).not.toHaveBeenCalled();
});
test('switching group while post submits restores an independent editable composer', async () => {
  let finish!: (value: { id: string }) => void;
  jest.mocked(postsApi.create).mockImplementationOnce(() => new Promise(resolve => { finish = resolve as never; }));
  const view = setup(); fireEvent.changeText(screen.getByLabelText('Bạn đang nghĩ gì?'), 'Feed cũ'); fireEvent.press(screen.getByText('Đăng bài'));
  await waitFor(() => expect(postsApi.create).toHaveBeenCalledTimes(1)); mockParams = { groupId: 'g2' }; view.refresh();
  expect(screen.getByLabelText('Bạn đang nghĩ gì?')).toBeEnabled(); expect(screen.getByLabelText('Bạn đang nghĩ gì?').props.value).toBe('');
  fireEvent.changeText(screen.getByLabelText('Bạn đang nghĩ gì?'), 'Nhóm mới'); await act(async () => finish({ id: 'old-post' }));
  expect(screen.getByLabelText('Bạn đang nghĩ gì?').props.value).toBe('Nhóm mới'); expect(router.replace).not.toHaveBeenCalled();
});

test('retry after second post attachment fails reuses the completed upload and uploads only the remaining file', async () => {
  const second = { ...photo, uri: 'file:///second.jpg', name: 'second.jpg' };
  jest.mocked(pickMedia).mockResolvedValueOnce(photo).mockResolvedValueOnce(second);
  jest.mocked(uploadMedia).mockResolvedValueOnce('media-1').mockRejectedValueOnce(new Error('Lỗi ảnh thứ hai')).mockResolvedValueOnce('media-2');
  const view = setup(); fireEvent.changeText(screen.getByLabelText('Bạn đang nghĩ gì?'), 'Hai ảnh');
  fireEvent.press(screen.getByText('Thêm ảnh / video')); await screen.findByText('Bỏ photo.jpg');
  fireEvent.press(screen.getByText('Thêm ảnh / video')); await screen.findByText('Bỏ second.jpg');
  fireEvent.press(screen.getByText('Đăng bài')); await screen.findByText('Lỗi ảnh thứ hai');
  expect(postsApi.create).not.toHaveBeenCalled(); view.unmount(); setup(); fireEvent.press(screen.getByText('Đăng bài'));
  await waitFor(() => expect(postsApi.create).toHaveBeenCalledWith({ content: 'Hai ảnh', privacy: 'friends', mediaIds: ['media-1', 'media-2'] }));
  expect(jest.mocked(uploadMedia).mock.calls.map(([file]) => file.name)).toEqual(['photo.jpg', 'second.jpg', 'second.jpg']);
});
