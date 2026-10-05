import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { router } from 'expo-router';
import Search from '../../app/search';
import { searchApi, type GlobalSearchResult } from '../../api/search';

jest.mock('expo-router', () => ({ router: { push: jest.fn() } }));
jest.mock('react-native-safe-area-context', () => ({ SafeAreaView: require('react-native').View }));
let mockAccount: string | undefined = 'account-1';
jest.mock('../../auth/AuthProvider', () => ({ useAuth: () => ({ session: mockAccount ? { user: { id: mockAccount } } : null }) }));
jest.mock('../../api/search', () => ({ searchApi: { search: jest.fn(), suggestions: jest.fn() } }));

const empty: GlobalSearchResult = { people: [], groups: [], pages: [], posts: [], reels: [], events: [], hashtags: [], nextCursor: null };
const person = { userId: 'u1', username: 'an', displayName: 'An Nguyễn', avatarUrl: null, bio: null, followerCount: 4, followingCount: 1, isFollowing: false, isFollowedBy: false, friendshipState: null };
const all: GlobalSearchResult = {
  ...empty,
  people: [person],
  groups: [{ groupId: 'g1', name: 'Nhóm đọc sách', description: 'Cùng đọc', privacy: 'public', coverUrl: null, memberCount: 8, viewerMembershipState: null }],
  pages: [{ pageId: 'page1', name: 'Trang sách', username: 'sach', category: 'Books', bio: null, avatarUrl: null, followerCount: 10, viewerIsFollowing: false }],
  posts: [{ postId: 'post1', authorUserId: 'u1', displayAuthor: { type: 'user', id: 'u1', username: 'an', name: 'An Nguyễn', avatarUrl: null }, snippet: 'Một cuốn sách hay', mediaIds: [], commentCount: 2, reactionCounts: {}, containerType: 'profile', containerId: 'u1', createdAtUtc: '2026-10-01T00:00:00Z' }],
  reels: [{ reelId: 'r1', author: { userId: 'u1', username: 'an', displayName: 'An Nguyễn', avatarUrl: null }, snippet: 'Video đọc sách', media: { mediaId: 'm1', durationMs: 1000, width: 10, height: 10, videoAccessPath: '/api/media/m1/video', posterAccessPath: '/api/media/m1/poster' }, commentCount: 0, reactionCount: 1, viewCount: 2, createdAtUtc: '2026-10-01T00:00:00Z' }],
  events: [{ eventId: 'e1', name: 'Ngày hội sách', hostType: 'page', hostId: 'page1', hostName: 'Trang sách', startsAtUtc: '2026-10-10T00:00:00Z', locationType: 'physical', locationName: 'Hà Nội', coverUrl: null, goingCount: 9, interestedCount: 3 }],
  hashtags: [{ tag: 'docsach', displayName: 'ĐọcSách' }],
};
let cache: QueryClient;
beforeEach(() => {
  jest.useFakeTimers(); jest.clearAllMocks(); mockAccount = 'account-1';
  cache = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity } } });
  jest.mocked(searchApi.search).mockResolvedValue(empty);
  jest.mocked(searchApi.suggestions).mockResolvedValue({ people: [], groups: [], pages: [] });
});
afterEach(() => { cleanup(); cache.clear(); act(() => jest.runOnlyPendingTimers()); jest.useRealTimers(); });
function mount() { return render(<QueryClientProvider client={cache}><Search /></QueryClientProvider>); }
function submit(value = 'sách') {
  fireEvent.changeText(screen.getByLabelText('Từ khóa tìm kiếm'), value);
  fireEvent(screen.getByLabelText('Từ khóa tìm kiếm'), 'submitEditing');
}
function deferred<T>() { let resolve!: (value: T) => void; const promise = new Promise<T>(done => { resolve = done; }); return { promise, resolve }; }

test('shows all supported kinds and opens real destinations; hashtag searches posts', async () => {
  jest.mocked(searchApi.search).mockImplementation(async (_text, type) => type === 'posts' ? { ...empty, posts: all.posts } : all);
  mount(); submit();
  await screen.findByText('Nhóm đọc sách');
  expect(screen.getByText('Trang sách')).toBeTruthy();
  expect(screen.getByText('Ngày hội sách')).toBeTruthy();
  expect(screen.getByText('Video đọc sách')).toBeTruthy();
  for (const [label, path] of [['Xem hồ sơ An Nguyễn', '/profile/u1'], ['Xem nhóm Nhóm đọc sách', '/groups/g1'], ['Xem bài viết Một cuốn sách hay', '/posts/post1'], ['Khám phá Trang Trang sách', '/pages?pageId=page1'], ['Khám phá sự kiện Ngày hội sách', '/events?eventId=e1'], ['Xem Reels Video đọc sách', '/reels?reelId=r1']]) {
    fireEvent.press(screen.getByLabelText(label)); expect(router.push).toHaveBeenLastCalledWith(path);
  }
  fireEvent.press(screen.getByLabelText('Xem bài viết về #ĐọcSách'));
  await waitFor(() => expect(searchApi.search).toHaveBeenLastCalledWith('#docsach', 'posts', undefined, 20, expect.objectContaining({ signal: expect.anything() })));
  await waitFor(() => expect(screen.queryByLabelText('Đang tải')).toBeNull());
  expect(screen.getByText('Một cuốn sách hay')).toBeTruthy();
  expect(screen.queryByText('Nhóm đọc sách')).toBeNull();
  expect(screen.getByLabelText('Từ khóa tìm kiếm').props.value).toBe('#docsach');
});

