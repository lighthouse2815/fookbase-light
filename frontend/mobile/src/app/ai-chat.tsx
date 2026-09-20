import { useState } from 'react';
import { aiApi, type AiChatMessage } from '../api/ai';
import { Button, Card, ErrorNotice, Field, Label, Screen } from '../components/ui';
export default function AiChat() {
  const [history, setHistory] = useState<AiChatMessage[]>([]); const [text, setText] = useState('');
  const [pending, setPending] = useState(false); const [error, setError] = useState<unknown>(null);
  async function send() {
    if (pending || !text.trim()) return;
    setPending(true); setError(null);
    try { const result = await aiApi.chat(text.trim(), history); setHistory([...history, { role: 'user', content: text.trim() }, { role: 'assistant', content: result.content }]); setText(''); }
    catch (e) { setError(e); } finally { setPending(false); }
  }
  return <Screen><Label title>Trò chuyện cùng AI</Label><Label muted>Lịch sử cuộc trò chuyện này chỉ được giữ khi màn hình đang mở.</Label>
    {history.map((m,i) => <Card key={i}><Label muted>{m.role === 'user' ? 'Bạn' : 'Trợ lý AI'}</Label><Label>{m.content}</Label></Card>)}
    <Card><Field label="Tin nhắn cho AI" multiline value={text} onChangeText={setText} editable={!pending} style={{ minHeight: 90 }} />{error ? <ErrorNotice error={error} /> : null}<Button title={pending ? 'Đang trả lời…' : 'Gửi'} disabled={pending || !text.trim()} onPress={() => void send()} /></Card>
  </Screen>;
}
