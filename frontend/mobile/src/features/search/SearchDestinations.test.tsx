import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { ScrollView } from 'react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { router } from 'expo-router';
import Pages from '../../app/pages';
import Events from '../../app/events';
import Reels from '../../app/reels';
import { pagesApi, type Page } from '../../api/pages';
import { eventsApi, type Event } from '../../api/events';
import { reelsApi, type Reel } from '../../api/reels';

let mockParams: Record<string, string> = {};
let mockAccount = 'a1';
jest.mock('expo-router', () => ({ router: { push: jest.fn(), back: jest.fn() }, useLocalSearchParams: () => mockParams }));
jest.mock('react-native-safe-area-context', () => ({ SafeAreaView: require('react-native').View }));
jest.mock('../../auth/AuthProvider', () => ({ useAuth: () => ({ session: { user: { id: mockAccount } } }) }));
jest.mock('../../api/pages', () => ({ pagesApi: { get: jest.fn(), discover: jest.fn(), invitationsMine: jest.fn(), follow: jest.fn(), unfollow: jest.fn() } }));
jest.mock('../../api/events', () => ({ eventsApi: { get: jest.fn(), upcoming: jest.fn(), invitations: jest.fn(), rsvp: jest.fn(), removeRsvp: jest.fn() } }));
jest.mock('../../api/reels', () => ({ reelsApi: { get: jest.fn(), getFeed: jest.fn() } }));
jest.mock('../../components/MediaView', () => ({ MediaView: () => null }));

const page: Page = { id: 'p1', name: 'Trang đã chọn', username: 'selected', category: 'Books', bio: 'Giới thiệu Trang', status: 'published', avatarUrl: null, coverUrl: null, followerCount: 3, isFollowing: false, viewerRole: null, createdAtUtc: '2026-10-01', updatedAtUtc: null };
const event: Event = { id: 'e1', name: 'Sự kiện đã chọn', description: 'Giới thiệu sự kiện', displayHost: { type: 'page', id: 'p1', name: 'Chủ sự kiện' }, privacy: 'public', locationType: 'physical', locationName: 'Hà Nội', address: null, onlineUrl: null, startsAtUtc: '2026-10-10T00:00:00Z', endsAtUtc: null, status: 'published', coverUrl: null, goingCount: 2, interestedCount: 1, viewerRsvpStatus: null, canManage: false, canPost: false, createdAtUtc: '2026-10-01', updatedAtUtc: null };
const reel = { id: 'r1', caption: 'Reel đã chọn', author: { userId: 'u1', username: 'an', displayName: 'An', avatarUrl: null }, reactionCount: 2, viewerHasSaved: false, viewerFollowsAuthor: false } as Reel;
let cache: QueryClient;
beforeEach(() => {
  jest.clearAllMocks(); mockParams = {}; mockAccount = 'a1';
  cache = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity }, mutations: { retry: false, gcTime: Infinity } } });
  jest.mocked(pagesApi.get).mockResolvedValue(page);
  jest.mocked(pagesApi.discover).mockResolvedValue({ items: [page], nextCursor: null });
  jest.mocked(pagesApi.invitationsMine).mockResolvedValue({ items: [], nextCursor: null });
  jest.mocked(eventsApi.get).mockResolvedValue(event);
  jest.mocked(eventsApi.upcoming).mockResolvedValue({ items: [event], nextCursor: null });
  jest.mocked(eventsApi.invitations).mockResolvedValue({ items: [], nextCursor: null });
  jest.mocked(reelsApi.get).mockResolvedValue(reel);
  jest.mocked(reelsApi.getFeed).mockResolvedValue({ items: [{ ...reel, id: 'r2', caption: 'Reel trong feed' }], nextCursor: null });
});
afterEach(() => { cleanup(); cache.clear(); jest.restoreAllMocks(); });
function tree(component: React.ReactNode) { return <QueryClientProvider client={cache}>{component}</QueryClientProvider>; }

