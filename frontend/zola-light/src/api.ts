export const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, '') ?? ''

export function resolveApiUrl(url: string) {
  return url.startsWith('/') ? `${apiBaseUrl}${url}` : url
}

const sessionKey = 'fookbase.zola-light.session'

export interface AuthenticatedUser {
  id: string
  email: string
  username: string
  emailConfirmed: boolean
  roles: string[]
}

export interface AuthSession {
  user: AuthenticatedUser
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken?: string
  refreshTokenExpiresAt: string
}
export interface TwoFactorChallenge { twoFactorRequired: true; challenge: string; expiresAtUtc: string }
export interface ExternalProviders { google: boolean }
export type GoogleLoginResponse = AuthSession | TwoFactorChallenge

export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

export function getSession(): AuthSession | null {
  const value = localStorage.getItem(sessionKey)
  if (!value) return null
  try {
    return JSON.parse(value) as AuthSession
  } catch {
    localStorage.removeItem(sessionKey)
    return null
  }
}

export function saveSession(session: AuthSession) {
  const { refreshToken: _refreshToken, ...safeSession } = session
  localStorage.setItem(sessionKey, JSON.stringify(safeSession))
}

export function clearSession() {
  localStorage.removeItem(sessionKey)
}

async function refreshSession() {
  const session = getSession()
  if (!session) return null
  const response = await fetch(`${apiBaseUrl}/api/auth/refresh`, {
    method: 'POST',
    credentials: 'include',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      'X-Fookbase-Auth-Transport': 'cookie:zola-light',
    },
    body: JSON.stringify(session.refreshToken ? { refreshToken: session.refreshToken } : {}),
  })
  if (!response.ok) {
    clearSession()
    return null
  }
  const { data: next } = await response.json() as { data: AuthSession }
  saveSession(next)
  return next
}

export async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const send = async (token: string | null) => fetch(`${apiBaseUrl}${path}`, {
    ...init,
    credentials: 'include',
    headers: {
      Accept: 'application/json',
      ...(init.body && !(init.body instanceof FormData) ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(path.startsWith('/api/auth/') ? { 'X-Fookbase-Auth-Transport': 'cookie:zola-light' } : {}),
      ...init.headers,
    },
  })
  let response: Response
  try {
    response = await send(getSession()?.accessToken ?? null)
    if (response.status === 401 && !path.startsWith('/api/auth/')) {
      response = await send((await refreshSession())?.accessToken ?? null)
    }
  } catch {
    throw new ApiError('Không thể kết nối tới máy chủ.', 0)
  }
  if (!response.ok) {
    const body = await response.json().catch(() => null) as { error?: { message?: string }; detail?: string; title?: string } | null
    throw new ApiError(body?.error?.message ?? body?.detail ?? body?.title ?? 'Yêu cầu không thành công.', response.status)
  }
  if (response.status === 204) return undefined as T
  const body = await response.json()
  return path.startsWith('/api/auth/') ? body.data as T : body as T
}

export const authApi = {
  providers: () => request<ExternalProviders>('/api/auth/providers'),
  login: (email: string, password: string) => request<AuthSession | TwoFactorChallenge>('/api/auth/login', {
    method: 'POST', body: JSON.stringify({ email, password }),
  }),
  completeGoogle: (code: string) => request<GoogleLoginResponse>('/api/auth/google/exchange', {
    method: 'POST', body: JSON.stringify({ code, client: 'zola-light' }),
  }),
  linkGoogle: (code: string, password: string) => request<GoogleLoginResponse>('/api/auth/google/link', {
    method: 'POST', body: JSON.stringify({ code, password, client: 'zola-light' }),
  }),
  verifyTwoFactor: (challenge: string, code: string) => request<AuthSession>('/api/auth/2fa/verify', {
    method: 'POST', body: JSON.stringify({ challenge, code }),
  }),
  logout: (refreshToken?: string) => request<void>('/api/auth/logout', {
    method: 'POST', body: JSON.stringify(refreshToken ? { refreshToken } : {}),
  }),
}

export interface MessageAttachment { mediaId: string; sortOrder: number }
export interface MessageReaction { userId: string; type: string }
export interface MessageReplyPreview { id: string; senderUserId: string; content: string | null; type: string; isDeleted: boolean }
export interface MessageStoryReference {
  storyId: string
  isAvailable: boolean
  caption: string | null
  mediaType: 'image' | 'video' | null
}
export interface Message {
  id: string
  conversationId: string
  senderUserId: string
  content: string | null
  createdAtUtc: string
  readAtUtc: string | null
  type: 'text' | 'media'
  replyToMessageId: string | null
  replyTo: MessageReplyPreview | null
  editedAtUtc: string | null
  deletedAtUtc: string | null
  attachments: MessageAttachment[]
  reactions: MessageReaction[]
  story: MessageStoryReference | null
}
export interface Participant {
  userId: string
  role: 'owner' | 'admin' | 'member'
  joinedAtUtc: string
  leftAtUtc: string | null
  lastReadMessageId: string | null
  lastReadAtUtc: string | null
  lastDeliveredMessageId: string | null
  nickname: string | null
}
export interface Conversation {
  id: string
  participantUserId: string | null
  createdAtUtc: string
  lastMessageAtUtc: string
  lastMessage: Message | null
  unreadCount: number
  type: 'direct' | 'group'
  title: string | null
  photoMediaId: string | null
  participants: Participant[]
  isMuted: boolean
  isArchived: boolean
}
export interface CursorPage<T> { items: T[]; limit: number; nextCursor: string | null }
export interface MessageHistory { items: Message[]; nextCursor: string | null; hasMore: boolean }
export interface UserProfile { userId: string; username: string; displayName: string; avatarUrl: string | null }

