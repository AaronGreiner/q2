import { mountSuspended } from '@nuxt/test-utils/runtime'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import AppTour from '~/components/layout/AppTour.vue'
import GoalDraftCard from '~/components/goals/GoalDraftCard.vue'
import GoalDraftForm from '~/components/goals/GoalDraftForm.vue'
import OnboardingAppearance from '~/components/onboarding/OnboardingAppearance.vue'
import OnboardingDone from '~/components/onboarding/OnboardingDone.vue'
import OnboardingFirstDo from '~/components/onboarding/OnboardingFirstDo.vue'
import OnboardingFrame from '~/components/onboarding/OnboardingFrame.vue'
import OnboardingIllustration from '~/components/onboarding/OnboardingIllustration.vue'
import OnboardingInvite from '~/components/onboarding/OnboardingInvite.vue'
import OnboardingNotifications from '~/components/onboarding/OnboardingNotifications.vue'
import OnboardingProfile from '~/components/onboarding/OnboardingProfile.vue'
import OnboardingWelcome from '~/components/onboarding/OnboardingWelcome.vue'
import type { ApiFailure } from '~/api/errors'
import type { Friend, GoalDraft, HandleCheck, Person } from '~/api/types'
import { de } from '~/i18n/messages'

/**
 * The first-run introduction, step by step (docs/adr/0039-first-run-onboarding.md).
 *
 * What is worth pinning down: every optional step can be put off, nothing is
 * accented that cannot be pressed, the handle is only taken when the server
 * said it is free *for what is in the field*, and the first Do becomes a goal
 * with a friend and a waiting Do without one.
 */
function person(overrides: Partial<Person> = {}): Person {
  return {
    id: 'person-1',
    displayName: 'Nora Neu',
    handle: '@nora.neu',
    initials: 'NN',
    avatarColor: '#0f766e',
    avatarImageId: null,
    isOnline: true,
    ...overrides,
  }
}

function friend(id = 'friend-1', displayName = 'Lena Mai'): Friend {
  return {
    person: { ...person({ id, displayName, handle: `@${id}`, initials: 'LM' }) },
    streak: 2,
    lastSeenAt: null,
  }
}

function draft(overrides: Partial<GoalDraft> = {}): GoalDraft {
  return {
    id: 'draft-1',
    title: 'Jeden Abend lesen',
    icon: 'book-open',
    schedule: { kind: 'Times', everyDays: null, weekdays: [], times: 3, period: 'Week' },
    reminderAt: '19:30:00',
    createdAt: '2026-10-12T09:00:00Z',
    ...overrides,
  }
}

function failure(kind: ApiFailure['kind'] = 'server'): ApiFailure {
  return { kind, isExpected: false, status: 500, fieldErrors: {}, traceId: null, errorId: null, reason: null }
}

afterEach(() => {
  document.body.replaceChildren()
})

describe('OnboardingFrame', () => {
  it('says where somebody is, in the text colour rather than the accent', async () => {
    const wrapper = await mountSuspended(OnboardingFrame, {
      props: { step: 2, total: 5, eyebrow: 'Profil', heading: 'Wer bist du?', intro: 'Kurz.' },
    })

    const progress = wrapper.get('[data-testid="onboarding-progress"]')
    expect(progress.attributes('role')).toBe('progressbar')
    expect(progress.attributes('aria-valuenow')).toBe('2')
    expect(progress.attributes('aria-valuemax')).toBe('5')
    expect(progress.attributes('aria-valuetext')).toBe(de.onboarding.progress(2, 5))

    const bars = progress.findAll('span')
    expect(bars).toHaveLength(5)
    expect(bars.filter(bar => bar.classes().includes('bg-(--ui-text)'))).toHaveLength(2)
    expect(progress.html()).not.toContain('primary')
    expect(progress.html()).not.toContain('q2-accent')

    expect(wrapper.get('h1').text()).toBe('Wer bist du?')
    expect(wrapper.get('[data-testid="onboarding-intro"]').text()).toBe('Kurz.')
  })

  it('offers a way back, and a way past only when the step can be put off', async () => {
    const wrapper = await mountSuspended(OnboardingFrame, {
      props: { step: 3, total: 5, heading: 'Schritt', skipLabel: de.onboarding.later },
    })

    await wrapper.get('[data-testid="onboarding-back"]').trigger('click')
    await wrapper.get('[data-testid="onboarding-skip"]').trigger('click')

    expect(wrapper.emitted('back')).toHaveLength(1)
    expect(wrapper.emitted('skip')).toHaveLength(1)

    await wrapper.setProps({ skipLabel: null, canGoBack: false })
    expect(wrapper.find('[data-testid="onboarding-skip"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="onboarding-back"]').exists()).toBe(false)
  })

  it('moves focus to a new step\'s heading so it is announced', async () => {
    const wrapper = await mountSuspended(OnboardingFrame, {
      props: { step: 1, total: 3, heading: 'Erster' },
      attachTo: document.body,
    })

    await wrapper.setProps({ step: 2, heading: 'Zweiter' })
    await new Promise(resolve => setTimeout(resolve, 0))

    expect(document.activeElement?.textContent?.trim()).toBe('Zweiter')
    wrapper.unmount()
  })

  it('draws the actions slot at the bottom only when there is one', async () => {
    const without = await mountSuspended(OnboardingFrame, { props: { step: 1, total: 2 } })
    expect(without.find('h1').exists()).toBe(false)

    const withActions = await mountSuspended(OnboardingFrame, {
      props: { step: 1, total: 2 },
      slots: { actions: () => 'Los' },
    })
    expect(withActions.text()).toContain('Los')
  })
})

