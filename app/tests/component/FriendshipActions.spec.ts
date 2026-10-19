import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import FriendshipActions from '~/components/friends/FriendshipActions.vue'
import type { FriendshipState } from '~/api/types'

/**
 * What a profile offers to do about the person on it (#86): one `state` from
 * the server, and the one answer it calls for. Every state, and what it emits.
 */
async function mount(state: FriendshipState, busy = false) {
  return mountSuspended(FriendshipActions, { props: { state, name: 'Max Brenner', busy } })
}

describe('FriendshipActions', () => {
  it('offers to add a stranger', async () => {
    const wrapper = await mount('None')

    const add = wrapper.get('[data-testid="person-request"]')
    expect(add.text()).toBe('Hinzufügen')
    expect(add.attributes('aria-label')).toBe('Hinzufügen: Max Brenner')

    await add.trigger('click')
    expect(wrapper.emitted('request')).toHaveLength(1)
  })

  it('says a sent request is waiting, in grey, and lets it be taken back', async () => {
    const wrapper = await mount('RequestSent')

    expect(wrapper.get('[data-testid="person-requested"]').text()).toBe('Angefragt')
    expect(wrapper.find('[data-testid="person-request"]').exists()).toBe(false)

    await wrapper.get('[data-testid="person-withdraw"]').trigger('click')
    expect(wrapper.emitted('withdraw')).toHaveLength(1)
  })

  it('answers somebody who asked, either way, from where they can be looked at', async () => {
    const wrapper = await mount('RequestReceived')

    expect(wrapper.text()).toContain('Möchte dich hinzufügen')

    await wrapper.get('[data-testid="person-accept"]').trigger('click')
    await wrapper.get('[data-testid="person-decline"]').trigger('click')

    expect(wrapper.emitted('accept')).toHaveLength(1)
    expect(wrapper.emitted('decline')).toHaveLength(1)
  })

  it('writes to a friend, and keeps ending the friendship out of reach of that button', async () => {
    const wrapper = await mount('Friends')

    const message = wrapper.get('[data-testid="person-message"]')
    expect(message.text()).toBe('Nachricht schreiben')

    await message.trigger('click')
    expect(wrapper.emitted('message')).toHaveLength(1)
    expect(wrapper.text()).not.toContain('Entfernen')
  })

  it('offers nothing on your own profile', async () => {
    const wrapper = await mount('Self')

    expect(wrapper.find('[data-testid="person-friendship"]').exists()).toBe(false)
  })

  it('takes no second answer while the first is on its way', async () => {
    const wrapper = await mount('RequestReceived', true)

    expect(wrapper.get('[data-testid="person-accept"]').attributes()).toHaveProperty('disabled')
    expect(wrapper.get('[data-testid="person-decline"]').attributes()).toHaveProperty('disabled')
  })
})
