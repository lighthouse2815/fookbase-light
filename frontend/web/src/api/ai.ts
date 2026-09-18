import { apiRequest } from './client'

export type AiChatRole = 'user' | 'assistant'

export interface AiChatMessage {
  role: AiChatRole
  content: string
}

export interface AiChatResponse {
  content: string
  model: string
}

export const aiApi = {
  chat: (message: string, history: AiChatMessage[]) =>
    apiRequest<AiChatResponse>('/api/ai/chat', {
      method: 'POST',
      body: JSON.stringify({ message, history }),
    }),
}
