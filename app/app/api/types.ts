import type { components } from './generated/schema'

/**
 * The API contract, named for use in the app.
 *
 * Everything the frontend knows about the backend's shapes comes from the
 * generated schema. Re-exporting here means components import from one stable
 * place, and a contract change surfaces as a type error rather than as a
 * runtime surprise.
 */
type Schemas = components['schemas']

export type Person = Schemas['PersonSummary']

export type Goal = Schemas['GoalResponse']
export type GoalDetail = Schemas['GoalDetailResponse']
export type GoalTeamMember = Schemas['GoalTeamMemberResponse']
export type GoalStatus = Schemas['GoalStatus']
export type GoalRhythm = Schemas['GoalRhythm']
export type CreateGoalRequest = Schemas['CreateGoalRequest']

export type GoalTask = Schemas['GoalTaskResponse']
export type CreateTaskRequest = Schemas['CreateTaskRequest']
export type DaySummary = Schemas['DaySummaryResponse']

export type Activity = Schemas['ActivityResponse']
export type ActivityKind = Schemas['ActivityKind']
export type LeaderboardEntry = Schemas['LeaderboardEntryResponse']

export type Friends = Schemas['FriendsResponse']
export type Friend = Schemas['FriendResponse']
export type FriendRequest = Schemas['FriendRequestResponse']
export type SentRequest = Schemas['SentRequestResponse']
export type FriendSuggestion = Schemas['FriendSuggestionResponse']
export type PersonSearchResult = Schemas['PersonSearchResultResponse']
export type FriendshipState = Schemas['FriendshipState']

export type ChatSummary = Schemas['ChatSummaryResponse']
export type ChatDetail = Schemas['ChatDetailResponse']
export type ChatMessage = Schemas['ChatMessageResponse']
export type MessageReaction = Schemas['MessageReactionResponse']
export type ChatPinnedGoal = Schemas['ChatPinnedGoalResponse']
export type StartDirectChatRequest = Schemas['StartDirectChatRequest']
export type CreateGroupChatRequest = Schemas['CreateGroupChatRequest']

export type Session = Schemas['SessionResponse']
export type RegisterRequest = Schemas['RegisterRequest']
export type LoginRequest = Schemas['LoginRequest']

export type Profile = Schemas['ProfileResponse']
export type Badge = Schemas['BadgeResponse']
export type BadgeKey = Schemas['BadgeKey']

export type Settings = Schemas['SettingsResponse']
export type UpdateSettingsRequest = Schemas['UpdateSettingsRequest']
export type ThemePreference = Schemas['ThemePreference']
export type LanguagePreference = Schemas['LanguagePreference']

export type ProblemDetails = Schemas['ProblemDetails']
export type ValidationProblemDetails = Schemas['HttpValidationProblemDetails']

/** Every status the API can return, in the order the UI offers them. */
export const goalStatuses: readonly GoalStatus[] = ['Active', 'Completed', 'Archived'] as const

/** Every rhythm, in the order the create sheet offers them. */
export const goalRhythms: readonly GoalRhythm[] = ['Daily', 'Weekdays', 'Weekly', 'Once'] as const

/**
 * The icons a goal may use.
 *
 * Must match `GoalIcons` in api/src/Q2.Api/Features/Goals/Goal.cs and the
 * bundle list in nuxt.config.ts. The server rejects anything else, and an icon
 * missing from the bundle renders as empty space.
 */
export const goalIcons: readonly string[] = [
  'target',
  'medal',
  'book-open',
  'sunrise',
  'droplet',
  'flame',
  'trophy',
  'sparkles',
  'calendar',
  'alarm-clock',
  'hand-heart',
  'users',
] as const

/** The reactions a message can carry. Matches `MessageReactions` on the server. */
export const messageReactions = ['👏', '🔥', '❤️'] as const

/**
 * The avatars a group chat can be given.
 *
 * A short, closed list rather than an emoji picker: the avatar is one glyph on
 * a coloured circle, and every one of these still reads at that size.
 */
export const groupEmoji = ['💬', '🌅', '📚', '🏃', '🌱', '🎯', '💚', '🎉'] as const

export function isGoalStatus(value: unknown): value is GoalStatus {
  return typeof value === 'string' && (goalStatuses as readonly string[]).includes(value)
}
