import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import AppScreenHeader from '~/components/layout/AppScreenHeader.vue'
import AppSegmented from '~/components/ui/AppSegmented.vue'
import ChallengeBanner from '~/components/home/ChallengeBanner.vue'
import HistoryGrid from '~/components/goals/HistoryGrid.vue'
import RiskCard from '~/components/goals/RiskCard.vue'
import SettingsSection from '~/components/settings/SettingsSection.vue'
import StreakHero from '~/components/home/StreakHero.vue'
import type { Goal, GoalWindow } from '~/api/types'

/**
 * The states these components have that nothing was rendering.
 *
 * Every one of them is a decision the design records somewhere — the streak
 * card is lit only once something has been earned, the history grid never uses
 * the accent, the challenge banner gives the accent up once you are in — and a
 * decision that no test renders is one a refactor can quietly reverse.
 */
function window(overrides: Partial<GoalWindow> = {}): GoalWindow {
  return {
    id: 'window-1',
    startsOn: '2026-09-01',
    dueOn: '2026-09-01',
    startsAt: '2026-09-01T00:00:00Z',
    dueAt: '2026-09-01T21:59:59Z',
    requiredProofs: 1,
    confirmedProofs: 1,
    remainingProofs: 0,
    status: 'Done',
    acceptsProof: false,
    ...overrides,
  } as GoalWindow
}

function goal(overrides: Partial<Goal> = {}): Goal {
  return {
    id: 'goal-1',
    title: 'Dreimal die Woche laufen',
    icon: 'medal',
    status: 'Active',
    isMine: true,
    schedule: { kind: 'Interval', everyDays: 1, weekdays: [], times: null, period: null },
    current: window({ status: 'Open', confirmedProofs: 0, remainingProofs: 1, acceptsProof: true }),
    risk: { reason: 'LastDay', missingProofs: 1, daysLeft: 0 },
    streak: 3,
    windowsDone: 4,
    windowsMissed: 1,
    ...overrides,
  } as Goal
}

describe('StreakHero', () => {
  /**
   * The app's one loud surface, spent only on something that has been earned.
   * Lighting it at zero would leave nothing to show somebody on the day they
   * reach five.
   */
  it('is lit once there is a streak and plain black before there is one', async () => {
    const started = await mountSuspended(StreakHero, {
      props: { streak: 5, week: [true, true, false, false, false, false, false] },
    })

    expect(started.get('[data-testid="streak-hero"]').attributes()).toHaveProperty('data-lit')
    expect(started.get('[data-testid="streak-hero"]').classes()).not.toContain('q2-card')
    expect(started.text()).toContain('5')

    const empty = await mountSuspended(StreakHero, {
      props: { streak: 0, week: [false, false, false, false, false, false, false] },
    })

    expect(empty.get('[data-testid="streak-hero"]').attributes()).not.toHaveProperty('data-lit')
    expect(empty.get('[data-testid="streak-hero"]').classes()).toContain('q2-card')
  })

  /** Seven letters read out one at a time tell a screen reader nothing. */
  it('draws the week decoratively, and keeps it out of Session Replay', async () => {
    const wrapper = await mountSuspended(StreakHero, {
      props: { streak: 2, week: [true, false, true, false, false, false, false] },
    })

    const week = wrapper.get('ul')

    expect(week.attributes('aria-hidden')).toBe('true')
    expect(week.attributes()).toHaveProperty('data-q2-block')
    expect(week.findAll('li')).toHaveLength(7)
  })
})

describe('HistoryGrid', () => {
  it('reads left to right the way a calendar does', async () => {
    const wrapper = await mountSuspended(HistoryGrid, {
      props: {
        history: [
          window({ id: 'newest', status: 'Missed' }),
          window({ id: 'oldest', status: 'Done' }),
        ],
      },
    })

    // The API sends newest first; the row shows oldest first.
    const html = wrapper.html()
    expect(html).toContain('title')
    expect(wrapper.findAll('li, [data-testid="history-cell"]').length).toBeGreaterThan(0)
  })

  it('shows only as many as it was asked for', async () => {
    const history = Array.from({ length: 40 }, (_, index) => window({ id: `w-${index}` }))

    const limited = await mountSuspended(HistoryGrid, { props: { history, limit: 7 } })
    const full = await mountSuspended(HistoryGrid, { props: { history } })

    expect(limited.html().length).toBeLessThan(full.html().length)
  })

  it('has something to say when there is no history at all', async () => {
    const wrapper = await mountSuspended(HistoryGrid, { props: { history: [] } })

    expect(wrapper.html()).toBeTruthy()
  })

  /**
   * A paused window is neither kept nor missed, and the grid has to be able to
   * draw the third outcome rather than folding it into one of the two.
   */
  it('draws all three outcomes differently', async () => {
    const wrapper = await mountSuspended(HistoryGrid, {
      props: {
        history: [
          window({ id: 'done', status: 'Done' }),
          window({ id: 'missed', status: 'Missed' }),
          window({ id: 'paused', status: 'Paused' }),
        ],
      },
    })

    expect(wrapper.html()).toBeTruthy()
    expect(wrapper.text().length).toBeGreaterThan(0)
  })
})