describe('OnboardingIllustration', () => {
  it.each(['promise', 'proof', 'streak'] as const)('draws %s from the app\'s own parts, hidden from assistive technology', async (kind) => {
    const wrapper = await mountSuspended(OnboardingIllustration, { props: { kind } })

    const root = wrapper.get(`[data-testid="onboarding-illustration-${kind}"]`)
    expect(root.attributes('aria-hidden')).toBe('true')

    // The accent is for what can be pressed now; nothing here can be.
    expect(root.html()).not.toContain('q2-accent')
    expect(root.html()).not.toContain('primary')

    // And the flame gradient only where it means a streak.
    expect(root.html().includes('flame-from')).toBe(kind === 'streak')
  })

  it('shows sample words from the catalogue, never a real person', async () => {
    const wrapper = await mountSuspended(OnboardingIllustration, { props: { kind: 'promise' } })

    expect(wrapper.text()).toContain(de.onboarding.welcome.sample.read)
    expect(wrapper.text()).toContain(de.onboarding.welcome.sample.phone)
  })
})

describe('OnboardingWelcome', () => {
  it('says what Qdos is in three slides, and starts after the last one', async () => {
    const wrapper = await mountSuspended(OnboardingWelcome, {
      props: { step: 1, total: 6, language: 'German' },
      attachTo: document.body,
    })

    expect(wrapper.findAll('[aria-roledescription="slide"]')).toHaveLength(3)
    expect(wrapper.get('h1').text()).toBe(de.onboarding.title)
    expect(wrapper.text()).toContain(de.onboarding.welcome.promise.heading)

    const button = wrapper.get('[data-testid="onboarding-welcome-next"]')
    expect(button.text()).toBe(de.onboarding.next)

    await button.trigger('click')
    expect(wrapper.get('[data-testid="onboarding-dot-1"]').attributes('aria-current')).toBe('step')

    await button.trigger('click')
    expect(button.text()).toBe(de.onboarding.welcome.start)

    // The last slide has nothing left to skip.
    expect(wrapper.find('[data-testid="onboarding-skip"]').exists()).toBe(false)
    expect(wrapper.emitted('next')).toBeUndefined()

    await button.trigger('click')
    expect(wrapper.emitted('next')).toHaveLength(1)
    wrapper.unmount()
  })

  it('does not let a scroll that is still on its way undo a quick second tap', async () => {
    const wrapper = await mountSuspended(OnboardingWelcome, {
      props: { step: 1, total: 6, language: 'German' },
      attachTo: document.body,
    })

    const track = wrapper.get<HTMLElement>('[data-testid="onboarding-slides"]').element
    Object.defineProperty(track, 'clientWidth', { configurable: true, value: 390 })
    track.scrollTo = () => {}

    const button = wrapper.get('[data-testid="onboarding-welcome-next"]')
    await button.trigger('click')

    // The smooth scroll towards the second slide reports where it passes.
    track.scrollLeft = 60
    await wrapper.get('[data-testid="onboarding-slides"]').trigger('scroll')
    expect(wrapper.get('[data-testid="onboarding-dot-1"]').attributes('aria-current')).toBe('step')

    await button.trigger('click')
    expect(wrapper.get('[data-testid="onboarding-dot-2"]').attributes('aria-current')).toBe('step')
    expect(button.text()).toBe(de.onboarding.welcome.start)

    // Arrived: from here on the position is what counts again — a swipe back.
    track.scrollLeft = 780
    await wrapper.get('[data-testid="onboarding-slides"]').trigger('scroll')
    track.scrollLeft = 0
    await wrapper.get('[data-testid="onboarding-slides"]').trigger('scroll')
    expect(wrapper.get('[data-testid="onboarding-dot-0"]').attributes('aria-current')).toBe('step')
    wrapper.unmount()
  })

  it('can be stepped through with the arrow keys', async () => {
    const wrapper = await mountSuspended(OnboardingWelcome, { props: { step: 1, total: 6, language: 'German' } })
    const track = wrapper.get('[data-testid="onboarding-slides"]')

    expect(track.attributes('tabindex')).toBe('0')

    await track.trigger('keydown', { key: 'ArrowRight' })
    await track.trigger('keydown', { key: 'ArrowRight' })
    await track.trigger('keydown', { key: 'ArrowRight' })
    expect(wrapper.get('[data-testid="onboarding-dot-2"]').attributes('aria-current')).toBe('step')

    await track.trigger('keydown', { key: 'ArrowLeft' })
    expect(wrapper.get('[data-testid="onboarding-dot-1"]').attributes('aria-current')).toBe('step')
  })

  it('lets the slides be skipped, jumped through, and read in English', async () => {
    const wrapper = await mountSuspended(OnboardingWelcome, {
      props: { step: 1, total: 6, language: 'German' },
      attachTo: document.body,
    })

    await wrapper.get('[data-testid="onboarding-dot-2"]').trigger('click')
    expect(wrapper.get('[data-testid="onboarding-welcome-next"]').text()).toBe(de.onboarding.welcome.start)

    await wrapper.get('[data-testid="onboarding-dot-0"]').trigger('click')
    await wrapper.get('[data-testid="onboarding-skip"]').trigger('click')
    expect(wrapper.emitted('skip')).toHaveLength(1)

    await wrapper.get('[role="radio"][value="English"]').trigger('click')
    expect(wrapper.emitted('update:language')?.[0]).toEqual(['English'])
    wrapper.unmount()
  })

  it('offers a way out for somebody on the wrong account', async () => {
    const wrapper = await mountSuspended(OnboardingWelcome, { props: { step: 1, total: 6, language: 'German' } })

    await wrapper.get('[data-testid="onboarding-sign-out"]').trigger('click')
    expect(wrapper.emitted('signOut')).toHaveLength(1)
  })
})