const query = (parameters: Record<string, string | undefined>) => new URLSearchParams(
  Object.entries(parameters).filter((entry): entry is [string, string] => entry[1] !== undefined),
).toString()

export const messengerApi = {
  conversations: (before?: string, includeArchived = false) => request<CursorPage<Conversation>>(
    `/api/messages/conversations?${query({ before, limit: '30', includeArchived: includeArchived ? 'true' : undefined })}`,
  ),
  conversation: (id: string) => request<Conversation>(`/api/messages/conversations/${id}`),
  direct: (userId: string) => request<Conversation>('/api/messages/conversations/direct', {
    method: 'POST', body: JSON.stringify({ userId }),
  }),
  group: (title: string, participantUserIds: string[], photoMediaId?: string) => request<Conversation>('/api/messages/conversations/group', {
    method: 'POST', body: JSON.stringify({ title, participantUserIds, photoMediaId }),
  }),
  updateConversation: (id: string, value: object) => request<Conversation>(`/api/messages/conversations/${id}`, {
    method: 'PATCH', body: JSON.stringify(value),
  }),
  messages: (id: string, before?: string) => request<MessageHistory>(`/api/messages/conversations/${id}/messages?${query({ before, limit: '50' })}`),
  searchMessages: (id: string, value: string) => request<Message[]>(`/api/messages/conversations/${id}/search?${query({ q: value })}`),
  send: (id: string, content: string, mediaIds: string[], replyToMessageId?: string) => request<Message>(`/api/messages/conversations/${id}/messages`, {
    method: 'POST', body: JSON.stringify({ content: content || null, mediaIds, replyToMessageId }),
  }),
  read: (id: string, lastReadMessageId: string) => request<void>(`/api/messages/conversations/${id}/read`, {
    method: 'POST', body: JSON.stringify({ lastReadMessageId }),
  }),
  edit: (id: string, content: string) => request<Message>(`/api/messages/${id}`, { method: 'PATCH', body: JSON.stringify({ content }) }),
  unsend: (id: string) => request<void>(`/api/messages/${id}`, { method: 'DELETE' }),
  react: (id: string, type: string) => request<void>(`/api/messages/${id}/reactions`, { method: 'POST', body: JSON.stringify({ type }) }),
  removeReaction: (id: string) => request<void>(`/api/messages/${id}/reactions`, { method: 'DELETE' }),
  addParticipants: (id: string, userIds: string[]) => request<Conversation>(`/api/messages/conversations/${id}/participants`, { method: 'POST', body: JSON.stringify({ userIds }) }),
  removeParticipant: (id: string, userId: string) => request<void>(`/api/messages/conversations/${id}/participants/${userId}`, { method: 'DELETE' }),
  changeRole: (id: string, userId: string, role: string) => request<void>(`/api/messages/conversations/${id}/participants/${userId}/role`, { method: 'PATCH', body: JSON.stringify({ role }) }),
  leave: (id: string) => request<void>(`/api/messages/conversations/${id}/leave`, { method: 'POST' }),
  transferOwnership: (id: string, userId: string) => request<void>(`/api/messages/conversations/${id}/transfer-ownership`, { method: 'POST', body: JSON.stringify({ userId }) }),
  currentUser: () => request<UserProfile>('/api/users/me'),
  user: (userId: string) => request<UserProfile>(`/api/users/${userId}`),
  searchUsers: (value: string) => request<{ items: UserProfile[] }>(`/api/users/search?${query({ query: value, offset: '0', limit: '10' })}`),
  upload: async (file: File) => {
    const intent = await request<{ mediaId: string; uploadUrl: string; uploadMethod: 'POST'; uploadParameters: Record<string, string> }>('/api/media/uploads', {
      method: 'POST', body: JSON.stringify({ fileName: file.name, contentType: file.type, sizeBytes: file.size }),
    })
    const body = new FormData()
    for (const [name, value] of Object.entries(intent.uploadParameters)) body.append(name, value)
    body.append('file', file)
    const upload = await fetch(intent.uploadUrl, { method: intent.uploadMethod, body })
    if (!upload.ok) throw new ApiError('Không thể tải media lên.', upload.status)
    await request(`/api/media/${intent.mediaId}/complete`, { method: 'POST' })
    return intent.mediaId
  },
  mediaUrl: (mediaId: string) => request<{ url: string }>(`/api/messages/media/${mediaId}/read-url`).then((result) => result.url),
}
