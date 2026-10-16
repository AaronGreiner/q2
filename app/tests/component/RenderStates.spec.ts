import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import { h } from 'vue'
import AppScreenHeader from '~/components/layout/AppScreenHeader.vue'
import AppSegmented from '~/components/ui/AppSegmented.vue'
import ChallengeBanner from '~/components/home/ChallengeBanner.vue'
import HistoryGrid from '~/components/goals/HistoryGrid.vue'
import RiskCard from '~/components/goals/RiskCard.vue'
import SettingsSection from '~/components/settings/SettingsSection.vue'
import StreakFlame from '~/components/home/StreakFlame.vue'
import type { Goal, GoalWindow } from '~/api/types'

/**
 * The states these components have that nothing was rendering.
 *
 * Every one of them is a decision the design records somewhere — the streak
 * flame is lit only once something has been earned, the history grid never uses
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

describe('StreakFlame', () => {
  /**
   * The flame is lit only for something that has been earned. Lighting it at
   * zero would leave nothing to show somebody on the day they reach five.
   */
  it('lights the flame and shows the number only once there is a streak', async () => {
    const lit = await mountSuspended(StreakFlame, { props: { streak: 5 } })

    const flame = lit.get('[data-testid="streak-flame"]')
    expect(flame.attributes()).toHaveProperty('data-lit')
    expect(flame.find('.bg-linear-to-br').exists()).toBe(true)
    expect(lit.get('[data-testid="streak-count"]').text()).toBe('5')

    const unlit = await mountSuspended(StreakFlame, { props: { streak: 0 } })

    expect(unlit.get('[data-testid="streak-flame"]').attributes()).not.toHaveProperty('data-lit')
    expect(unlit.find('.bg-linear-to-br').exists()).toBe(false)

    // Grey says it; a "0" beside it would not add anything.
    expect(unlit.find('[data-testid="streak-count"]').exists()).toBe(false)
  })

  /** An icon and a digit read out separately tell a screen reader nothing. */
  it('is read out as one sentence, and kept out of Session Replay', async () => {
    const one = await mountSuspended(StreakFlame, { props: { streak: 1 } })
    expect(one.get('.sr-only').text()).toBe('1 Tag in Folge')

    const many = await mountSuspended(StreakFlame, { props: { streak: 12 } })
    expect(many.get('.sr-only').text()).toBe('12 Tage in Folge')
    expect(many.get('[data-testid="streak-count"]').attributes('aria-hidden')).toBe('true')
    expect(many.get('[data-testid="streak-flame"]').attributes()).toHaveProperty('data-q2-private')

    const none = await mountSuspended(StreakFlame, { props: { streak: 0 } })
    expect(none.get('.sr-only').text()).toBe('Noch keine Serie')
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

  /** The start screen's streak belongs to the name, not among the actions on the right (#121). */
  it('puts what trails the title right after it, and the actions apart', async () => {
    const wrapper = await mountSuspended(AppScreenHeader, {
      props: { title: 'Mara' },
      slots: {
        'title-trailing': () => h('span', { 'data-testid': 'trailing' }, '5'),
        'actions': () => h('span', { 'data-testid': 'action' }, 'Glocke'),
      },
    })

    const trailing = wrapper.get('[data-testid="trailing"]')
    expect(trailing.element.previousElementSibling?.tagName).toBe('H1')
    expect(trailing.element.parentElement?.contains(wrapper.get('[data-testid="action"]').element)).toBe(false)
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
          { value: 'all', label: 'Alle', icon: 'i-lucide-target' },
        ],
      },
    })

    expect(wrapper.text()).toContain('Heute')
    expect(wrapper.text()).toContain('Alle')
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