test('selected page is highlighted and scrolled into view even outside discovery page', async () => {
  mockParams = { pageId: 'p1' };
  jest.mocked(pagesApi.discover).mockResolvedValue({ items: [], nextCursor: null });
  const scroll = jest.spyOn(ScrollView.prototype, 'scrollTo');
  render(tree(<Pages />));
  await screen.findByText('Trang đã chọn');
  expect(pagesApi.get).toHaveBeenCalledWith('p1');
  fireEvent(screen.getByLabelText('Trang đã chọn từ tìm kiếm'), 'layout', { nativeEvent: { layout: { y: 260 } } });
  expect(scroll).toHaveBeenCalledWith({ y: 260, animated: true });
  fireEvent.press(screen.getByText('Theo dõi'));
  await waitFor(() => expect(pagesApi.follow).toHaveBeenCalledWith('p1'));
});

test('selected event loads real data once and RSVP targets the selected event', async () => {
  mockParams = { eventId: 'e1' };
  const scroll = jest.spyOn(ScrollView.prototype, 'scrollTo');
  render(tree(<Events />));
  await screen.findByText('Sự kiện đã chọn');
  expect(screen.getAllByText('Sự kiện đã chọn')).toHaveLength(1);
  expect(screen.getByLabelText('Sự kiện đã chọn từ tìm kiếm')).toBeTruthy();
  expect(eventsApi.get).toHaveBeenCalledWith('e1');
  fireEvent(screen.getByLabelText('Sự kiện đã chọn từ tìm kiếm'), 'layout', { nativeEvent: { layout: { y: 210 } } });
  expect(scroll).toHaveBeenCalledWith({ y: 210, animated: true });
  fireEvent.press(screen.getByText('Tham gia'));
  await waitFor(() => expect(eventsApi.rsvp).toHaveBeenCalledWith('e1', 'going'));
});

test('failed selected item has retry while preserving the discovery list', async () => {
  mockParams = { pageId: 'p1' };
  jest.mocked(pagesApi.get).mockRejectedValueOnce(new Error('Trang tạm thời không tải được')).mockResolvedValueOnce(page);
  jest.mocked(pagesApi.discover).mockResolvedValue({ items: [{ ...page, id: 'p2', name: 'Trang khám phá' }], nextCursor: null });
  render(tree(<Pages />));
  await screen.findByText('Trang tạm thời không tải được');
  expect(screen.getByText('Trang khám phá')).toBeTruthy();
  fireEvent.press(screen.getByText('Thử lại'));
  await screen.findByText('Trang đã chọn');
});

test('selected Reel plays first, opens its comments, then continues the real feed', async () => {
  mockParams = { reelId: 'r1' };
  render(tree(<Reels />));
  await screen.findByText('Reel đã chọn');
  expect(reelsApi.get).toHaveBeenCalledWith('r1');
  expect(screen.queryByText('Reel trong feed')).toBeNull();
  fireEvent.press(screen.getByLabelText('Bình luận Reel'));
  expect(router.push).toHaveBeenLastCalledWith('/posts/r1');
  fireEvent.press(screen.getByLabelText('Reel tiếp theo'));
  await screen.findByText('Reel trong feed');
});

test('failed selected Reel shows retry instead of playing an unrelated feed result', async () => {
  mockParams = { reelId: 'r1' };
  jest.mocked(reelsApi.get).mockRejectedValueOnce(new Error('Không tải được Reel đã chọn')).mockResolvedValueOnce(reel);
  render(tree(<Reels />));
  await screen.findByText('Không tải được Reel đã chọn');
  expect(screen.queryByText('Reel trong feed')).toBeNull();
  fireEvent.press(screen.getByText('Thử lại'));
  await screen.findByText('Reel đã chọn');
});

