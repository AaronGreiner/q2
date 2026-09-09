import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import ReportSheet from '~/components/safety/ReportSheet.vue'

/**
 * The sheet for asking that something be looked at.
 *
 * What is worth pinning down is not the layout but the three things that would
 * quietly stop people using it: that it says reporting is anonymous, that the
 * note is optional, and that "Belästigung" is not the option under the thumb.
 *
 * Read off `document` rather than the wrapper, like the other drawer tests
 * here: a `UDrawer` portals its body out of the component's own tree.
 */
async function mount(props: Record<string, unknown> = {}) {
  return await mountSuspended(ReportSheet, {
    props: { open: true, targetKind: 'Person', targetId: 'person-1', ...props },
  })
}

function submitButton(): HTMLButtonElement | null {
  return document.querySelector('[data-testid="report-submit"] button')
    ?? document.querySelector<HTMLButtonElement>('[data-testid="report-submit"]')
}

describe('ReportSheet', () => {
  /**
   * People do not report a friend if they think the friend will find out, and
   * the ones who most need to report will assume the worst unless told.
   */
  it('says the report is anonymous, where the buttons are', async () => {
    const wrapper = await mount()

    expect(document.body.textContent).toContain('Die gemeldete Person erfährt nicht, wer gemeldet hat.')

    wrapper.unmount()
  })

  it('offers the picture reasons before the one about a person', async () => {
    const wrapper = await mount()
    const html = document.body.innerHTML

    // Reaching for "Belästigung" should be a deliberate choice rather than the
    // first thing under the thumb.
    expect(html.indexOf('report-reason-Faked')).toBeLessThan(html.indexOf('report-reason-Harassment'))
    expect(html.indexOf('report-reason-Inappropriate')).toBeLessThan(html.indexOf('report-reason-Harassment'))

    wrapper.unmount()
  })

  it('cannot be sent until a reason is chosen', async () => {
    const wrapper = await mount()

    expect(submitButton()?.disabled).toBe(true)

    document.querySelector<HTMLElement>('[data-testid="report-reason-Spam"]')?.click()
    await nextTick()

    expect(submitButton()?.disabled).toBe(false)

    wrapper.unmount()
  })

  /** Requiring a sentence means somebody in a hurry sends nothing at all. */
  it('sends without a note', async () => {
    const wrapper = await mount()

    document.querySelector<HTMLElement>('[data-testid="report-reason-Spam"]')?.click()
    await nextTick()
    submitButton()?.click()
    await nextTick()

    expect(wrapper.emitted('report')?.[0]).toEqual(['Spam', ''])

    wrapper.unmount()
  })

  /**
   * Beside the report, never instead of it: one asks somebody else to act, the
   * other acts now, and whoever needs one usually wants both.
   */
  it('offers blocking only when there is a person to block', async () => {
    const withPerson = await mount({ personId: 'person-1' })
    expect(document.querySelector('[data-testid="report-block"]')).not.toBeNull()
    withPerson.unmount()

    const withoutPerson = await mount({ targetKind: 'Proof', targetId: 'proof-1' })
    expect(document.querySelector('[data-testid="report-block"]')).toBeNull()
    withoutPerson.unmount()
  })
})
