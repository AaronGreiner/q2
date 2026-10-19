import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import AppContentPanel from '~/components/layout/AppContentPanel.vue'

/**
 * Pulling a screen down to read it again (#19), on the panel every screen
 * scrolls in. What is pinned here: letting go before the threshold does
 * nothing, past it calls the screen's `refresh` once and shows that it is
 * working, and nothing but a pull from the top counts.
 */
function panel(refresh?: () => Promise<unknown>) {
  return mountSuspended(defineComponent({
    setup: () => () => h(AppContentPanel, { refresh }, { default: () => h('p', 'Inhalt') }),
  }), { attachTo: document.body })
}

function touch(target: Element, type: string, x: number, y: number) {
  const event = new Event(type, { bubbles: true, cancelable: true })
  const points = type === 'touchend' || type === 'touchcancel' ? [] : [{ clientX: x, clientY: y }]
  Object.defineProperty(event, 'touches', { value: points })
  Object.defineProperty(event, 'changedTouches', { value: [{ clientX: x, clientY: y }] })
  target.dispatchEvent(event)
  return event
}

/** A finger put down at the top of the list, moved by (dx, dy) and lifted. */
async function pull(area: Element, dy: number, dx = 0) {
  touch(area, 'touchstart', 100, 100)
  touch(area, 'touchmove', 100 + dx / 2, 100 + dy / 2)
  touch(area, 'touchmove', 100 + dx, 100 + dy)
  touch(area, 'touchend', 100 + dx, 100 + dy)
  await nextTick()
}

const area = () => document.querySelector('[data-slot="body"]')!
const indicator = () => document.querySelector<HTMLElement>('[data-testid="pull-indicator"]')

describe('pull to refresh', () => {
  it('does nothing when let go before the threshold', async () => {
    const refresh = vi.fn().mockResolvedValue(undefined)
    const wrapper = await panel(refresh)

    // 100 px of finger is 50 px of indicator, under the 64 it takes.
    await pull(area(), 100)

    expect(refresh).not.toHaveBeenCalled()
    expect(indicator()?.dataset.refreshing).toBeUndefined()
    wrapper.unmount()
  })

  it('reads the screen again once past it, and shows it is working until it has', async () => {
    let finish: () => void = () => {}
    const refresh = vi.fn(() => new Promise<void>((resolve) => {
      finish = resolve
    }))
    const wrapper = await panel(refresh)

    touch(area(), 'touchstart', 100, 100)
    const move = touch(area(), 'touchmove', 100, 260)
    await nextTick()

    // Armed, and the list does not bounce away under the finger.
    expect(indicator()?.dataset.armed).toBe('')
    expect(move.defaultPrevented).toBe(true)

    touch(area(), 'touchend', 100, 260)
    await nextTick()

    expect(refresh).toHaveBeenCalledOnce()
    expect(indicator()?.dataset.refreshing).toBe('')
    expect(indicator()?.textContent).toContain('Wird aktualisiert')

    finish()
    await vi.waitFor(() => expect(indicator()?.dataset.refreshing).toBeUndefined())
    wrapper.unmount()
  })

  it('leaves a sideways swipe alone', async () => {
    const refresh = vi.fn().mockResolvedValue(undefined)
    const wrapper = await panel(refresh)

    await pull(area(), 160, 240)

    expect(refresh).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('is only a pull from the top of the list', async () => {
    const refresh = vi.fn().mockResolvedValue(undefined)
    const wrapper = await panel(refresh)

    Object.defineProperty(area(), 'scrollTop', { value: 120, configurable: true })
    await pull(area(), 300)

    expect(refresh).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('is not there on a screen that cannot be read again', async () => {
    const wrapper = await panel()

    expect(indicator()).toBeNull()
    await pull(area(), 300)
    expect(indicator()).toBeNull()
    wrapper.unmount()
  })
})