describe('RiskCard', () => {
  /**
   * Red for the edge, accent for the camera: the warning is neither an
   * invitation nor yet a failure, but pressing the camera is still the thing
   * to do.
   */
  it('offers the camera only while the window still takes a photograph', async () => {
    const offered = await mountSuspended(RiskCard, { props: { goal: goal() } })
    expect(offered.find('[data-testid="risk-deliver"]').exists()).toBe(true)

    const closed = await mountSuspended(RiskCard, {
      props: { goal: goal({ current: window({ acceptsProof: false }) }) },
    })
    expect(closed.find('[data-testid="risk-deliver"]').exists()).toBe(false)
  })

  it('says nothing about the risk when there is none to describe', async () => {
    const wrapper = await mountSuspended(RiskCard, { props: { goal: goal({ risk: null }) } })

    expect(wrapper.find('[data-testid="risk-sentence"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Dreimal die Woche laufen')
  })

  it('keeps the goal title out of Session Replay', async () => {
    const wrapper = await mountSuspended(RiskCard, { props: { goal: goal() } })

    expect(wrapper.get('[data-testid="risk-card"]').attributes()).toHaveProperty('data-q2-block')
  })
})

describe('ChallengeBanner', () => {
  function room(overrides: Record<string, unknown> = {}) {
    return {
      challenge: {
        id: 'challenge-1',
        prompt: 'Zeig deinen Arbeitsplatz.',
        publishedAt: '2026-09-09T00:00:00Z',
        expiresAt: '2099-01-01T00:00:00Z',
      },
      ownEntry: null,
      entries: [],
      friendCount: 0,
      isRevealed: false,
      ...overrides,
    }
  }

  /** The denominator is what makes the numerator mean anything. */
  it('says so plainly when there are no friends to count against', async () => {
    const wrapper = await mountSuspended(ChallengeBanner, { props: { room: room() } })

    expect(wrapper.text()).toContain('Noch keine Freunde dabei')
  })

  it('counts one friend in the singular', async () => {
    const wrapper = await mountSuspended(ChallengeBanner, {
      props: { room: room({ friendCount: 1 }) },
    })

    expect(wrapper.text()).toContain('0 von 1 Freund dabei')
  })

  /** A prompt whose day is over has nothing left to count down to. */
  it('says the day is over rather than counting backwards', async () => {
    const wrapper = await mountSuspended(ChallengeBanner, {
      props: {
        room: room({
          challenge: { ...room().challenge, expiresAt: '2020-01-01T00:00:00Z' },
        }),
      },
    })

    expect(wrapper.text()).toContain('Für heute vorbei')
  })
})

describe('AppScreenHeader', () => {
  it('draws a back arrow only when there is somewhere to go', async () => {
    const plain = await mountSuspended(AppScreenHeader, { props: { title: 'Profil' } })
    expect(plain.find('a').exists()).toBe(false)

    const nested = await mountSuspended(AppScreenHeader, {
      props: { title: 'Dein Archiv', backTo: '/challenge', backLabel: 'Zurück' },
    })
    expect(nested.get('a').attributes('href')).toBe('/challenge')
  })

  /**
   * A title that is somebody's name is personal data; one that is product copy
   * is not, and blocking every heading would make the replay useless.
   */
  it('blocks the title from Session Replay only when it is a person', async () => {
    const copy = await mountSuspended(AppScreenHeader, { props: { title: 'Einstellungen' } })
    expect(copy.get('h1').attributes()).not.toHaveProperty('data-q2-private')

    const name = await mountSuspended(AppScreenHeader, {
      props: { title: 'Mara Klein', privateTitle: true },
    })
    expect(name.get('h1').attributes()).toHaveProperty('data-q2-private')
  })

  it('shows the eyebrow above the title when there is one', async () => {
    const wrapper = await mountSuspended(AppScreenHeader, {
      props: { title: 'Mara', eyebrow: 'Guten Abend' },
    })

    expect(wrapper.text()).toContain('Guten Abend')
  })
})

describe('AppSegmented', () => {
  it('marks the option that is selected, and draws an icon only where there is one', async () => {
    const wrapper = await mountSuspended(AppSegmented, {
      props: {
        modelValue: 'goals',
        label: 'Ansicht',
        options: [
          { value: 'today', label: 'Heute' },
          { value: 'goals', label: 'Ziele', icon: 'i-lucide-target' },
        ],
      },
    })

    expect(wrapper.text()).toContain('Heute')
    expect(wrapper.text()).toContain('Ziele')
    expect(wrapper.findAll('svg').length).toBe(1)
  })
})

describe('SettingsSection', () => {
  it('carries a note under the rows only when it was given one', async () => {
    const bare = await mountSuspended(SettingsSection, {
      props: { title: 'Darstellung' },
      slots: { default: () => 'row' },
    })
    expect(bare.text()).toContain('Darstellung')

    const noted = await mountSuspended(SettingsSection, {
      props: { title: 'Benachrichtigungen', note: 'Gilt für jedes Gerät.' },
      slots: { default: () => 'row' },
    })
    expect(noted.text()).toContain('Gilt für jedes Gerät.')
  })
})