test('duplicate cursor records cannot advance selected Reel playback past the real feed', async () => {
  mockParams = { reelId: 'r1' };
  const feedReel = { ...reel, id: 'r2', caption: 'Reel trong feed' };
  jest.mocked(reelsApi.getFeed).mockResolvedValueOnce({ items: [reel, feedReel], nextCursor: 'next' }).mockResolvedValueOnce({ items: [feedReel], nextCursor: null });
  render(tree(<Reels />));
  await screen.findByText('Reel đã chọn');
  fireEvent.press(screen.getByLabelText('Reel tiếp theo'));
  await screen.findByText('Reel trong feed');
  fireEvent.press(screen.getByLabelText('Reel tiếp theo'));
  await waitFor(() => expect(reelsApi.getFeed).toHaveBeenCalledWith('forYou', 'next'));
  await waitFor(() => expect(screen.getByLabelText('Reel tiếp theo').props.accessibilityState.disabled).toBe(true));
  expect(screen.getByText('Reel trong feed')).toBeTruthy();
});

test('late cursor response from the previous Reel mode cannot advance the new mode', async () => {
  let resolve!: (page: { items: Reel[]; nextCursor: null }) => void;
  const pending = new Promise<{ items: Reel[]; nextCursor: null }>(done => { resolve = done; });
  jest.mocked(reelsApi.getFeed).mockImplementation((mode, cursor) => {
    if (mode === 'following') return Promise.resolve({ items: [{ ...reel, id: 'f1', caption: 'Theo dõi đầu tiên' }, { ...reel, id: 'f2', caption: 'Theo dõi tiếp theo' }], nextCursor: null });
    if (cursor) return pending;
    return Promise.resolve({ items: [reel], nextCursor: 'next' });
  });
  render(tree(<Reels />));
  await screen.findByText('Reel đã chọn');
  fireEvent.press(screen.getByLabelText('Reel tiếp theo'));
  await waitFor(() => expect(reelsApi.getFeed).toHaveBeenCalledWith('forYou', 'next'));
  fireEvent.press(screen.getByRole('tab', { name: 'Đang theo dõi' }));
  await screen.findByText('Theo dõi đầu tiên');
  await act(async () => resolve({ items: [{ ...reel, id: 'r2', caption: 'Phản hồi cũ' }], nextCursor: null }));
  expect(screen.getByText('Theo dõi đầu tiên')).toBeTruthy();
  expect(screen.queryByText('Theo dõi tiếp theo')).toBeNull();
});

test('changing mode after opening a selected Reel shows loading for the new feed', async () => {
  mockParams = { reelId: 'r1' };
  jest.mocked(reelsApi.getFeed).mockImplementation(mode => mode === 'following' ? new Promise(() => {}) : Promise.resolve({ items: [reel], nextCursor: null }));
  render(tree(<Reels />));
  await screen.findByText('Reel đã chọn');
  fireEvent.press(screen.getByRole('tab', { name: 'Đang theo dõi' }));
  await waitFor(() => expect(reelsApi.getFeed).toHaveBeenCalledWith('following', undefined));
  expect(screen.getByLabelText('Đang tải')).toBeTruthy();
  expect(screen.queryByText('Reel đã chọn')).toBeNull();
});

test('late selected page response cannot leak into the next route or account', async () => {
  let resolve!: (value: Page) => void;
  jest.mocked(pagesApi.get).mockImplementationOnce(() => new Promise(done => { resolve = done; })).mockResolvedValueOnce({ ...page, id: 'p2', name: 'Trang mới' });
  jest.mocked(pagesApi.discover).mockResolvedValue({ items: [], nextCursor: null });
  mockParams = { pageId: 'p1' };
  const view = render(tree(<Pages />));
  await waitFor(() => expect(pagesApi.get).toHaveBeenCalled());
  mockParams = { pageId: 'p2' }; mockAccount = 'a2'; view.rerender(tree(<Pages />));
  await screen.findByText('Trang mới');
  await act(async () => resolve(page));
  expect(screen.queryByText('Trang đã chọn')).toBeNull();
  expect(screen.getByText('Trang mới')).toBeTruthy();
});
