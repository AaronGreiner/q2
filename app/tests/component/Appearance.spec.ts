import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import ChatContactStrip from '~/components/chats/ChatContactStrip.vue'
import AccentPicker from '~/components/settings/AccentPicker.vue'
import AppContentPanel from '~/components/layout/AppContentPanel.vue'

it('keeps the toolbar and content in one shared surface', async () => {
  const panel = await mountSuspended(AppContentPanel, {
    slots: { toolbar: () => 'Search', default: () => 'Conversations' },
  })
  expect(panel.get('[data-slot="header"]').text()).toBe('Search')
  expect(panel.get('[data-slot="body"]').text()).toBe('Conversations')
})

describe('accent picker', () => {
  it('names all choices and emits a choice without fetching account settings', async () => {
    const picker = await mountSuspended(AccentPicker, { props: { modelValue: 'iris' } })
    const choices = picker.findAll('[role="radio"]')
    expect(choices).toHaveLength(4)
    expect(choices[0]!.attributes('aria-checked')).toBe('true')
    expect(picker.text()).toContain('Salbei')
    await choices[1]!.trigger('click')
    expect(picker.emitted('update:modelValue')?.[0]).toEqual(['sage'])
    await picker.setProps({ modelValue: 'sage' })
    expect(choices[1]!.attributes('aria-checked')).toBe('true')
  })
})

it('opens a friend shortcut and blocks its identity from Replay', async () => {
  const strip = await mountSuspended(ChatContactStrip, {
    props: {
      people: [{ id: 'person-1', displayName: 'Mara', handle: '@mara', initials: 'MK', avatarColor: '#ffffff', avatarImageId: null, isOnline: true }],
    },
  })
  const shortcut = strip.get('[data-testid="chat-contact"]')
  expect(shortcut.attributes()).toHaveProperty('data-q2-block')
  await shortcut.trigger('click')
  expect(strip.emitted('open')).toEqual([['person-1']])
  await strip.setProps({ busy: true })
  expect(shortcut.attributes()).toHaveProperty('disabled')
})
