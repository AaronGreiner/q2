import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import { useNuxtData } from '#imports'
import FriendRow from '~/components/friends/FriendRow.vue'
import SentRequestRow from '~/components/friends/SentRequestRow.vue'
import type { Friend, Person, SentRequest, Settings } from '~/api/types'

const now = Date.parse('2026-06-15T09:00:00Z')

function person(overrides: Partial<Person> = {}): Person {
  return {
    id: '019faece-5a81-7c67-8fa2-00a63d9e9200',
    displayName: 'Jonas Weber',
    handle: '@jonas.w',
    initials: 'JW',
    avatarColor: '#4f46e5',
    avatarImageId: null,
    isOnline: false,
    ...overrides,
  }
}

function friend(overrides: Partial<Friend> = {}): Friend {
  return {
    person: person(),
    streak: 0,
    lastSeenAt: null,
    ...overrides,
  }
}

describe('FriendRow', () => {
  it('shows a streak when there is one', async () => {
    const wrapper = await mountSuspended(FriendRow, {
      props: { friend: friend({ streak: 12 }), now },
    })

    expect(wrapper.text()).toContain('12-Tage-Streak')
  })

  it('falls back to presence when there is no streak', async () => {
    // Never the green dot alone: presence is not conveyed by colour only.
    const wrapper = await mountSuspended(FriendRow, {
      props: { friend: friend({ person: person({ isOnline: true }) }), now },
    })

    expect(wrapper.text()).toContain('Online')
  })

  it('shows nobody\'s presence to somebody who hides their own (#106)', async () => {
    // The reader's own choice, as the app has loaded it.
    const { data } = useNuxtData<Settings | null>('settings')
    data.value = { showActivity: false } as Settings

    try {
      const wrapper = await mountSuspended(FriendRow, {
        props: { friend: friend({ person: person({ isOnline: true }), lastSeenAt: '2026-06-15T08:51:00Z' }), now },
      })

      expect(wrapper.text()).not.toContain('Online')
      expect(wrapper.text()).not.toContain('aktiv')
      expect(wrapper.text()).toContain('@jonas.w')
      expect(wrapper.find('[style*="--q2-online"]').exists()).toBe(false)
    }
    finally {
      data.value = null
    }
  })

  it('offers a message, labelled with the person, and no way to end the friendship', async () => {
    const wrapper = await mountSuspended(FriendRow, { props: { friend: friend(), now } })

    const message = wrapper.find('[data-testid="friend-message"]')
    const identity = wrapper.find('[data-q2-private]')

    expect(identity.text()).toContain('Jonas Weber')
    expect(message.attributes('aria-label')).toContain('Jonas Weber')

    // Ending it lives on their profile, not one slip away from "Nachricht".
    expect(wrapper.find('[data-testid="friend-remove"]').exists()).toBe(false)

    await message.trigger('click')
    expect(wrapper.emitted('message')?.[0]).toEqual([person().id])
  })
})

describe('SentRequestRow', () => {
  function sent(): SentRequest {
    return { person: person(), requestedAt: '2026-06-14T09:00:00+00:00' }
  }

  it('shows who is being waited on and offers to take it back', async () => {
    const wrapper = await mountSuspended(SentRequestRow, { props: { request: sent() } })

    expect(wrapper.text()).toContain('Jonas Weber')
    expect(wrapper.text()).toContain('Zurückziehen')

    await wrapper.find('[data-testid="sent-request-withdraw"]').trigger('click')
    expect(wrapper.emitted('withdraw')?.[0]).toEqual([person().id])
  })
})
