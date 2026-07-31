import type { ApiCaller } from './client'
import type {
  ChatDetail,
  ChatSummary,
  CreateGroupChatRequest,
  Settings,
  UpdateSettingsRequest,
} from './types'

/** Conversations and messages. */
export interface ChatsApi {
  list: (options?: { search?: string }) => Promise<ChatSummary[]>
  get: (id: string) => Promise<ChatDetail>
  send: (id: string, text: string) => Promise<ChatDetail>
  react: (id: string, messageId: string, emoji: string) => Promise<ChatDetail>
  startDirect: (personId: string) => Promise<ChatDetail>
  createGroup: (request: CreateGroupChatRequest) => Promise<ChatDetail>
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
    // cacheable read — see ChatService on the server.
    get: id => call<ChatDetail>(`/api/chats/${encodeURIComponent(id)}`, { method: 'GET' }),

    // The whole thread comes back, not just the new message: a reply may have
    // arrived while this one was being typed, and re-rendering from one answer
    // is simpler to get right than merging two.
    send: (id, text) => call<ChatDetail>(`/api/chats/${encodeURIComponent(id)}/messages`, {
      method: 'POST',
      body: { text },
    }),

    react: (id, messageId, emoji) => call<ChatDetail>(
      `/api/chats/${encodeURIComponent(id)}/messages/${encodeURIComponent(messageId)}/reactions`,
      { method: 'POST', body: { emoji } },
    ),

    // Opens the one conversation with that person, creating it the first time.
    // Asking twice is the same as asking once, which is what lets the message
    // button appear on several screens without making several threads.
    startDirect: personId => call<ChatDetail>('/api/chats/direct', {
      method: 'POST',
      body: { personId },
    }),

    createGroup: request => call<ChatDetail>('/api/chats/groups', { method: 'POST', body: request }),

    leave: async (id) => {
      await call<unknown>(`/api/chats/${encodeURIComponent(id)}/leave`, { method: 'POST' })
    },
  }
}

export function createSettingsApi(call: ApiCaller): SettingsApi {
  return {
    get: () => call<Settings>('/api/settings', { method: 'GET' }),

    update: request => call<Settings>('/api/settings', { method: 'PUT', body: request }),
  }
}
