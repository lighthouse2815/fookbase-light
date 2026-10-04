import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { PostCard } from './PostCard';
import { postsApi, type Post } from '../api/posts';
jest.mock('expo-router', () => ({ router: { push: jest.fn() } }));
jest.mock('../auth/AuthProvider', () => ({ useAuth: () => ({ session: { user: { id: 'u1' } } }) }));
jest.mock('./MediaView', () => ({ MediaView: () => null }));
jest.mock('../api/posts', () => ({ postsApi: { getById: jest.fn(), removeSaved: jest.fn() } }));
test('feed without saved field resolves saved status and offers unsave', async () => {
  const post = { id: 'p1', createdAtUtc: new Date().toISOString(), mediaIds: [], reactionCounts: {}, content: 'Bài viết', privacy: 'public', commentCount: 0 } as unknown as Post;
  jest.mocked(postsApi.getById).mockResolvedValue({ ...post, viewerHasSaved: true });
  jest.mocked(postsApi.removeSaved).mockResolvedValue(undefined);
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity }, mutations: { gcTime: Infinity } } })}><PostCard post={post} /></QueryClientProvider>);
  fireEvent.press(await screen.findByText('Bỏ lưu'));
  await waitFor(() => expect(postsApi.removeSaved).toHaveBeenCalledWith('p1'));
});