describe('OnboardingProfile', () => {
  function props(overrides: Record<string, unknown> = {}) {
    return {
      step: 2,
      total: 6,
      person: person(),
      handleCheck: null as HandleCheck | null,
      isChecking: false,
      isSaving: false,
      failed: false,
      ...overrides,
    }
  }

  it('shows the handle that was made from the name, and that it is about to be fixed', async () => {
    const wrapper = await mountSuspended(OnboardingProfile, { props: props() })

    const input = wrapper.get<HTMLInputElement>('[data-testid="onboarding-handle"] input, input[data-testid="onboarding-handle"]')
    expect(input.element.value).toBe('nora.neu')
    expect(wrapper.get('[data-testid="onboarding-handle-status"]').text()).toBe(de.onboarding.profile.handleCurrent)
    expect(wrapper.text()).toContain(de.onboarding.profile.handleHint)

    await wrapper.get('form').trigger('submit')
    expect(wrapper.emitted('save')).toEqual([[{ displayName: 'Nora Neu', handle: '@nora.neu' }]])
  })

  it('asks about a new handle, and waits for the answer about exactly that one', async () => {
    const wrapper = await mountSuspended(OnboardingProfile, { props: props() })
    const input = wrapper.get('[data-testid="onboarding-handle"] input, input[data-testid="onboarding-handle"]')

    await input.setValue('@Nora')
    expect(wrapper.emitted('check')?.at(-1)).toEqual(['@nora'])

    // An answer about something else is not an answer.
    await wrapper.setProps({ handleCheck: { handle: '@nor', availability: 'Available' } })
    expect(wrapper.get('[data-testid="onboarding-handle-status"]').text()).toBe(de.onboarding.profile.handleChecking)
    expect(wrapper.get('[data-testid="onboarding-profile-next"]').attributes('disabled')).toBeDefined()

    await wrapper.setProps({ handleCheck: { handle: '@nora', availability: 'Available' } })
    expect(wrapper.get('[data-testid="onboarding-handle-status"]').text()).toBe(de.onboarding.profile.handleAvailable)
    expect(wrapper.get('[data-testid="onboarding-profile-next"]').attributes('disabled')).toBeUndefined()

    await wrapper.get('form').trigger('submit')
    expect(wrapper.emitted('save')?.at(-1)).toEqual([{ displayName: 'Nora Neu', handle: '@nora' }])
  })

  it.each([
    ['Taken', de.onboarding.profile.handleTaken],
    ['Invalid', de.onboarding.profile.handleInvalid],
  ] as const)('refuses a handle that is %s, under the field', async (availability, message) => {
    const wrapper = await mountSuspended(OnboardingProfile, {
      props: props({ handleCheck: { handle: '@test.two', availability } }),
    })

    await wrapper.get('[data-testid="onboarding-handle"] input, input[data-testid="onboarding-handle"]').setValue('test.two')

    expect(wrapper.text()).toContain(message)
    expect(wrapper.get('[data-testid="onboarding-profile-next"]').attributes('disabled')).toBeDefined()

    await wrapper.get('form').trigger('submit')
    expect(wrapper.emitted('save')).toBeUndefined()
  })

  it('does not take an empty name, or an empty handle', async () => {
    const wrapper = await mountSuspended(OnboardingProfile, { props: props() })

    await wrapper.get('[data-testid="onboarding-name"] input, input[data-testid="onboarding-name"]').setValue('  ')
    expect(wrapper.text()).toContain(de.profileEdit.nameRequired)
    expect(wrapper.get('[data-testid="onboarding-profile-next"]').attributes('disabled')).toBeDefined()

    await wrapper.get('[data-testid="onboarding-name"] input, input[data-testid="onboarding-name"]').setValue('Nora')
    await wrapper.get('[data-testid="onboarding-handle"] input, input[data-testid="onboarding-handle"]').setValue('')
    expect(wrapper.text()).toContain(de.onboarding.profile.handleInvalid)
  })

  it('asks for the camera, and says when saving failed', async () => {
    const wrapper = await mountSuspended(OnboardingProfile, { props: props({ failed: true }) })

    await wrapper.get('[data-testid="onboarding-photo"]').trigger('click')
    expect(wrapper.emitted('capture')).toHaveLength(1)
    expect(wrapper.get('[data-testid="onboarding-photo"]').text()).toBe(de.onboarding.profile.addPhoto)

    expect(wrapper.get('[data-testid="onboarding-profile-error"]').attributes('role')).toBe('alert')

    await wrapper.setProps({ person: person({ avatarImageId: 'image-1' }) })
    expect(wrapper.get('[data-testid="onboarding-photo"]').text()).toBe(de.onboarding.profile.changePhoto)
  })

  it('keeps the handle out of Session Replay', async () => {
    const wrapper = await mountSuspended(OnboardingProfile, { props: props() })

    expect(wrapper.find('[data-q2-private]').exists()).toBe(true)
  })
})

