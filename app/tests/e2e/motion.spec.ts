import { expect, test } from '@playwright/test'
import { gotoHydrated } from './support/hydration'

for (const reducedMotion of ['no-preference', 'reduce'] as const) {
  test(`navigation, tab selection and sheets work with motion ${reducedMotion}`, async ({ page }, testInfo) => {
    await page.emulateMedia({ reducedMotion })

    // A goal's own screen always has its window's ring; the start screen only
    // has one when something due wants more than one photograph.
    await page.goto('/goals/e2e00000-0000-4000-8000-000000000001')
    const ring = page.getByTestId('progress-ring').first()
    await expect(ring).toBeVisible()
    const duration = await ring.evaluate(element => Number.parseFloat(getComputedStyle(element).transitionDuration))
    expect(duration > 0.01).toBe(reducedMotion === 'no-preference')

    await page.goto('/')
    await expect(page.getByTestId('streak-flame')).toBeVisible()

    // Navigate within a layout and then across layouts: both must release the
    // outgoing view, keep the shell inside the phone and support browser back.
    await page.getByTestId('nav-chats').click()
    await expect(page.getByTestId('nav-chats')).toHaveAttribute('aria-current', 'page')
    const marker = page.locator('.q2-nav-indicator')
    await expect.poll(async () => {
      const bar = await marker.boundingBox()
      const tab = await page.getByTestId('nav-chats').boundingBox()
      return Math.abs((bar?.x ?? 0) - (tab?.x ?? 999))
    }).toBeLessThan(1)

    await page.getByTestId('chat-row').filter({ hasText: 'E2E group chat' }).click()
    await expect(page.getByTestId('chat-input')).toBeVisible()
    await expect(page.getByTestId('bottom-nav')).toHaveCount(0)
    await page.goBack()
    await expect(page.getByTestId('bottom-nav')).toBeVisible()

    await page.goto('/goals')
    const goalsTab = page.getByRole('radio', { name: 'Don’ts', exact: true })
    await goalsTab.focus()
    await page.keyboard.press('Space')
    await expect(goalsTab).toBeChecked()
    await expect(page.getByTestId('goal-list')).toBeVisible()

    // A query-only selection must not remount the page (and steal focus).
    await expect(goalsTab).toBeFocused()
    const fieldset = page.locator('.q2-segmented [data-slot="fieldset"]')
    await expect.poll(() => fieldset.evaluate((element) => {
      const style = getComputedStyle(element, '::before')
      const selected = element.querySelector('[data-slot="item"]:has([data-state="checked"])')!
      const markerX = element.getBoundingClientRect().x + 4 + new DOMMatrixReadOnly(style.transform).m41
      return Math.abs(markerX - selected.getBoundingClientRect().x)
    })).toBeLessThan(1)

    await page.getByTestId('open-create-goal').click()
    await expect(page.getByRole('dialog')).toBeVisible()
    await page.keyboard.press('Escape')
    await expect(page.getByRole('dialog')).toHaveCount(0)
    await expect(page.getByTestId('bottom-nav')).toBeVisible()
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)

    if (reducedMotion === 'reduce') {
      const activeAnimations = await page.evaluate(() => document.getAnimations().filter(animation => animation.playState === 'running').length)
      expect(activeAnimations).toBe(0)
      expect(await marker.count()).toBe(1)
    }
    await page.screenshot({ path: testInfo.outputPath(`goals-${reducedMotion}.png`) })
  })
}

test('a new chat message arrives without remounting existing messages or losing the composer', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'no-preference' })
  await gotoHydrated(page, '/chats')
  await page.getByTestId('chat-row').filter({ hasText: 'E2E group chat' }).click()
  const firstMessage = page.getByTestId('chat-bubble').first()
  await firstMessage.evaluate(element => element.setAttribute('data-motion-retained', 'true'))
  await page.getByTestId('chat-input').fill('E2E motion message')
  await page.getByTestId('chat-send').click()
  await expect(page.getByTestId('chat-bubble').last()).toContainText('E2E motion message')
  await expect(page.getByTestId('chat-bubble').last()).toBeInViewport()
  await expect(firstMessage).toHaveAttribute('data-motion-retained', 'true')
  await expect(page.getByTestId('chat-input')).toHaveValue('')
})
