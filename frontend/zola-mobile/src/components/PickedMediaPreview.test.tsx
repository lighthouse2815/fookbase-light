import { act, fireEvent, render, screen } from '@testing-library/react-native';
import { AppState, type AppStateStatus } from 'react-native';
import { PickedMediaPreview } from './PickedMediaPreview';
const mockPlayer = { playing: true, pause() { this.playing = false; } };
let mockFocusCleanup: () => void;
jest.mock('expo-router', () => ({ useFocusEffect: (callback: () => () => void) => { mockFocusCleanup = callback(); } }));
jest.mock('expo-video', () => ({ useVideoPlayer: () => mockPlayer, VideoView: require('react-native').View }));
test('local video stops when app goes to background and when composer loses focus', () => {
  let change: (status: AppStateStatus) => void = () => {};
  jest.spyOn(AppState, 'addEventListener').mockImplementation((_name, listener) => { change = listener; return { remove: jest.fn() }; });
  mockPlayer.playing = true;
  render(<PickedMediaPreview file={{ uri: 'file:///clip.mp4', name: 'clip.mp4', mimeType: 'video/mp4', sizeBytes: 2048 }} />);
  act(() => change('background')); expect(mockPlayer.playing).toBe(false);
  mockPlayer.playing = true; act(() => mockFocusCleanup()); expect(mockPlayer.playing).toBe(false);
  jest.restoreAllMocks();
});
test('unavailable local image tells the user to choose the attachment again', () => {
  render(<PickedMediaPreview file={{ uri: 'file:///expired.jpg', name: 'expired.jpg', mimeType: 'image/jpeg', sizeBytes: 1024 }} />);
  fireEvent(screen.getByLabelText('Ảnh đã chọn: expired.jpg'), 'error');
  expect(screen.getByText('Không đọc được tệp đã chọn. Hãy chọn lại tệp.')).toBeTruthy();
});
