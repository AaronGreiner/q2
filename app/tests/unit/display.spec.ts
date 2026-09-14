import { describe, expect, it } from 'vitest'
import {
  activitySentence,
  clampProgress,
  formatAmount,
  formatChatTime,
  formatClock,
  formatDay,
  formatRelativeTime,
  goalIconName,
  goalStatusPresentation,
  notificationIcon,
  notificationLink,
  notificationText,
  scheduleExplanation,
  scheduleLabel,
  windowLabel,
  windowOutcome,
  windowPercent,
} from '~/utils/display'
import { de, en } from '~/i18n/messages'
import type { Activity, GoalSchedule, GoalWindow, NotificationKind, NotificationLine } from '~/api/types'

/**
 * The presentation helpers.
 *
 * Every one of them takes its clock, its time zone and its language as
 * arguments, which is exactly why they can be tested like this — and why the
 * server-rendered HTML and the browser's agree.
 */

// 2026-06-17 is a Wednesday. 09:00 UTC.
const now = Date.parse('2026-06-17T09:00:00Z')

describe('clampProgress', () => {
  it('rounds to a whole percent', () => {
    expect(clampProgress(66.6)).toBe(67)
  })

  it.each([[-10, 0], [0, 0], [50, 50], [100, 100], [140, 100]])(
    'clamps %i to %i so a bad value cannot render a broken bar',
    (input, expected) => {
      expect(clampProgress(input)).toBe(expected)
    },
  )

  it('treats a value that is not a number as no progress', () => {
    expect(clampProgress(Number.NaN)).toBe(0)
  })
})

function line(overrides: Partial<NotificationLine> = {}): NotificationLine {
  return {
    id: 'line-1',
    kind: 'ReactionReceived',
    actor: {
      id: 'person-lena',
      displayName: 'Lena',
      handle: '@lena',
      initials: 'L',
      avatarColor: '#4f46e5',
      isOnline: false,
      avatarImageId: null,
    },
    subject: 'Jeden Tag laufen',
    excerpt: null,
    amount: null,
    target: 'Goal',
    targetId: 'goal-1',
    occurredAt: '2026-06-17T08:45:00Z',
    isNew: true,
    ...overrides,
  }
}

const everyKind: readonly NotificationKind[] = [
  'MessageReceived',
  'FriendRequestReceived',
  'FriendshipStarted',
  'ProofAwaitingVote',
  'ProofConfirmed',
  'ProofRefused',
  'ReactionReceived',
  'GoalInvitation',
  'GoalPaused',
  'PauseLifted',
  'FriendWindowAtRisk',
  'ChallengePublished',
]

/**
 * What a notification says. One function for the bell and the lock screen, so
 * these assertions hold for both.
 */
describe('notificationText', () => {
  it('names who and says what, and joins the two for a lock screen', () => {
    const text = notificationText(line(), de)

    expect(text.title).toBe('Kudos')
    expect(text.who).toBe('Lena')
    expect(text.text).toBe('hat auf deinen Beweis für „Jeden Tag laufen“ reagiert.')
    expect(text.body).toBe('Lena hat auf deinen Beweis für „Jeden Tag laufen“ reagiert.')
  })

  it('makes several people reacting to one thing one line, in the plural', () => {
    const text = notificationText(line({ amount: 3 }), de)

    expect(text.who).toBe('Lena und 2 weitere')
    expect(text.text).toBe('haben auf deinen Beweis für „Jeden Tag laufen“ reagiert.')
  })

  it('names nobody for a verdict — doubt is anonymous', () => {
    const confirmed = notificationText(line({ kind: 'ProofConfirmed', actor: null }), de)

    expect(confirmed.who).toBeNull()
    expect(confirmed.body).toBe('Dein Beweis für „Jeden Tag laufen“ wurde bestätigt.')

    // Not even if a name arrived, which the server never sends.
    expect(notificationText(line({ kind: 'ProofRefused', amount: 1 }), de).who).toBeNull()
  })

  it('says whether a refused proof can be tried again', () => {
    expect(notificationText(line({ kind: 'ProofRefused', actor: null, amount: 1 }), de).text)
      .toContain('noch einen Versuch')
    expect(notificationText(line({ kind: 'ProofRefused', actor: null, amount: 0 }), de).text)
      .not.toContain('Versuch')
  })

  it('puts a message\'s own words under who wrote them, and where', () => {
    const direct = notificationText(
      line({ kind: 'MessageReceived', target: 'Conversation', subject: null, excerpt: 'Bis gleich!' }),
      de,
    )
    const group = notificationText(
      line({ kind: 'MessageReceived', target: 'Conversation', subject: 'Laufgruppe', excerpt: 'Wer kommt?' }),
      de,
    )

    expect(direct).toEqual({ title: 'Lena', who: null, text: 'Bis gleich!', body: 'Bis gleich!' })
    expect(group.title).toBe('Lena in Laufgruppe')
  })

  it('says how long a pause is, and there is no field that could say why', () => {
    expect(notificationText(line({ kind: 'GoalPaused', amount: 3 }), de).text)
      .toBe('setzt „Jeden Tag laufen“ für 3 Tage aus.')
  })

  it('stands in for somebody whose account is gone', () => {
    expect(notificationText(line({ actor: null }), de).who).toBe('Jemand')
  })

  it('speaks the language it is given', () => {
    expect(notificationText(line({ kind: 'FriendshipStarted', target: 'Person', subject: null }), en).body)
      .toBe('Lena and you are friends now.')
  })

  it('has a whole sentence for every kind, in both languages', () => {
    for (const messages of [de, en]) {
      for (const kind of everyKind) {
        const text = notificationText(line({ kind }), messages)

        expect(text.title, kind).toBeTruthy()
        expect(text.body, kind).toBeTruthy()
        expect(text.body, kind).not.toContain('undefined')
      }
    }
  })
})

