import type { ApiCaller } from './client'
import type {
  ChatDetail,
  ChatMessage,
  ChatPage,
  ChatPhoto,
  ChatSummary,
  CreateGroupChatRequest,
  KudosKind,
  Settings,
  UpdateSettingsRequest,
} from './types'

/** Conversations and messages. */
export interface ChatsApi {
  list: (options?: { search?: string }) => Promise<ChatSummary[]>
  get: (id: string) => Promise<ChatDetail>
  older: (id: string, before: string) => Promise<ChatPage>
  send: (id: string, message: { text?: string, imageId?: string }) => Promise<ChatDetail>
  react: (id: string, messageId: string, kind: KudosKind) => Promise<ChatMessage>
  /** One message as it now is — for a line further back that changed (#25). */
  message: (id: string, messageId: string) => Promise<ChatMessage>
  /** Corrects the words of your own message, within fifteen minutes (#25). */
  edit: (id: string, messageId: string, text: string) => Promise<ChatMessage>
  /** Takes back your own message for everybody; its place stays (#25). */
  remove: (id: string, messageId: string) => Promise<ChatMessage>
  /** Pins the conversation to the top of its section, or unpins it (#24). */
  pin: (id: string, pinned: boolean) => Promise<ChatDetail>
  /** The conversation's photographs, newest first (#24). */
  photos: (id: string) => Promise<ChatPhoto[]>
  startDirect: (personId: string) => Promise<ChatDetail>
  createGroup: (request: CreateGroupChatRequest) => Promise<ChatDetail>
  mute: (id: string, muted: boolean) => Promise<ChatDetail>
  leave: (id: string) => Promise<void>
}

/** The signed-in person's preferences. */
export interface SettingsApi {
  get: () => Promise<Settings>
  update: (request: UpdateSettingsRequest) => Promise<Settings>
}

export function createChatsApi(call: ApiCaller): ChatsApi {
  return {
    list: options => call<ChatSummary[]>('/api/chats', {
      method: 'GET',
      query: { search: options?.search || undefined },
    }),

    // Opening a thread also marks it read, which is why this is not a plain
    // cacheable read — see ChatService on the server. It answers with the
    // newest page of the thread; `older` reads the pages before it.
    get: id => call<ChatDetail>(`/api/chats/${encodeURIComponent(id)}`, { method: 'GET' }),

    // `before` is the cursor the previous page handed out, passed back as it
    // came: what it means is the server's business.
    older: (id, before) => call<ChatPage>(`/api/chats/${encodeURIComponent(id)}/messages`, {
      method: 'GET',
      query: { before },
    }),

    // The newest page comes back, not just the new message: a reply may have
    // arrived while this one was being typed, and re-rendering from one answer
    // is simpler to get right than merging two. A photograph is uploaded first
    // (`purpose=ChatPhoto`) and only its id travels here.
    send: (id, message) => call<ChatDetail>(`/api/chats/${encodeURIComponent(id)}/messages`, {
      method: 'POST',
      body: message,
    }),

    // Only the message comes back: it may be pages back from the newest, and
    // it is the one thing a reaction changes.
    react: (id, messageId, kind) => call<ChatMessage>(
      `/api/chats/${encodeURIComponent(id)}/messages/${encodeURIComponent(messageId)}/reactions`,
      { method: 'POST', body: { kind } },
    ),

    // Opens the one conversation with that person, creating it the first time.
    // Asking twice is the same as asking once, which is what lets the message
    // button appear on several screens without making several threads.
    startDirect: personId => call<ChatDetail>('/api/chats/direct', {
      method: 'POST',
      body: { personId },
    }),

    createGroup: request => call<ChatDetail>('/api/chats/groups', { method: 'POST', body: request }),

    // A PUT with the state rather than a toggle: tapping twice on a slow
    // connection should end where the second tap meant, not back where it began.
    mute: (id, muted) => call<ChatDetail>(`/api/chats/${encodeURIComponent(id)}/mute`, {
      method: 'PUT',
      body: { muted },
    }),

    leave: async (id) => {
      await call<unknown>(`/api/chats/${encodeURIComponent(id)}/leave`, { method: 'POST' })
    },

    message: (id, messageId) => call<ChatMessage>(
      `/api/chats/${encodeURIComponent(id)}/messages/${encodeURIComponent(messageId)}`,
      { method: 'GET' },
    ),

    edit: (id, messageId, text) => call<ChatMessage>(
      `/api/chats/${encodeURIComponent(id)}/messages/${encodeURIComponent(messageId)}`,
      { method: 'PUT', body: { text } },
    ),

    remove: (id, messageId) => call<ChatMessage>(
      `/api/chats/${encodeURIComponent(id)}/messages/${encodeURIComponent(messageId)}`,
      { method: 'DELETE' },
    ),

    // A PUT with the state, like mute, for the same reason.
    pin: (id, pinned) => call<ChatDetail>(`/api/chats/${encodeURIComponent(id)}/pin`, {
      method: 'PUT',
      body: { pinned },
    }),

    photos: id => call<ChatPhoto[]>(`/api/chats/${encodeURIComponent(id)}/photos`, { method: 'GET' }),
  }
}

export function createSettingsApi(call: ApiCaller): SettingsApi {
  return {
    get: () => call<Settings>('/api/settings', { method: 'GET' }),

    update: request => call<Settings>('/api/settings', { method: 'PUT', body: request }),
  }
}
