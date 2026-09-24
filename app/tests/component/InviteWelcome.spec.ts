import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import InviteWelcome from '~/components/friends/InviteWelcome.vue'
import type { ApiFailure } from '~/api/errors'
import type { FriendshipState, InvitePreview } from '~/api/types'
import { de } from '~/i18n/messages'

/**
 * The page an invite link lands on.
 *
 * What is worth pinning down is that each visitor is offered the one step that
 * fits them — an account, or "accept", or nothing because there is nothing
 * left to do — and that a link which stopped working reads as that rather than
 * as an error.
 */
function preview(relation: FriendshipState | null = null): InvitePreview {
  return { displayName: 'Mara Keller', initials: 'MK', avatarColor: '#4f46e5', relation }
}

function failure(kind: ApiFailure['kind']): ApiFailure {
  return { kind, isExpected: true, status: null, fieldErrors: {}, traceId: null, errorId: null, reason: null }
}

function props(overrides: Record<string, unknown> = {}) {
  return {
    preview: preview(),
    failure: null,
    isLoading: false,
    isSignedIn: false,
    isAccepting: false,
    acceptFailure: null,
    registerTo: '/register?invite=abc123',
    signInTo: '/login?next=%2Fjoin%2Fabc123',
    ...overrides,
  }
}

describe('InviteWelcome', () => {
  it('names the sender, and keeps the name out of Session Replay', async () => {
    const wrapper = await mountSuspended(InviteWelcome, { props: props() })

    const name = wrapper.get('[data-testid="invite-welcome-name"]')
    expect(name.text()).toBe('Mara Keller')
    expect(name.attributes()).toHaveProperty('data-q2-private')

    // The heading belongs to the frame and carries no name.
    expect(wrapper.get('h1').text()).toBe(de.join.heading)
  })

  it('offers an account, or signing in and coming back, to somebody without a session', async () => {
    const wrapper = await mountSuspended(InviteWelcome, { props: props() })

    expect(wrapper.get('[data-testid="invite-welcome-register"]').attributes('href')).toBe('/register?invite=abc123')
    expect(wrapper.get('[data-testid="invite-welcome-sign-in"]').attributes('href')).toBe('/login?next=%2Fjoin%2Fabc123')
    expect(wrapper.find('[data-testid="invite-welcome-accept"]').exists()).toBe(false)
  })

  it.each<FriendshipState>(['None', 'RequestSent', 'RequestReceived'])(
    'offers to accept to somebody signed in who is not a friend yet (%s)',
    async (relation) => {
      const wrapper = await mountSuspended(InviteWelcome, { props: props({ isSignedIn: true, preview: preview(relation) }) })

      expect(wrapper.get('[data-testid="invite-welcome-accept"]').text()).toBe(de.join.accept)
      expect(wrapper.find('[data-testid="invite-welcome-register"]').exists()).toBe(false)
    },
  )

  it('emits accept when the button is pressed', async () => {
    const wrapper = await mountSuspended(InviteWelcome, { props: props({ isSignedIn: true, preview: preview('None') }) })

    await wrapper.get('[data-testid="invite-welcome-accept"]').trigger('click')

    expect(wrapper.emitted('accept')).toHaveLength(1)
  })

  it.each<[FriendshipState, string]>([
    ['Friends', de.join.alreadyFriends],
    ['Self', de.join.ownLink],
  ])('has nothing to accept when the relation is %s', async (relation, sentence) => {
    const wrapper = await mountSuspended(InviteWelcome, { props: props({ isSignedIn: true, preview: preview(relation) }) })

    expect(wrapper.get('[data-testid="invite-welcome-standing"]').text()).toBe(sentence)
    expect(wrapper.find('[data-testid="invite-welcome-accept"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="invite-welcome-home"]').attributes('href')).toBe('/')
  })

  it('says why accepting did not work', async () => {
    const wrapper = await mountSuspended(InviteWelcome, {
      props: props({ isSignedIn: true, preview: preview('None'), acceptFailure: failure('network') }),
    })

    const alert = wrapper.get('[data-testid="invite-welcome-error"]')
    expect(alert.attributes('role')).toBe('alert')
    expect(alert.text()).toBe(de.errors.network)
  })

  /**
   * A replaced link is the normal end of a link's life, not a fault. The page
   * says so and still offers the way in.
   */
  it('reads a link that stopped working as that, and still offers an account', async () => {
    const wrapper = await mountSuspended(InviteWelcome, { props: props({ preview: null, failure: failure('notFound') }) })

    expect(wrapper.get('h1').text()).toBe(de.join.invalidHeading)
    expect(wrapper.find('[data-testid="error-state"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="invite-welcome-invalid"]').text()).toContain(de.join.createAccount)
  })

  it('shows any other failure as an error with a retry', async () => {
    const wrapper = await mountSuspended(InviteWelcome, { props: props({ preview: null, failure: failure('network') }) })

    await wrapper.get('[data-testid="error-retry"]').trigger('click')

    expect(wrapper.emitted('retry')).toHaveLength(1)
  })

  it('announces that it is loading', async () => {
    const wrapper = await mountSuspended(InviteWelcome, { props: props({ preview: null, isLoading: true }) })

    expect(wrapper.get('[data-testid="invite-welcome-loading"]').attributes('aria-busy')).toBe('true')
  })
})