describe('OnboardingAppearance', () => {
  it('shows the accent doing its one job, and hands every choice to the page', async () => {
    const wrapper = await mountSuspended(OnboardingAppearance, {
      props: { step: 3, total: 6, theme: 'Dark', accent: 'iris', language: 'German' },
    })

    const preview = wrapper.get('[data-testid="onboarding-preview"]')
    expect(preview.text()).toContain(de.onboarding.appearance.previewAction)
    expect(preview.html()).toContain('q2-accent-solid')

    await wrapper.get('[role="radio"][value="Light"]').trigger('click')
    await wrapper.get('[role="radio"][value="sage"]').trigger('click')
    await wrapper.get('[role="radio"][value="English"]').trigger('click')

    expect(wrapper.emitted('update:theme')?.[0]).toEqual(['Light'])
    expect(wrapper.emitted('update:accent')?.[0]).toEqual(['sage'])
    expect(wrapper.emitted('update:language')?.[0]).toEqual(['English'])

    await wrapper.get('[data-testid="onboarding-appearance-next"]').trigger('click')
    expect(wrapper.emitted('next')).toHaveLength(1)
  })
})

describe('GoalDraftForm', () => {
  it('fills from an idea, without a one-off on offer, and says what was chosen', async () => {
    const wrapper = await mountSuspended(GoalDraftForm, { props: { formId: 'draft-form' } })

    expect(wrapper.find('[data-testid="schedule-kind-Once"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="draft-checkers"]').exists()).toBe(false)

    // Nothing to send without a title.
    await wrapper.get('form').trigger('submit')
    expect(wrapper.emitted('submit')).toBeUndefined()

    await wrapper.get('[data-testid="draft-idea-run"]').trigger('click')
    expect(wrapper.emitted('update:valid')?.at(-1)).toEqual([true])

    await wrapper.get('form').trigger('submit')
    expect(wrapper.emitted('submit')).toEqual([[{
      title: de.create.templates.run,
      icon: 'medal',
      schedule: { kind: 'Times', times: 3, period: 'Week' },
      reminderAt: '19:00:00',
      participantIds: [],
    }]])
  })

  it('starts from a waiting Do when there is one', async () => {
    const wrapper = await mountSuspended(GoalDraftForm, { props: { formId: 'draft-form', initial: draft() } })

    const title = wrapper.get<HTMLInputElement>('[data-testid="draft-title-input"] input, input[data-testid="draft-title-input"]')
    expect(title.element.value).toBe('Jeden Abend lesen')

    await wrapper.get('form').trigger('submit')
    expect(wrapper.emitted('submit')?.[0]?.[0]).toMatchObject({
      title: 'Jeden Abend lesen',
      icon: 'book-open',
      schedule: { kind: 'Times', times: 3, period: 'Week' },
      reminderAt: '19:30:00',
    })
  })

  it('asks who checks it when there is somebody, the only one chosen already', async () => {
    const wrapper = await mountSuspended(GoalDraftForm, {
      props: { formId: 'draft-form', friends: [friend()] },
    })

    expect(wrapper.get('[data-testid="draft-checkers"]').text()).toContain('Lena Mai')

    await wrapper.get('[data-testid="draft-title-input"] input, input[data-testid="draft-title-input"]').setValue('Laufen')
    await wrapper.get('form').trigger('submit')

    expect(wrapper.emitted('submit')?.[0]?.[0]).toMatchObject({ title: 'Laufen', participantIds: ['friend-1'] })
  })

  it('needs a checker to be chosen among several', async () => {
    const wrapper = await mountSuspended(GoalDraftForm, {
      props: { formId: 'draft-form', friends: [friend(), friend('friend-2', 'Jonas Kerr')] },
    })

    await wrapper.get('[data-testid="draft-title-input"] input, input[data-testid="draft-title-input"]').setValue('Laufen')
    await wrapper.get('form').trigger('submit')
    expect(wrapper.emitted('submit')).toBeUndefined()
    expect(wrapper.emitted('update:valid')).toBeUndefined()

    await wrapper.get('[data-testid="draft-checker-friend-2"] [role="checkbox"]').trigger('click')
    expect(wrapper.emitted('update:valid')?.at(-1)).toEqual([true])

    await wrapper.get('form').trigger('submit')
    expect(wrapper.emitted('submit')?.[0]?.[0]).toMatchObject({ participantIds: ['friend-2'] })
  })

  it('shows a failure that belongs to no field', async () => {
    const wrapper = await mountSuspended(GoalDraftForm, { props: { formId: 'draft-form', error: failure('network') } })

    expect(wrapper.get('[data-testid="draft-form-error"]').text()).toBe(de.errors.network)
  })
})