describe('notificationLink', () => {
  const cases: Array<[string, NotificationLine, string]> = [
    ['a message, to its conversation', line({ kind: 'MessageReceived', target: 'Conversation', targetId: 'chat-1' }), '/chats/chat-1'],
    ['a friend request, to where requests are answered', line({ kind: 'FriendRequestReceived', target: 'Person', targetId: null }), '/search'],
    ['a new friendship, to the friend', line({ kind: 'FriendshipStarted', target: 'Person' }), '/people/person-lena'],
    ['a friend about to miss, to the friend rather than their goal', line({ kind: 'FriendWindowAtRisk' }), '/people/person-lena'],
    ['a proof to vote on, to the vote', line({ kind: 'ProofAwaitingVote' }), '/vote'],
    ['a verdict, to the goal', line({ kind: 'ProofConfirmed', actor: null }), '/goals/goal-1'],
    ['an invitation, to the goal', line({ kind: 'GoalInvitation' }), '/goals/goal-1'],
    ['a reaction to a proof, to the goal', line(), '/goals/goal-1'],
    ['a reaction to a message, to the conversation', line({ target: 'Conversation', targetId: 'chat-1' }), '/chats/chat-1'],
    ['a reaction to a contribution, to the challenge', line({ target: 'Challenge', targetId: 'challenge-1' }), '/challenge'],
    ['kudos on the feed, to your own history', line({ target: 'Activity', targetId: 'activity-1' }), '/profile'],
    ['a new challenge, to the challenge', line({ kind: 'ChallengePublished', actor: null, target: 'Challenge' }), '/challenge'],
  ]

  it.each(cases)('leads %s', (_name, input, path) => {
    expect(notificationLink(input)).toBe(path)
  })
})

describe('notificationIcon', () => {
  it('draws a bundled Lucide icon for every kind', () => {
    for (const kind of everyKind) {
      expect(notificationIcon(line({ kind }))).toMatch(/^i-lucide-/)
    }
  })

  it('tells a confirmed proof from a refused one by shape, not by colour', () => {
    expect(notificationIcon(line({ kind: 'ProofConfirmed' })))
      .not.toBe(notificationIcon(line({ kind: 'ProofRefused' })))
  })
})

describe('goalStatusPresentation', () => {
  it('gives every status a label and an icon, so colour is never the only signal', () => {
    for (const status of ['Active', 'Completed', 'Archived'] as const) {
      const presentation = goalStatusPresentation(status, de)

      expect(presentation.label).not.toBe('')
      expect(presentation.icon).toMatch(/^i-lucide-/)
    }
  })

  it('speaks the language it is given', () => {
    expect(goalStatusPresentation('Completed', de).label).toBe('Abgeschlossen')
    expect(goalStatusPresentation('Completed', en).label).toBe('Completed')
  })
})

describe('goalIconName', () => {
  it('maps a server icon onto the bundled icon set', () => {
    expect(goalIconName('medal')).toBe('i-lucide-medal')
  })
})

