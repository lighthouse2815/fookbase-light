import { fireEvent, render, screen } from '@testing-library/react-native';
import { router } from 'expo-router';
import Notifications from '../app/(tabs)/notifications';

const mockMessages = [{ conversation: { id: 'conversation-42', title: 'Nhóm học tập' }, message: { id: 'message-1', content: 'Chào bạn', createdAtUtc: '2026-10-05T00:00:00Z' } }];
jest.mock('expo-router', () => ({ router: { push: jest.fn() } }));
jest.mock('@tanstack/react-query', () => ({
  useQuery: () => ({ data: { items: mockMessages }, isPending: false }),
  useInfiniteQuery: () => ({ data: { pages: [{ items: [] }] }, isPending: false }),
  useMutation: () => ({ mutate: jest.fn() }),
  useQueryClient: () => ({ invalidateQueries: jest.fn() }),
}));
jest.mock('../auth/AuthProvider', () => ({ useAuth: () => ({ session: { user: { id: 'u1' } } }) }));
jest.mock('../api/messages', () => ({ messengerApi: {} }));
jest.mock('../api/notifications', () => ({ notificationsApi: {} }));

test('pressing a message notification opens the associated conversation', () => {
  render(<Notifications />);
  fireEvent.press(screen.getByLabelText('Mở cuộc trò chuyện Nhóm học tập'));
  expect(router.push).toHaveBeenCalledWith('/conversations/conversation-42');
});
test('shows an empty state when both notification feeds are empty', () => {
  mockMessages.splice(0);
  render(<Notifications />);
  expect(screen.getByText('Bạn chưa có thông báo mới.')).toBeTruthy();
});
