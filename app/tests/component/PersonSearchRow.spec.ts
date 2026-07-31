import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import PersonSearchRow from '~/components/friends/PersonSearchRow.vue'
import type { FriendshipState, Person, PersonSearchResult } from '~/api/types'

/**
 * The whole job of this component is turning one `state` from the server into
 * the one action that makes sense for it, so that is what the tests are: every
 * state, and the button it produces.
 */
function person(overrides: Partial<Person> = {}): Person {
  return {
    id: '019faece-5a81-7c67-8fa2-00a63d9e9200',
    displayName: 'Jonas Weber',
    handle: '@jonas.w',
    initials: 'JW',
    avatarColor: '#4f46e5',
    isOnline: true,
    ...overrides,
  }
}

function result(state: FriendshipState, mutualFriends = 0): PersonSearchResult {
  return { person: person(), state, mutualFriends }
}

describe('PersonSearchRow', () => {
  it('shows the name and the handle, which is what somebody searched for', async () => {
    const wrapper = await mountSuspended(PersonSearchRow, { props: { result: result('None') } })

    expect(wrapper.text()).toContain('Jonas Weber')
    expect(wrapper.text()).toContain('@jonas.w')
  })

  it('mentions mutual friends only when there are any', async () => {
    const withMutual = await mountSuspended(PersonSearchRow, {
      props: { result: result('None', 3) },
    })
    const without = await mountSuspended(PersonSearchRow, { props: { result: result('None', 0) } })

    expect(withMutual.text()).toContain('3 gemeinsame Freunde')
    expect(without.text()).not.toContain('gemeinsame')
  })

  it('offers to add somebody you are not connected to', async () => {
    const wrapper = await mountSuspended(PersonSearchRow, { props: { result: result('None') } })

    const button = wrapper.find('[data-testid="result-request"]')
    expect(button.exists()).toBe(true)

    await button.trigger('click')
    expect(wrapper.emitted('request')?.[0]).toEqual([person().id])
  })

  it('offers to take back a request you have already sent', async () => {
    const wrapper = await mountSuspended(PersonSearchRow, {
      props: { result: result('RequestSent') },
    })

    expect(wrapper.text()).toContain('Angefragt')

    await wrapper.find('[data-testid="result-withdraw"]').trigger('click')
    expect(wrapper.emitted('withdraw')?.[0]).toEqual([person().id])
  })

  it('offers to accept a request they sent you', async () => {
    const wrapper = await mountSuspended(PersonSearchRow, {
      props: { result: result('RequestReceived') },
    })

    await wrapper.find('[data-testid="result-accept"]').trigger('click')
    expect(wrapper.emitted('accept')?.[0]).toEqual([person().id])
  })

  it('offers to write to somebody you are already friends with', async () => {
    const wrapper = await mountSuspended(PersonSearchRow, {
      props: { result: result('Friends') },
    })

    // Not "add" — the row has to say that you already know them.
    expect(wrapper.find('[data-testid="result-request"]').exists()).toBe(false)

    await wrapper.find('[data-testid="result-message"]').trigger('click')
    expect(wrapper.emitted('message')?.[0]).toEqual([person().id])
  })

  it('offers nothing at all for yourself', async () => {
    const wrapper = await mountSuspended(PersonSearchRow, { props: { result: result('Self') } })

    expect(wrapper.text()).toContain('Du')
    expect(wrapper.find('button').exists()).toBe(false)
  })

  it('labels every action with the person it acts on', async () => {
    // An unlabelled icon button is indistinguishable to a screen reader, and
    // this is a decision about a person.
    const wrapper = await mountSuspended(PersonSearchRow, {
      props: { result: result('Friends') },
    })

    expect(wrapper.find('[data-testid="result-message"]').attributes('aria-label'))
      .toContain('Jonas Weber')
  })
})
