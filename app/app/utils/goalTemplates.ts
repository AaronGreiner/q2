import type { GoalScheduleRequest } from '~/api/types'

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