describe('OnboardingFirstDo', () => {
  function props(overrides: Record<string, unknown> = {}) {
    return {
      step: 4,
      total: 6,
      friends: [] as Friend[],
      draft: null as GoalDraft | null,
      ownGoalCount: 0,
      isSaving: false,
      error: null,
      ...overrides,
    }
  }

  it('lets somebody without friends keep their first Do waiting, or put it off', async () => {
    const wrapper = await mountSuspended(OnboardingFirstDo, { props: props(), attachTo: document.body })

    expect(wrapper.get('[data-testid="onboarding-intro"]').text()).toBe(de.onboarding.firstDo.introWaiting)

    const submit = wrapper.get('[data-testid="onboarding-first-do-submit"]')
    expect(submit.text()).toBe(de.onboarding.firstDo.submitWaiting)
    expect(submit.attributes('disabled')).toBeDefined()

    await wrapper.get('[data-testid="draft-idea-read"]').trigger('click')
    expect(submit.attributes('disabled')).toBeUndefined()

    await wrapper.get('form').trigger('submit')
    expect(wrapper.emitted('submit')?.[0]?.[0]).toMatchObject({ title: de.create.templates.read, participantIds: [] })

    await wrapper.get('[data-testid="onboarding-skip"]').trigger('click')
    expect(wrapper.emitted('skip')).toHaveLength(1)
    wrapper.unmount()
  })

  it('makes a real goal with the friend somebody arrived through', async () => {
    const wrapper = await mountSuspended(OnboardingFirstDo, { props: props({ friends: [friend()] }) })

    expect(wrapper.get('[data-testid="onboarding-intro"]').text()).toBe(de.onboarding.firstDo.introWithFriends)
    expect(wrapper.get('[data-testid="onboarding-first-do-submit"]').text()).toBe(de.onboarding.firstDo.submitNow)
  })

  it('does not ask for another Do when somebody\'s are already running', async () => {
    const wrapper = await mountSuspended(OnboardingFirstDo, { props: props({ ownGoalCount: 2 }) })

    expect(wrapper.get('h1').text()).toBe(de.onboarding.firstDo.runningHeading)
    expect(wrapper.get('[data-testid="onboarding-intro"]').text()).toBe(de.onboarding.firstDo.runningIntro(2))
    expect(wrapper.find('[data-testid="goal-draft-form"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="onboarding-skip"]').exists()).toBe(false)

    await wrapper.get('[data-testid="onboarding-first-do-next"]').trigger('click')
    expect(wrapper.emitted('next')).toHaveLength(1)
  })

  it('words a single running Do in the singular', () => {
    expect(de.onboarding.firstDo.runningIntro(1)).not.toContain('1 laufende')
  })
})

