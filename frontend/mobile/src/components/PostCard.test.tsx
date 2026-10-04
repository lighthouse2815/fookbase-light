import { Alert } from 'react-native';
import { reportsApi } from '../api/reports';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { PostCard } from './PostCard';
import { postsApi, type Post } from '../api/posts';
jest.mock('expo-router', () => ({ router: { push: jest.fn() } }));
jest.mock('../auth/AuthProvider', () => ({ useAuth: () => ({ session: { user: { id: 'u1' } } }) }));
jest.mock('./MediaView', () => ({ MediaView: () => null }));
jest.mock('../api/posts', () => ({ postsApi: { getById: jest.fn(), removeSaved: jest.fn(), update: jest.fn(), delete: jest.fn() } }));
jest.mock('../api/reports', () => ({ reportsApi: { reportPost: jest.fn() } }));
beforeEach(() => jest.clearAllMocks());
test('feed without saved field resolves saved status and offers unsave', async () => {
  const post = { id: 'p1', createdAtUtc: new Date().toISOString(), mediaIds: [], reactionCounts: {}, content: 'Bài viết', privacy: 'public', commentCount: 0 } as unknown as Post;
  jest.mocked(postsApi.getById).mockResolvedValue({ ...post, viewerHasSaved: true });
  jest.mocked(postsApi.removeSaved).mockResolvedValue(undefined);
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity }, mutations: { gcTime: Infinity } } })}><PostCard post={post} /></QueryClientProvider>);
  fireEvent.press(await screen.findByText('Bỏ lưu'));
  await waitFor(() => expect(postsApi.removeSaved).toHaveBeenCalledWith('p1'));
});

const ownPost = { id: 'p2', authorUserId: 'u1', content: 'Bài viết của tôi', createdAtUtc: new Date().toISOString(), mediaIds: ['media-1'], reactionCounts: {}, privacy: 'friends', commentCount: 0, viewerHasSaved: false } as Post;
function renderPost(post: Post) {
  return render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity }, mutations: { gcTime: Infinity } } })}><PostCard post={post} /></QueryClientProvider>);
}
test('the options menu edits the real post while keeping its audience and attachments', async () => {
  jest.mocked(postsApi.update).mockResolvedValue(ownPost);
  renderPost(ownPost);
  fireEvent.press(screen.getByLabelText('Tùy chọn bài viết'));
  fireEvent.press(screen.getByText('Chỉnh sửa'));
  fireEvent.changeText(screen.getByLabelText('Nội dung bài viết'), 'Nội dung mới');
  fireEvent.press(screen.getByText('Lưu thay đổi'));
  await waitFor(() => expect(postsApi.update).toHaveBeenCalledWith('p2', { content: 'Nội dung mới', privacy: 'friends', mediaIds: ['media-1'], textBackground: undefined }));
  await waitFor(() => expect(screen.queryByText('Lưu thay đổi')).toBeNull());
});
test('editing a text post preserves its existing background', async () => {
  const post = { ...ownPost, mediaIds: [], textBackground: 'sunset' };
  jest.mocked(postsApi.update).mockResolvedValue(post);
  renderPost(post);
  fireEvent.press(screen.getByLabelText('Tùy chọn bài viết'));
  fireEvent.press(screen.getByText('Chỉnh sửa'));
  fireEvent.changeText(screen.getByLabelText('Nội dung bài viết'), 'Nội dung vẫn có nền');
  fireEvent.press(screen.getByText('Lưu thay đổi'));
  await waitFor(() => expect(postsApi.update).toHaveBeenCalledWith('p2', { content: 'Nội dung vẫn có nền', privacy: 'friends', mediaIds: [], textBackground: 'sunset' }));
});
test('an edit failure keeps the draft available for retry', async () => {
  jest.mocked(postsApi.update).mockRejectedValue(new Error('Không lưu được'));
  renderPost(ownPost);
  fireEvent.press(screen.getByLabelText('Tùy chọn bài viết'));
  fireEvent.press(screen.getByText('Chỉnh sửa'));
  fireEvent.changeText(screen.getByLabelText('Nội dung bài viết'), 'Giữ nội dung này');
  fireEvent.press(screen.getByText('Lưu thay đổi'));
  await waitFor(() => expect(screen.getByLabelText('Nội dung bài viết').props.value).toBe('Giữ nội dung này'));
  await waitFor(() => expect(screen.getAllByText('Không lưu được').length).toBeGreaterThan(0));
  expect(screen.getByText('Lưu thay đổi')).toBeTruthy();
});
test('deletion requires confirmation before sending the delete request', () => {
  const alert = jest.spyOn(Alert, 'alert').mockImplementation(() => {});
  renderPost(ownPost);
  fireEvent.press(screen.getByLabelText('Tùy chọn bài viết'));
  fireEvent.press(screen.getByText('Xóa bài viết'));
  expect(alert).toHaveBeenCalled();
  expect(postsApi.delete).not.toHaveBeenCalled();
  alert.mockRestore();
});
test('other users can report a post but cannot edit or delete it', async () => {
  jest.mocked(reportsApi.reportPost).mockResolvedValue({} as never);
  const alert = jest.spyOn(Alert, 'alert').mockImplementation(() => {});
  renderPost({ ...ownPost, authorUserId: 'u2' });
  fireEvent.press(screen.getByLabelText('Tùy chọn bài viết'));
  expect(screen.queryByText('Chỉnh sửa')).toBeNull();
  expect(screen.queryByText('Xóa bài viết')).toBeNull();
  fireEvent.press(screen.getByText('Báo cáo'));
  fireEvent.press(screen.getByText('Gửi báo cáo'));
  await waitFor(() => expect(reportsApi.reportPost).toHaveBeenCalledWith('p2', { reason: 'spam', details: undefined }));
  alert.mockRestore();
});