describe('formatClock', () => {
  it('shows a reminder time as written, whatever the reader\'s zone', () => {
    // A TimeOnly is wall-clock already; shifting it would move somebody's
    // seven-in-the-morning run.
    expect(formatClock('07:00:00', 120)).toBe('07:00')
  })

  it('pads a single-digit hour', () => {
    expect(formatClock('7:5:00')).toBe('07:05')
  })

  it('shows an instant in UTC when no offset is known', () => {
    expect(formatClock('2026-06-17T09:05:00+00:00')).toBe('09:05')
  })

  it('shifts an instant into the reader\'s zone once it is known', () => {
    // This is what turns the server-rendered 17:55 into a local 19:55 after
    // hydration.
    expect(formatClock('2026-06-17T17:55:00+00:00', 120)).toBe('19:55')
  })

  it('returns null rather than a broken string for nonsense', () => {
    expect(formatClock(null)).toBeNull()
    expect(formatClock('not a time')).toBeNull()
  })
})

describe('formatRelativeTime', () => {
  it.each([
    ['2026-06-17T09:00:00Z', 'gerade eben'],
    ['2026-06-17T08:48:00Z', 'vor 12 Min'],
    ['2026-06-17T06:00:00Z', 'vor 3 Std'],
    ['2026-06-16T09:00:00Z', 'gestern'],
    ['2026-06-13T09:00:00Z', 'vor 4 Tagen'],
  ])('reads %s as "%s"', (iso, expected) => {
    expect(formatRelativeTime(iso, now, de)).toBe(expected)
  })

  it('never reports a negative age when a clock is slightly ahead', () => {
    expect(formatRelativeTime('2026-06-17T09:00:30Z', now, de)).toBe('gerade eben')
  })

  it('translates', () => {
    expect(formatRelativeTime('2026-06-16T09:00:00Z', now, en)).toBe('yesterday')
  })
})

describe('formatChatTime', () => {
  it('shows the clock for today', () => {
    expect(formatChatTime('2026-06-17T08:30:00Z', now, de)).toBe('08:30')
  })

  it('says "gestern" for yesterday', () => {
    expect(formatChatTime('2026-06-16T20:00:00Z', now, de)).toBe('gestern')
  })

  it('names the weekday within the week', () => {
    expect(formatChatTime('2026-06-15T20:00:00Z', now, de)).toBe('Mo')
  })

  it('falls back to a date beyond a week', () => {
    expect(formatChatTime('2026-06-01T20:00:00Z', now, de)).toBe('1.6.')
  })

  it('decides "today" in the reader\'s zone, not the server\'s', () => {
    // 23:30 UTC is already the next day in Berlin, and the row has to say so.
    expect(formatChatTime('2026-06-16T23:30:00Z', now, de, 120)).toBe('01:30')
  })

  it('returns an empty string for a conversation with no messages', () => {
    expect(formatChatTime(null, now, de)).toBe('')
  })
})

describe('formatAmount', () => {
  it('writes a decimal the way the language does', () => {
    expect(formatAmount(1.2, ',')).toBe('1,2')
    expect(formatAmount(1.2, '.')).toBe('1.2')
  })

  it('leaves a whole number whole', () => {
    expect(formatAmount(2, ',')).toBe('2')
  })
})

describe('formatDay', () => {
  it('writes a day the short way, without a leading zero', () => {
    expect(formatDay('2026-06-05')).toBe('5.6.')
  })

  it('leaves something that is not a day alone rather than inventing one', () => {
    expect(formatDay('nonsense')).toBe('nonsense')
  })
})

