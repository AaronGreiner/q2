import { expect, test } from '@playwright/test'

/**
 * The settings a notification obeys.
 *
 * Deliberately narrow. Who gets told, and what stops it, is decided on the
 * server and asserted there (`NotificationEndpointTests`); the encryption is
 * checked against RFC 8291's own worked example in the unit tests. What only a
 * browser can prove is that the two controls on the screen mean what they say
 * and survive a reload — quiet hours are stored on the account, so a value that
 * did not come back is a value that was never saved.
 *
 * The per-device switch is not exercised here. It needs a real push service and
 * a granted permission, and a headless browser can only demonstrate the state
 * this deployment is actually in: no VAPID keys, so nothing to switch on.
 */
/** Resolves when the settings screen has actually saved something. */
function saved(page: import('@playwright/test').Page) {
  return page.waitForResponse(response =>
    response.url().includes('/api/settings') && response.request().method() === 'PUT')
}

test.describe('notification settings', () => {
  test('quiet hours can be turned off and back on, and the window is remembered', async ({ page }) => {
    await page.goto('/settings')

    // On by default: a product that has to be told not to buzz at three in the
    // morning has already buzzed at three in the morning.
    const window = page.getByTestId('quiet-hours')
    await expect(window).toBeVisible()
    await expect(page.getByTestId('quiet-hours-from')).toHaveValue('22:00')
    await expect(page.getByTestId('quiet-hours-to')).toHaveValue('07:00')

    // The wait is armed before the action: the request is sent the moment the
    // control changes, so setting it up afterwards is a race the test loses
    // whenever the server is quick.
    await Promise.all([
      saved(page),
      page.getByTestId('quiet-hours-from').fill('21:30'),
    ])

    await page.reload()
    await expect(page.getByTestId('quiet-hours-from')).toHaveValue('21:30')

    // Off hides the window rather than leaving two controls that decide
    // nothing.
    await page.getByRole('switch', { name: 'Ruhezeiten' }).click()
    await expect(page.getByTestId('quiet-hours')).toBeHidden()

    await page.reload()
    await expect(page.getByTestId('quiet-hours')).toBeHidden()

    // Put back, because the suite shares one database.
    await page.getByRole('switch', { name: 'Ruhezeiten' }).click()
    await expect(page.getByTestId('quiet-hours')).toBeVisible()
    await Promise.all([
      saved(page),
      page.getByTestId('quiet-hours-from').fill('22:00'),
    ])
  })

  test('a deployment with no keys says so instead of offering a dead switch', async ({ page }) => {
    await page.goto('/settings')

    // The E2E host configures no VAPID pair, which is the default and a
    // supported state.
    await expect(page.getByTestId('push-unavailable')).toBeVisible()
    await expect(page.getByTestId('push-toggle')).toHaveCount(0)
  })
})
