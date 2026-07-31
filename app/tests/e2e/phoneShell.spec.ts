import { expect, test } from '@playwright/test'

/**
 * The three things that stop q2 behaving like a web page.
 *
 * Installed, there is no address bar to undo a stray pinch, no browser chrome
 * to explain a selection handle, and the safe-area insets stop being zero for
 * the first time. All three are one CSS declaration each, all three are
 * invisible in a normal desktop run, and all three are the kind of thing that
 * gets "cleaned up" by somebody who does not know why it is there.
 *
 * See docs/adr/0013-app-like-input.md.
 */

test.describe('pinch-zoom', () => {
  test('is off, in both of the two places that matter', async ({ page }) => {
    await page.goto('/')

    // Safari ignores this in a browser tab and honours it once installed,
    // which is the case it is here for.
    const viewport = await page.locator('meta[name="viewport"]').getAttribute('content')
    expect(viewport).toContain('user-scalable=no')
    expect(viewport).toContain('maximum-scale=1')

    // And the half that works everywhere: the gesture list without pinch-zoom
    // in it. Dropping double-tap with it is also what removes the tap delay.
    const touchAction = await page.evaluate(
      () => getComputedStyle(document.documentElement).touchAction,
    )
    expect(touchAction).toBe('pan-x pan-y')
  })
})

test.describe('text selection', () => {
  test('does nothing on content, so a long press behaves like an app', async ({ page }) => {
    await page.goto('/')

    const heading = page.getByRole('heading', { level: 1 })
    await heading.dblclick()

    // A double click is the pointer equivalent of the long press this is
    // really about, and it is the one a headless browser can perform.
    const selected = await page.evaluate(() => window.getSelection()?.toString() ?? '')
    expect(selected).toBe('')
  })

  test('still works in anything a person types into', async ({ page }) => {
    await page.goto('/friends')

    const search = page.getByTestId('friend-search')
    await search.fill('Robin')

    /*
     * Not a detail: `user-select: none` is inherited, so an input that was not
     * opted back in would keep its text on screen and refuse a caret — a goal
     * title could be typed but never corrected.
     */
    expect(await search.evaluate(field => getComputedStyle(field).userSelect)).toBe('text')

    const range = await search.evaluate((field: HTMLInputElement) => {
      field.setSelectionRange(0, 3)
      return `${field.selectionStart}-${field.selectionEnd}`
    })
    expect(range).toBe('0-3')
  })
})

test.describe('the bottom navigation', () => {
  /**
   * Pretends to be a phone with a home indicator.
   *
   * Chromium only, which is the only engine this suite runs — and the only way
   * to exercise `env(safe-area-inset-*)` at all, because a desktop browser
   * reports zero for every inset and every assertion below would pass on a
   * layout that is broken on an actual phone.
   */
  async function withHomeIndicator(page: import('@playwright/test').Page, bottom: number) {
    const cdp = await page.context().newCDPSession(page)
    await cdp.send('Emulation.setSafeAreaInsetsOverride', {
      insets: { top: 47, bottom, left: 0, right: 0 },
    })
  }

  test('sits on the bottom edge of the screen', async ({ page }) => {
    await page.goto('/')

    const box = (await page.getByTestId('bottom-nav').boundingBox())!
    const viewportHeight = page.viewportSize()!.height

    expect(Math.round(box.y + box.height)).toBe(viewportHeight)
  })

  test('keeps its room for the home indicator within a sane bound', async ({ page }) => {
    await page.goto('/')
    await withHomeIndicator(page, 34)

    const nav = page.getByTestId('bottom-nav')

    /*
     * The regression this guards: padding by the whole 34px inset leaves the
     * icons near the top edge of the bar with an empty band beneath them, so
     * the bar reads as hovering above the screen edge rather than sitting on
     * it. --q2-safe-bottom caps the inset at 1.25rem instead.
     */
    const paddingBottom = await nav.evaluate(bar => getComputedStyle(bar).paddingBottom)
    expect(paddingBottom).toBe('20px')

    // Capping the padding must not lift the bar itself off the edge: the
    // background still has to reach the bottom of the screen.
    const box = (await nav.boundingBox())!
    expect(Math.round(box.y + box.height)).toBe(page.viewportSize()!.height)
  })

  test('still clears the bottom edge on a device that reports no inset', async ({ page }) => {
    await page.goto('/')
    await withHomeIndicator(page, 0)

    // The floor, so the labels never sit flush against the edge of a screen
    // that simply does not report an inset.
    const paddingBottom = await page.getByTestId('bottom-nav').evaluate(
      bar => getComputedStyle(bar).paddingBottom,
    )
    expect(paddingBottom).toBe('12px')
  })
})
