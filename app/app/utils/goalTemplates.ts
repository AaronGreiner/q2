import type { DontProofMode, GoalCategory, GoalScheduleRequest, QuotaPeriod } from '~/api/types'

/**
 * The ideas the create sheet offers above an empty title.
 *
 * A starting point, not a catalogue: tapping one fills the title, the category
 * and the rhythm, and every one of them can be changed before the goal exists.
 * The words live in the message catalogue under `create.templates`, keyed by
 * `key`; the category draws the icon (#18).
 */
export interface GoalTemplate {
  key: 'run' | 'read' | 'water' | 'early' | 'gym' | 'tidy'
  category: GoalCategory
  schedule: GoalScheduleRequest
}

export const goalTemplates: readonly GoalTemplate[] = [
  { key: 'run', category: 'Sport', schedule: { kind: 'Times', times: 3, period: 'Week' } },
  { key: 'read', category: 'Reading', schedule: { kind: 'Interval', everyDays: 1 } },
  { key: 'water', category: 'Other', schedule: { kind: 'Interval', everyDays: 1 } },
  { key: 'early', category: 'Other', schedule: { kind: 'Interval', everyDays: 1 } },
  { key: 'gym', category: 'Sport', schedule: { kind: 'Times', times: 2, period: 'Week' } },
  { key: 'tidy', category: 'Other', schedule: { kind: 'Weekdays', weekdays: ['Sunday'] } },
]

/**
 * The Don't ideas, offered in the same place when the sheet is set to "Ich will
 * nicht mehr …". Each fills the title, the category, the limit and how friends
 * check it (docs/adr/0037-donts.md).
 */
export interface DontTemplate {
  key: 'alcohol' | 'socialMedia' | 'smoking' | 'fastFood' | 'sugar'
  category: GoalCategory
  limit: number
  period: QuotaPeriod
  proofMode: DontProofMode
}

export const dontTemplates: readonly DontTemplate[] = [
  { key: 'alcohol', category: 'Other', limit: 0, period: 'Day', proofMode: 'SpotCheck' },
  { key: 'socialMedia', category: 'Mindfulness', limit: 0, period: 'Day', proofMode: 'ProofAtEnd' },
  { key: 'smoking', category: 'Other', limit: 0, period: 'Day', proofMode: 'SpotCheck' },
  { key: 'fastFood', category: 'Other', limit: 2, period: 'Week', proofMode: 'HonestOnly' },
  { key: 'sugar', category: 'Other', limit: 0, period: 'Day', proofMode: 'HonestOnly' },
]
