import { fireEvent, render, screen } from '@testing-library/react-native';
import Account from '../app/(tabs)/menu';
import { clearSession } from './session';
import { authApi } from '../api/auth';
jest.mock('expo-router', () => ({ router: { push: jest.fn() } }));
jest.mock('react-native-safe-area-context', () => ({ SafeAreaView: require('react-native').View }));
jest.mock('./session', () => ({ getSession: () => ({ user: { id: 'u1', username: 'user' }, refreshToken: 'refresh' }), clearSession: jest.fn().mockResolvedValue(undefined) }));
jest.mock('../api/auth', () => ({ authApi: { logout: jest.fn(() => new Promise(() => {})) } }));
test('clears credentials even if server logout never responds', () => {
  render(<Account />); fireEvent.press(screen.getByText('Đăng xuất'));
  expect(clearSession).toHaveBeenCalledTimes(1);
  expect(authApi.logout).toHaveBeenCalledWith('refresh');
});
