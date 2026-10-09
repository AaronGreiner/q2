import type { DontProofMode, GoalScheduleRequest, QuotaPeriod } from '~/api/types'

/**
 * The ideas the create sheet offers above an empty title.
 *
 * A starting point, not a catalogue: tapping one fills the title, the icon and
 * the rhythm, and every one of them can be changed before the goal exists. The
 * words live in the message catalogue under `create.templates`, keyed by
 * `key`; the icons are from `goalIcons`, which the server checks.
 */
export interface GoalTemplate {
  key: 'run' | 'read' | 'water' | 'early' | 'gym' | 'tidy'
  icon: string
  schedule: GoalScheduleRequest
}

export const goalTemplates: readonly GoalTemplate[] = [
  { key: 'run', icon: 'medal', schedule: { kind: 'Times', times: 3, period: 'Week' } },
  { key: 'read', icon: 'book-open', schedule: { kind: 'Interval', everyDays: 1 } },
  { key: 'water', icon: 'droplet', schedule: { kind: 'Interval', everyDays: 1 } },
  { key: 'early', icon: 'sunrise', schedule: { kind: 'Interval', everyDays: 1 } },
  { key: 'gym', icon: 'trophy', schedule: { kind: 'Times', times: 2, period: 'Week' } },
  { key: 'tidy', icon: 'sparkles', schedule: { kind: 'Weekdays', weekdays: ['Sunday'] } },
]

/**
 * The Don't ideas, offered in the same place when the sheet is set to "Ich will
 * nicht mehr …". Each fills the title, the icon, the limit and how friends
 * check it (docs/adr/0037-donts.md).
 */
export interface DontTemplate {
  key: 'alcohol' | 'socialMedia' | 'smoking' | 'fastFood' | 'sugar'
  icon: string
  limit: number
  period: QuotaPeriod
  proofMode: DontProofMode
}

export const dontTemplates: readonly DontTemplate[] = [
  { key: 'alcohol', icon: 'flame', limit: 0, period: 'Day', proofMode: 'SpotCheck' },
  { key: 'socialMedia', icon: 'alarm-clock', limit: 0, period: 'Day', proofMode: 'ProofAtEnd' },
  { key: 'smoking', icon: 'hand-heart', limit: 0, period: 'Day', proofMode: 'SpotCheck' },
  { key: 'fastFood', icon: 'calendar', limit: 2, period: 'Week', proofMode: 'HonestOnly' },
  { key: 'sugar', icon: 'droplet', limit: 0, period: 'Day', proofMode: 'HonestOnly' },
]