describe('OnboardingInvite', () => {
  function props(overrides: Record<string, unknown> = {}) {
    return {
      step: 5,
      total: 6,
      url: 'https://q2.example/join/abc',
      isLoading: false,
      friendNames: [] as string[],
      draft: null as GoalDraft | null,
      hasShared: false,
      ...overrides,
    }
  }

  it('sends the link for the waiting Do, and only then makes "Weiter" the main button', async () => {
    const wrapper = await mountSuspended(OnboardingInvite, { props: props({ draft: draft() }) })

    expect(wrapper.get('h1').text()).toBe(de.onboarding.invite.headingAlone)
    expect(wrapper.get('[data-testid="goal-draft-card"]').text()).toContain('Jeden Abend lesen')
    expect(wrapper.find('[data-testid="goal-draft-share"]').exists()).toBe(false)

    const link = wrapper.get('[data-testid="onboarding-invite-link"]')
    expect(link.text()).toBe('https://q2.example/join/abc')
    expect(link.attributes()).toHaveProperty('data-q2-block')

    expect(wrapper.find('[data-testid="onboarding-invite-next"]').exists()).toBe(false)

    // "Später" is gone: without friends the choice is sharing, or going on
    // without them and letting the Do lapse.
    expect(wrapper.find('[data-testid="onboarding-skip"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="onboarding-invite-leave"]').text()).toBe(de.onboarding.invite.leave)

    await wrapper.get('[data-testid="onboarding-invite-share"]').trigger('click')
    await wrapper.get('[data-testid="onboarding-invite-copy"]').trigger('click')
    expect(wrapper.emitted('share')).toHaveLength(1)
    expect(wrapper.emitted('copy')).toHaveLength(1)

    await wrapper.setProps({ hasShared: true })
    expect(wrapper.get('[data-testid="onboarding-invite-sent"]').text()).toBe(de.onboarding.invite.sent)
    await wrapper.get('[data-testid="onboarding-invite-next"]').trigger('click')
    expect(wrapper.emitted('next')).toHaveLength(1)

    // Shared: the Do keeps waiting, and the way out is no longer offered.
    expect(wrapper.find('[data-testid="onboarding-invite-leave"]').exists()).toBe(false)
  })

  it('asks before letting the waiting Do lapse, and only then leaves', async () => {
    const wrapper = await mountSuspended(OnboardingInvite, { props: props({ draft: draft() }), attachTo: document.body })

    await wrapper.get('[data-testid="onboarding-invite-leave"]').trigger('click')
    await new Promise(resolve => setTimeout(resolve, 0))
    expect(wrapper.emitted('leave')).toBeUndefined()

    const dialog = document.querySelector('[role="dialog"]')
    expect(dialog?.textContent).toContain(de.onboarding.invite.leaveBody('Jeden Abend lesen'))

    document.querySelector<HTMLButtonElement>('[data-testid="confirm-accept"]')!.click()
    await new Promise(resolve => setTimeout(resolve, 0))
    expect(wrapper.emitted('leave')).toHaveLength(1)
    wrapper.unmount()
  })

  it('leaves at once when there is no Do to lose, and says when leaving failed', async () => {
    const wrapper = await mountSuspended(OnboardingInvite, { props: props({ leaveFailed: true }) })

    await wrapper.get('[data-testid="onboarding-invite-leave"]').trigger('click')
    expect(wrapper.emitted('leave')).toHaveLength(1)
    expect(wrapper.get('[data-testid="onboarding-invite-leave-error"]').attributes('role')).toBe('alert')
  })

  it('does not offer the way out to somebody who has friends', async () => {
    const wrapper = await mountSuspended(OnboardingInvite, { props: props({ friendNames: ['Lena'] }) })

    expect(wrapper.find('[data-testid="onboarding-invite-leave"]').exists()).toBe(false)
  })

  it('names the friends somebody already has', async () => {
    const wrapper = await mountSuspended(OnboardingInvite, {
      props: props({ friendNames: ['Lena', 'Jonas', 'Sarah', 'Tom'] }),
    })

    expect(wrapper.get('h1').text()).toBe(de.onboarding.invite.headingFriends)
    expect(wrapper.get('[data-testid="onboarding-intro"]').text()).toContain('Lena, Jonas und 2 weiteren')
    expect(wrapper.find('[data-testid="onboarding-invite-next"]').exists()).toBe(true)
  })

  it('waits for the link before it can be shared', async () => {
    const wrapper = await mountSuspended(OnboardingInvite, { props: props({ url: '', isLoading: true }) })

    expect(wrapper.get('[data-testid="onboarding-invite-share"]').attributes('disabled')).toBeDefined()
    expect(wrapper.find('[data-testid="onboarding-invite-link"]').exists()).toBe(false)
  })

  it('lists one or two names plainly, and one more as a person', async () => {
    const two = await mountSuspended(OnboardingInvite, { props: props({ friendNames: ['Lena', 'Jonas'] }) })
    expect(two.get('[data-testid="onboarding-intro"]').text()).toContain('mit Lena und Jonas befreundet')

    const three = await mountSuspended(OnboardingInvite, { props: props({ friendNames: ['Lena', 'Jonas', 'Sarah'] }) })
    expect(three.get('[data-testid="onboarding-intro"]').text()).toContain('Lena, Jonas und einer weiteren Person')
  })
})

