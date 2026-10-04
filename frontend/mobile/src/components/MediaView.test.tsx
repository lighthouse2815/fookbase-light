import { act, fireEvent, render, screen } from '@testing-library/react-native';
import { AppState, type AppStateStatus } from 'react-native';
import { MediaView } from './MediaView';

const mockListeners = new Map<string, (event: object) => void>();
const mockPlayer = {
  loop: false,
  playing: false,
  status: 'readyToPlay',
  play: jest.fn(),
  pause: jest.fn(),
  addListener: (event: string, listener: (value: object) => void) => {
    mockListeners.set(event, listener);
    return { remove: () => mockListeners.delete(event) };
  },
};
const mockRefetch = jest.fn();
let mockFocusCleanup: (() => void) | undefined;
jest.mock('expo', () => ({
  ...jest.requireActual('expo'),
  useEvent: (player: typeof mockPlayer, event: string, initial: object) => {
    const React = require('react');
    const [value, setValue] = React.useState(initial);
    React.useEffect(() => {
      const listener = player.addListener(event, setValue);
      return () => listener.remove();
    }, [player, event]);
    return value;
  },
}));
jest.mock('expo-router', () => ({
  useFocusEffect: (effect: () => (() => void)) => {
    require('react').useEffect(() => { mockFocusCleanup = effect(); return mockFocusCleanup; }, [effect]);
  },
}));
jest.mock('expo-video', () => ({
  useVideoPlayer: (_url: string, setup: (player: typeof mockPlayer) => void) => { setup(mockPlayer); return mockPlayer; },
  VideoView: require('react-native').View,
}));
jest.mock('@tanstack/react-query', () => ({ useQuery: () => ({ data: { url: 'https://media.example.test/video.mp4', mediaType: 'video' }, refetch: mockRefetch }) }));
jest.mock('../auth/AuthProvider', () => ({ useAuth: () => ({ session: { user: { id: 'u1' } } }) }));
jest.mock('../api/client', () => ({ apiRequest: jest.fn() }));

beforeEach(() => {
  jest.clearAllMocks();
  jest.spyOn(AppState, 'addEventListener').mockReturnValue({ remove: jest.fn() });
  mockListeners.clear();
  mockPlayer.playing = false;
  mockPlayer.status = 'readyToPlay';
  Object.defineProperty(AppState, 'currentState', { configurable: true, value: 'active' });
});

test('post videos keep manual playback by default', () => {
  render(<MediaView path="/post/video" />);
  expect(mockPlayer.play).not.toHaveBeenCalled();
  expect(mockPlayer.loop).toBe(false);
  expect(screen.queryByLabelText('Phát video')).toBeNull();
});

test('reels start playback, allow pause and play, and pause when leaving the screen', () => {
  render(<MediaView path="/reel/video" nativeControls={false} autoPlay loop />);
  expect(mockPlayer.play).toHaveBeenCalledTimes(1);
  expect(mockPlayer.loop).toBe(true);
  act(() => mockListeners.get('playingChange')?.({ isPlaying: true }));
  fireEvent.press(screen.getByLabelText('Tạm dừng video'));
  expect(mockPlayer.pause).toHaveBeenCalledTimes(1);
  act(() => mockListeners.get('playingChange')?.({ isPlaying: false }));
  fireEvent.press(screen.getByLabelText('Phát video'));
  expect(mockPlayer.play).toHaveBeenCalledTimes(2);
  act(() => mockFocusCleanup?.());
  expect(mockPlayer.pause).toHaveBeenCalledTimes(2);
});

test('backgrounding pauses playback and releases the app listener on unmount', () => {
  let onAppStateChange: ((state: AppStateStatus) => void) | undefined;
  const remove = jest.fn();
  const subscription = jest.spyOn(AppState, 'addEventListener').mockImplementation((_event, listener) => { onAppStateChange = listener; return { remove }; });
  const view = render(<MediaView path="/reel/video" nativeControls={false} autoPlay />);
  act(() => onAppStateChange?.('background'));
  expect(mockPlayer.pause).toHaveBeenCalled();
  view.unmount();
  expect(remove).toHaveBeenCalledTimes(1);
  subscription.mockRestore();
});

test('loading disables playback and playback errors offer retry', () => {
  render(<MediaView path="/reel/video" nativeControls={false} />);
  act(() => mockListeners.get('statusChange')?.({ status: 'loading' }));
  expect(screen.getByLabelText('Phát video')).toBeDisabled();
  expect(screen.getByLabelText('Đang tải')).toBeTruthy();
  act(() => mockListeners.get('statusChange')?.({ status: 'error' }));
  expect(screen.getByText('Không phát được video. Hãy thử tải lại.')).toBeTruthy();
  mockPlayer.status = 'readyToPlay';
  fireEvent.press(screen.getByText('Thử lại'));
  expect(mockRefetch).toHaveBeenCalledTimes(1);
});