describe('scheduleLabel', () => {
  function schedule(overrides: Partial<GoalSchedule> = {}): GoalSchedule {
    return { kind: 'Interval', everyDays: 1, weekdays: [], times: null, period: null, ...overrides }
  }

  it('says every day rather than every one day', () => {
    expect(scheduleLabel(schedule(), de)).toBe('Jeden Tag')
    expect(scheduleLabel(schedule(), en)).toBe('Every day')
  })

  it('says a week rather than seven days', () => {
    expect(scheduleLabel(schedule({ everyDays: 7 }), de)).toBe('Jede Woche')
  })

  it('names an interval that is neither', () => {
    expect(scheduleLabel(schedule({ everyDays: 3 }), de)).toBe('Alle 3 Tage')
  })

  it('shortens a run of weekdays into a range', () => {
    const weekdays = schedule({
      kind: 'Weekdays',
      everyDays: null,
      weekdays: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'],
    })

    expect(scheduleLabel(weekdays, de)).toBe('Mo–Fr')
  })

  it('lists weekdays that do not run together', () => {
    const weekdays = schedule({ kind: 'Weekdays', everyDays: null, weekdays: ['Monday', 'Thursday'] })

    expect(scheduleLabel(weekdays, de)).toBe('Mo, Do')
  })

  it('calls all seven weekdays what they are', () => {
    const all = schedule({
      kind: 'Weekdays',
      everyDays: null,
      weekdays: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'],
    })

    expect(scheduleLabel(all, de)).toBe('Jeden Tag')
  })

  it('says a quota with its period', () => {
    const quota = schedule({ kind: 'Times', everyDays: null, times: 3, period: 'Week' })

    expect(scheduleLabel(quota, de)).toBe('3× pro Woche')
    expect(scheduleLabel(quota, en)).toBe('3× per week')
  })

  it('explains what the choice actually means', () => {
    expect(scheduleExplanation(schedule(), de)).toContain('Mitternacht')
    expect(scheduleExplanation(schedule({ kind: 'Once', everyDays: null }), de)).toContain('Einmal')
    expect(scheduleExplanation(schedule({ kind: 'Times', everyDays: null, times: 3, period: 'Week' }), de))
      .toContain('3 Nachweise')
  })
})

describe('windowLabel', () => {
  function window(overrides: Partial<GoalWindow> = {}): GoalWindow {
    return {
      id: 'w',
      startsOn: '2026-06-15',
      dueOn: '2026-06-21',
      dueAt: '2026-06-21T21:59:59+00:00',
      requiredProofs: 3,
      confirmedProofs: 1,
      remainingProofs: 2,
      status: 'Open',
      ...overrides,
    }
  }

  it('says what is left of a quota window', () => {
    expect(windowLabel(window(), de)).toBe('Noch 2 von 3')
    expect(windowLabel(window(), en)).toBe('2 of 3 left')
  })

  it('says a single-day window is due today rather than "noch 1 von 1"', () => {
    const today = window({ requiredProofs: 1, confirmedProofs: 0, remainingProofs: 1, startsOn: '2026-06-21' })

    expect(windowLabel(today, de)).toBe('Heute fällig')
  })

  it('names the day a longer one-proof window runs to', () => {
    const later = window({ requiredProofs: 1, confirmedProofs: 0, remainingProofs: 1 })

    expect(windowLabel(later, de)).toBe('Bis 21.6.')
  })

  it('derives how full it is rather than being told', () => {
    expect(windowPercent(window())).toBe(33)
    expect(windowPercent(window({ confirmedProofs: 3, remainingProofs: 0 }))).toBe(100)
  })

  it('names an outcome in words as well as an icon', () => {
    expect(windowOutcome(window({ status: 'Done' }), de).label).toBe('Geschafft')
    expect(windowOutcome(window({ status: 'Missed' }), de).label).toBe('Verpasst')
    expect(windowOutcome(window(), de).label).toBe('Offen')
  })
})

describe('activitySentence', () => {
  function activity(overrides: Partial<Activity>): Activity {
    return {
      id: 'a',
      actor: {
        id: 'p',
        displayName: 'Jonas Weber',
        handle: '@jonas.w',
        initials: 'JW',
        avatarColor: '#4f46e5',
        avatarImageId: null,
        isOnline: true,
      },
      kind: 'TaskCompleted',
      subject: 'Joggen 5 km',
      amount: null,
      kudosCount: 8,
      hasMyKudos: false,
      occurredAt: '2026-06-17T08:48:00+00:00',
      ...overrides,
    }
  }

  it('composes the sentence from the parts the server sent', () => {
    expect(activitySentence(activity({}), de)).toBe('hat „Joggen 5 km“ abgeschlossen')
    expect(activitySentence(activity({}), en)).toBe('completed “Joggen 5 km”')
  })

  it('uses the amount for a streak', () => {
    const entry = activity({ kind: 'StreakReached', subject: null, amount: 7 })

    expect(activitySentence(entry, de)).toContain('7')
    expect(activitySentence(entry, en)).toContain('7')
  })

  it('covers every kind the contract can produce', () => {
    for (const kind of ['TaskCompleted', 'StreakReached', 'GoalProgress', 'GoalCreated'] as const) {
      expect(activitySentence(activity({ kind, amount: 5 }), de)).not.toBe('')
    }
  })
})
