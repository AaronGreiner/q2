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
export type CreateGoalRequest = Schemas['CreateGoalRequest']

export type GoalSchedule = Schemas['GoalScheduleResponse']
export type GoalScheduleRequest = Schemas['GoalScheduleRequest']
export type ScheduleKind = Schemas['ScheduleKind']
/*
 * `NonNullable`, because the generated union carries the `null` that the
 * *property* allows: a period only means anything on a quota, and the schema
 * says so by making the field nullable. What a period can be is Week or Month.
 */
export type QuotaPeriod = NonNullable<Schemas['QuotaPeriod']>
export type Weekday = Schemas['Weekday']

export type GoalPause = Schemas['GoalPauseResponse']
export type RequestPauseRequest = Schemas['RequestPauseRequest']
export type CloseGoalRequest = Schemas['CloseGoalRequest']

export type GoalWindow = Schemas['GoalInstanceResponse']
export type GoalWindowStatus = Schemas['GoalInstanceStatus']
export type DaySummary = Schemas['DaySummaryResponse']

export type Balance = Schemas['BalanceResponse']
export type PersonProfile = Schemas['PersonProfileResponse']
export type Risk = Schemas['RiskResponse']
export type RiskReason = Schemas['RiskReason']

export type Proof = Schemas['ProofResponse']
export type FeedProof = Schemas['FeedProofResponse']
export type ProofStatus = Schemas['ProofStatus']
export type VoteSummary = Schemas['VoteSummaryResponse']
export type ReactionSummary = Schemas['ReactionSummaryResponse']
/*
 * Named `ProofVoteValue` rather than `VoteValue`: a bare "vote" will mean
 * something else the moment this product has anything else to vote on.
 */
export type ProofVoteValue = Schemas['VoteValue']

export type Challenge = Schemas['ChallengeResponse']
export type ChallengeRoom = Schemas['ChallengeRoomResponse']
export type ChallengeEntry = Schemas['ChallengeEntryResponse']
export type ChallengeArchiveEntry = Schemas['ChallengeArchiveEntryResponse']
/*
 * A wrapper with one nullable property, because "no challenge today" is an
 * ordinary state rather than an error. The alternatives — a 204 or a naked
 * `null` body — make it depend on how the fetch layer treats a body that is
 * not there.
 */
export type ChallengeToday = Schemas['ChallengeTodayResponse']

export type ReportReceipt = Schemas['ReportReceiptResponse']
/*
 * `NonNullable`, because the generated unions carry the `null` the *request
 * properties* allow: every field of a report request is nullable so an empty
 * body produces field errors rather than a binding failure.
 */
export type ReportReason = NonNullable<Schemas['ReportReason']>
export type ReportTargetKind = NonNullable<Schemas['ReportTargetKind']>

export type Image = Schemas['ImageResponse']
export type ImagePurpose = Schemas['ImagePurpose']
export type ImageQuota = Schemas['ImageQuotaResponse']

export type Activity = Schemas['ActivityResponse']
export type ActivityKind = Schemas['ActivityKind']

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
export type KudosKind = Schemas['KudosKind']
export type ChatPinnedGoal = Schemas['ChatPinnedGoalResponse']
export type StartDirectChatRequest = Schemas['StartDirectChatRequest']
export type CreateGroupChatRequest = Schemas['CreateGroupChatRequest']

export type Session = Schemas['SessionResponse']
export type AccountDeletion = Schemas['AccountDeletionResponse']
export type RegisterRequest = Schemas['RegisterRequest']
export type LoginRequest = Schemas['LoginRequest']

export type Profile = Schemas['ProfileResponse']
export type UpdateProfileRequest = Schemas['UpdateProfileRequest']
export type Badge = Schemas['BadgeResponse']
export type BadgeKey = Schemas['BadgeKey']

export type PushKey = Schemas['PushKeyResponse']

export type Settings = Schemas['SettingsResponse']
export type UpdateSettingsRequest = Schemas['UpdateSettingsRequest']
export type ThemePreference = Schemas['ThemePreference']
export type LanguagePreference = Schemas['LanguagePreference']

export type ProblemDetails = Schemas['ProblemDetails']
export type ValidationProblemDetails = Schemas['HttpValidationProblemDetails']

/** Every status the API can return, in the order the UI offers them. */
export const goalStatuses: readonly GoalStatus[] = ['Active', 'Completed', 'Archived'] as const

/** Every kind of schedule, in the order the create sheet offers them. */
export const scheduleKinds: readonly ScheduleKind[] = ['Interval', 'Weekdays', 'Times', 'Once'] as const

/** Monday to Sunday, the way this product numbers them. */
export const weekdays: readonly Weekday[] = [
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
  'Sunday',
] as const

/**
 * The two verdicts, in the order the card offers them.
 *
 * Confirm first, and it is the larger of the two on screen: believing a friend
 * is the ordinary answer, and doubting has to be a deliberate second choice
 * rather than a symmetrical one.
 */
export const proofVoteValues: readonly ProofVoteValue[] = ['Confirm', 'Doubt'] as const

/**
 * What the server accepts for a pause.
 *
 * Mirrored here only so the sheet can offer the right number of day buttons and
 * refuse a reason that would be rejected anyway. The rule itself is
 * `PauseRules` on the server, which decides every one of these again — nothing
 * here is binding (AGENTS.md section 3).
 */
export const pauseLimits = {
  minDays: 1,
  maxDays: 7,
  minReason: 10,
  maxReason: 280,
} as const

/**
 * Why something is being reported, in the order the sheet offers them.
 *
 * Harassment is third rather than first on purpose: the two above it are about
 * a picture, and reaching for "Belästigung" should be a deliberate choice
 * rather than the one under the thumb.
 */
export const reportReasons = [
  'Faked',
  'Inappropriate',
  'Harassment',
  'Spam',
  'Other',
] as const satisfies readonly ReportReason[]

/** What the server accepts in a report's note. Mirrored so the sheet can count. */
export const reportNoteLimit = 500

/** The two periods a quota can be counted over. */
export const quotaPeriods: readonly QuotaPeriod[] = ['Week', 'Month'] as const

/**
 * The intervals the create sheet offers, in days.
 *
 * A short list rather than a number field: "every 137 days" is not a
 * commitment anybody makes, and a stepper is one more thing to get wrong on a
 * phone. Anything else the server would still accept.
 */
export const intervalChoices: readonly number[] = [1, 2, 3, 7, 14] as const

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

/**
 * The three ways to say "I saw that", in the order they are offered.
 *
 * Matches `KudosKind` on the server. They are drawn as icons rather than as
 * emoji — see `kudosIconName` in app/utils/display.ts — and counted together as
 * kudos on a profile.
 */
export const kudosKinds = ['Fire', 'Strong', 'Applause'] as const satisfies readonly KudosKind[]

/**
 * The avatars a group chat can be given.
 *
 * A short, closed list rather than a picker: the avatar is one icon on a dark
 * tile, and every one of these still reads at that size. Matches
 * `ConversationIcons` on the server, and each name must also be in the bundled
 * icon list in nuxt.config.ts — an icon in one but not the other renders as
 * nothing at all.
 */
export const groupIcons = [
  'message-circle',
  'sunrise',
  'book-open',
  'footprints',
  'sprout',
  'target',
  'hand-heart',
  'party-popper',
] as const

export function isGoalStatus(value: unknown): value is GoalStatus {
  return typeof value === 'string' && (goalStatuses as readonly string[]).includes(value)
}