test('type filtering makes a new query and hides previous kinds', async () => {
  jest.mocked(searchApi.search).mockImplementation(async (_text, type) => type === 'groups' ? { ...empty, groups: all.groups } : all);
  mount(); submit(); await screen.findByText('Nhóm đọc sách');
  fireEvent.press(screen.getByRole('button', { name: 'Nhóm' }));
  await waitFor(() => expect(searchApi.search).toHaveBeenLastCalledWith('sách', 'groups', undefined, 20, expect.anything()));
  await screen.findByText('Nhóm đọc sách');
  expect(screen.queryByText('An Nguyễn')).toBeNull();
});

test('does not report empty during loading or failure and retry recovers', async () => {
  const pending = deferred<GlobalSearchResult>();
  jest.mocked(searchApi.search).mockReturnValueOnce(pending.promise).mockRejectedValueOnce(new Error('Mất kết nối')).mockResolvedValueOnce(all);
  mount(); submit(); expect(screen.getByLabelText('Đang tải')).toBeTruthy();
  expect(screen.queryByText('Không tìm thấy kết quả.')).toBeNull();
  await act(async () => { pending.resolve(empty); });
  await screen.findByText('Không tìm thấy kết quả.');
  submit('truyện'); await screen.findByText('Mất kết nối');
  expect(screen.queryByText('Không tìm thấy kết quả.')).toBeNull();
  fireEvent.press(screen.getByText('Thử lại')); await screen.findByText('Nhóm đọc sách');
});

test('late old query cannot replace the latest submitted results', async () => {
  const old = deferred<GlobalSearchResult>();
  jest.mocked(searchApi.search).mockImplementation((term) => term === 'cũ' ? old.promise : Promise.resolve({ ...empty, people: [{ ...person, displayName: 'Kết quả mới' }] }));
  mount(); submit('cũ'); await waitFor(() => expect(searchApi.search).toHaveBeenCalled());
  submit('mới'); await screen.findByText('Kết quả mới');
  await act(async () => { old.resolve(all); });
  expect(screen.queryByText('Nhóm đọc sách')).toBeNull(); expect(screen.getByText('Kết quả mới')).toBeTruthy();
});

test('account switch resets search and cannot reveal previous account results', async () => {
  const old = deferred<GlobalSearchResult>();
  jest.mocked(searchApi.search).mockReturnValueOnce(old.promise).mockResolvedValueOnce(empty);
  const view = mount(); submit(); await waitFor(() => expect(searchApi.search).toHaveBeenCalledTimes(1));
  mockAccount = 'account-2'; view.rerender(<QueryClientProvider client={cache}><Search /></QueryClientProvider>);
  expect(screen.getByLabelText('Từ khóa tìm kiếm').props.value).toBe('');
  await act(async () => { old.resolve(all); });
  expect(screen.queryByText('An Nguyễn')).toBeNull();
  submit(); await screen.findByText('Không tìm thấy kết quả.');
  expect(searchApi.search).toHaveBeenCalledTimes(2);
});

test('cursor pages dedupe records and retry the failed page without losing existing results', async () => {
  jest.mocked(searchApi.search).mockResolvedValueOnce({ ...empty, people: [person], nextCursor: 'next1' }).mockRejectedValueOnce(new Error('Lỗi trang sau')).mockResolvedValueOnce({ ...empty, people: [person, { ...person, userId: 'u2', displayName: 'Bình' }] });
  mount(); fireEvent.press(screen.getByRole('button', { name: 'Mọi người' })); submit(); await screen.findByText('An Nguyễn');
  fireEvent.press(screen.getByText('Xem thêm')); await screen.findByText('Lỗi trang sau');
  expect(screen.getByText('An Nguyễn')).toBeTruthy();
  fireEvent.press(screen.getByText('Thử lại')); await screen.findByText('Bình');
  expect(screen.getAllByText('An Nguyễn')).toHaveLength(1);
  expect(searchApi.search).toHaveBeenLastCalledWith('sách', 'people', 'next1', 20, expect.anything());
});

test('suggestions debounce typing and open a suggested profile', async () => {
  jest.useFakeTimers(); jest.mocked(searchApi.suggestions).mockResolvedValue({ people: [person], groups: [], pages: [] });
  mount(); fireEvent.changeText(screen.getByLabelText('Từ khóa tìm kiếm'), 'a');
  await act(async () => { jest.advanceTimersByTime(400); });
  expect(searchApi.suggestions).not.toHaveBeenCalled();
  fireEvent.changeText(screen.getByLabelText('Từ khóa tìm kiếm'), 'an');
  await act(async () => { jest.advanceTimersByTime(200); });
  expect(searchApi.suggestions).not.toHaveBeenCalled();
  fireEvent.changeText(screen.getByLabelText('Từ khóa tìm kiếm'), 'anh');
  await act(async () => { jest.advanceTimersByTime(300); });
  await screen.findByText('An Nguyễn');
  expect(searchApi.suggestions).toHaveBeenCalledTimes(1);
  expect(searchApi.suggestions).toHaveBeenLastCalledWith('anh', 5, expect.anything());
  fireEvent.press(screen.getByLabelText('Xem hồ sơ An Nguyễn')); expect(router.push).toHaveBeenCalledWith('/profile/u1');
});

test('clearing text removes old results and short queries cannot be submitted', async () => {
  jest.mocked(searchApi.search).mockResolvedValue(all); mount(); submit(); await screen.findByText('Nhóm đọc sách');
  fireEvent.changeText(screen.getByLabelText('Từ khóa tìm kiếm'), '');
  expect(screen.queryByText('Nhóm đọc sách')).toBeNull();
  submit('a'); expect(searchApi.search).toHaveBeenCalledTimes(1);
});
