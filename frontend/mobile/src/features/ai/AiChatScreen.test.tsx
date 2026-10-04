import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import AiChat from '../../app/ai-chat';
import { aiApi } from '../../api/ai';
jest.mock('../../api/ai', () => ({ aiApi: { chat: jest.fn() } }));
test('keeps user draft on failure and sends history on the next successful turn', async () => {
  jest.mocked(aiApi.chat).mockRejectedValueOnce(new Error('Máy chủ bận')).mockResolvedValueOnce({ content: 'Xin chào!', model: 'server-model' });
  render(<SafeAreaProvider initialMetrics={{ frame: { x:0, y:0, width:390, height:844 }, insets:{ top:0, left:0, right:0, bottom:0 } }}><AiChat /></SafeAreaProvider>);
  fireEvent.changeText(screen.getByLabelText('Tin nhắn cho AI'), 'Chào bạn');
  fireEvent.press(screen.getByText('Gửi'));
  await screen.findByText('Máy chủ bận');
  expect(screen.getByLabelText('Tin nhắn cho AI').props.value).toBe('Chào bạn');
  fireEvent.press(screen.getByText('Gửi'));
  await screen.findByText('Xin chào!');
  expect(aiApi.chat).toHaveBeenLastCalledWith('Chào bạn', []);
  await waitFor(() => expect(screen.getByLabelText('Tin nhắn cho AI').props.value).toBe(''));
});