describe('OnboardingNotifications', () => {
  it('shows what a notification looks like before the browser asks', async () => {
    const wrapper = await mountSuspended(OnboardingNotifications, {
      props: { step: 6, total: 7, state: 'off', isBusy: false },
    })

    expect(wrapper.get('[data-testid="onboarding-notification-sample"]').text()).toContain(de.onboarding.notifications.sampleBody)
    expect(wrapper.get('[data-testid="onboarding-skip"]').text()).toBe(de.onboarding.later)

    await wrapper.get('[data-testid="onboarding-notifications-allow"]').trigger('click')
    expect(wrapper.emitted('allow')).toHaveLength(1)

    await wrapper.get('[data-testid="onboarding-skip"]').trigger('click')
    expect(wrapper.emitted('next')).toHaveLength(1)
  })

  it.each([
    ['on', 'onboarding-notifications-on', de.onboarding.notifications.on],
    ['blocked', 'onboarding-notifications-blocked', de.onboarding.notifications.blocked],
  ] as const)('says where it stands once answered (%s), and moves on', async (state, testId, text) => {
    const wrapper = await mountSuspended(OnboardingNotifications, {
      props: { step: 6, total: 7, state, isBusy: false },
    })

    expect(wrapper.get(`[data-testid="${testId}"]`).text()).toBe(text)
    expect(wrapper.find('[data-testid="onboarding-notifications-allow"]').exists()).toBe(false)

    await wrapper.get('[data-testid="onboarding-notifications-next"]').trigger('click')
    expect(wrapper.emitted('next')).toHaveLength(1)
  })
})

describe('OnboardingDone', () => {
  it('lists what is set up as states, and finishes on the one button', async () => {
    const wrapper = await mountSuspended(OnboardingDone, {
      props: {
        step: 6,
        total: 6,
        summary: { hasPhoto: false, handle: '@nora', firstDo: 'waiting', friendCount: 0, notifications: null },
        isFinishing: false,
        failed: false,
      },
    })

    expect(wrapper.get('[data-testid="onboarding-summary-photo"]').text()).toContain(de.onboarding.done.photoMissing)
    expect(wrapper.get('[data-testid="onboarding-summary-firstDo"]').text()).toContain(de.onboarding.done.firstDoWaiting)
    expect(wrapper.get('[data-testid="onboarding-summary-friends"]').text()).toContain(de.onboarding.done.friendsNone)

    const handle = wrapper.get('[data-testid="onboarding-summary-handle"]')
    expect(handle.text()).toContain('@nora')
    expect(handle.find('[data-q2-private]').exists()).toBe(true)

    // A device that was never asked has nothing to report.
    expect(wrapper.find('[data-testid="onboarding-summary-notifications"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="onboarding-summary"]').html()).not.toContain('q2-accent')

    await wrapper.get('[data-testid="onboarding-finish"]').trigger('click')
    expect(wrapper.emitted('finish')).toHaveLength(1)
  })

  it('reports a running Do, friends and notifications, and a failed finish', async () => {
    const wrapper = await mountSuspended(OnboardingDone, {
      props: {
        step: 7,
        total: 7,
        summary: { hasPhoto: true, handle: '@nora', firstDo: 'running', friendCount: 1, notifications: false },
        isFinishing: false,
        failed: true,
      },
    })

    expect(wrapper.get('[data-testid="onboarding-summary-photo"]').text()).toContain(de.onboarding.done.photoSet)
    expect(wrapper.get('[data-testid="onboarding-summary-firstDo"]').text()).toContain(de.onboarding.done.firstDoRunning)
    expect(wrapper.get('[data-testid="onboarding-summary-friends"]').text()).toContain(de.onboarding.done.friendsCount(1))
    expect(wrapper.get('[data-testid="onboarding-summary-notifications"]').text()).toContain(de.onboarding.done.notificationsOff)
    expect(wrapper.get('[data-testid="onboarding-done-error"]').attributes('role')).toBe('alert')
  })

  it('says "nothing yet" for a skipped first Do', async () => {
    const wrapper = await mountSuspended(OnboardingDone, {
      props: {
        step: 6,
        total: 6,
        summary: { hasPhoto: false, handle: '@nora', firstDo: 'none', friendCount: 3, notifications: true },
        isFinishing: true,
        failed: false,
      },
    })

    expect(wrapper.get('[data-testid="onboarding-summary-firstDo"]').text()).toContain(de.onboarding.done.firstDoNone)
    expect(wrapper.get('[data-testid="onboarding-summary-notifications"]').text()).toContain(de.onboarding.done.notificationsOn)
  })
})

