import { render, screen, fireEvent } from '@testing-library/react-native';
import { FriendActions } from './FriendActions';
import { friendsApi } from '../api/friends';
jest.mock('../api/friends', () => ({ friendsApi: { sendRequest: jest.fn(), acceptRequest: jest.fn(), declineRequest: jest.fn(), cancelRequest: jest.fn(), unfriend: jest.fn(), unblock: jest.fn() } }));
test('incoming request exposes accept and decline, never duplicate friend request', () => {
  const run = jest.fn(fn => fn());
  render(<FriendActions userId="u2" relationship={{ userId: 'u2', status: 'request_received', requestId: 'r1' }} pending={false} run={run} />);
  expect(screen.queryByText('Kết bạn')).toBeNull();
  fireEvent.press(screen.getByText('Chấp nhận'));
  expect(friendsApi.acceptRequest).toHaveBeenCalledWith('r1');
});
test('outgoing request can be cancelled', () => {
  render(<FriendActions userId="u2" relationship={{ userId: 'u2', status: 'request_sent', requestId: 'r1' }} pending={false} run={fn => { void fn(); }} />);
  fireEvent.press(screen.getByText('Hủy lời mời'));
  expect(friendsApi.cancelRequest).toHaveBeenCalledWith('r1');
});