describe('GoalDraftCard', () => {
  it('says what is waiting and for whom, keeping the title private', async () => {
    const wrapper = await mountSuspended(GoalDraftCard, { props: { draft: draft() } })

    const title = wrapper.get('[data-testid="goal-draft-title"]')
    expect(title.text()).toBe('Jeden Abend lesen')
    expect(title.attributes()).toHaveProperty('data-q2-private')
    expect(wrapper.text()).toContain('3× pro Woche')
    expect(wrapper.get('[data-testid="goal-draft-waiting"]').text()).toContain(de.draft.waiting)

    await wrapper.get('[data-testid="goal-draft-share"]').trigger('click')
    expect(wrapper.emitted('share')).toHaveLength(1)
  })

  it('asks before throwing it away', async () => {
    const wrapper = await mountSuspended(GoalDraftCard, { props: { draft: draft() }, attachTo: document.body })

    await wrapper.get('[data-testid="goal-draft-discard"]').trigger('click')
    await new Promise(resolve => setTimeout(resolve, 0))
    expect(wrapper.emitted('discard')).toBeUndefined()

    document.querySelector<HTMLButtonElement>('[data-testid="confirm-accept"]')!.click()
    await new Promise(resolve => setTimeout(resolve, 0))

    expect(wrapper.emitted('discard')).toHaveLength(1)
    wrapper.unmount()
  })

  it('can be a plain statement where the link is already next to it', async () => {
    const wrapper = await mountSuspended(GoalDraftCard, { props: { draft: draft(), actions: false } })

    expect(wrapper.find('[data-testid="goal-draft-share"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="goal-draft-discard"]').exists()).toBe(false)
  })
})

describe('AppTour', () => {
  function tabBar() {
    for (const key of ['home', 'search', 'dos', 'chats', 'profile']) {
      const tab = document.createElement('a')
      tab.dataset.testid = `nav-${key}`
      document.body.append(tab)
    }
  }

  // Mounted on an open route: the guard would otherwise send a test without a
  // session to the sign-in screen, taking the parameter with it. Even there it
  // asks the server who is signed in, so the answer — nobody — is given here;
  // without it the tests pass only while an API happens to be running on 5080.
  beforeEach(() => {
    useSession().adopt(null)
  })

  it('walks over the five tabs once the introduction hands over, and ends by taking the parameter away', async () => {
    tabBar()
    const wrapper = await mountSuspended(AppTour, { route: '/diagnostics?tour=1', attachTo: document.body })
    await new Promise(resolve => setTimeout(resolve, 0))

    const tour = () => document.querySelector<HTMLElement>('[data-testid="app-tour"]')
    expect(tour()?.getAttribute('role')).toBe('dialog')
    expect(tour()?.dataset.stop).toBe('home')
    expect(document.querySelector('[data-testid="app-tour-title"]')?.textContent?.trim()).toBe(de.tour.home.title)

    for (const stop of ['search', 'dos', 'chats', 'profile']) {
      document.querySelector<HTMLButtonElement>('[data-testid="app-tour-next"]')!.click()
      await new Promise(resolve => setTimeout(resolve, 0))
      expect(tour()?.dataset.stop).toBe(stop)
    }

    expect(document.querySelector('[data-testid="app-tour-next"]')?.textContent?.trim()).toBe(de.tour.done)

    document.querySelector<HTMLButtonElement>('[data-testid="app-tour-next"]')!.click()
    await new Promise(resolve => setTimeout(resolve, 20))

    expect(useRoute().query.tour).toBeUndefined()
    expect(tour()).toBeNull()
    wrapper.unmount()
  })

  it('can be ended at any stop', async () => {
    tabBar()
    const wrapper = await mountSuspended(AppTour, { route: '/diagnostics?tour=1', attachTo: document.body })
    await new Promise(resolve => setTimeout(resolve, 0))

    document.querySelector<HTMLButtonElement>('[data-testid="app-tour-skip"]')!.click()
    await new Promise(resolve => setTimeout(resolve, 20))

    expect(document.querySelector('[data-testid="app-tour"]')).toBeNull()
    wrapper.unmount()
  })

  it('is not there without the parameter', async () => {
    tabBar()
    const wrapper = await mountSuspended(AppTour, { route: '/diagnostics', attachTo: document.body })
    await new Promise(resolve => setTimeout(resolve, 0))

    expect(document.querySelector('[data-testid="app-tour"]')).toBeNull()
    wrapper.unmount()
  })
})
