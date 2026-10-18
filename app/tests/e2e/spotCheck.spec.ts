import { expect, test } from '@playwright/test'
import { apiBaseUrl } from './support/e2eEnvironment'
import { goalLink } from './support/goalList'
import { gotoHydrated } from './support/hydration'

/**
 * A spot check, end to end at phone width (docs/adr/0038-spot-checks.md): the
 * open check is the first thing on the start screen with its countdown, the
 * camera it opens is live only, and the photograph arrives for its friends'
 * vote labelled "Live".
 *
 * The moment of a real check is random, so the diagnostics route — absent in
 * Staging and Production — asks one now. The synthetic camera needs launch
 * flags, which Playwright accepts only at the top of a file; such files run in
 * a worker of their own after the rest, by when safety.spec.ts has ended the
 * friendship with Jonas, so Lena checks this one.
 */
test.use({
  launchOptions: { args: ['--use-fake-device-for-media-stream', '--use-fake-ui-for-media-stream'] },
  permissions: ['camera'],
  serviceWorkers: 'block',
})

const friend = 'E2E Lena'

test('an open check comes first, opens a live-only camera and goes to its friends as live', async ({ page }, testInfo) => {
  const title = `E2E no smoking ${Date.now()}`

  // A Don't checked by spot checks, made the way a person makes one.
  await gotoHydrated(page, '/goals?create=1')
  await page.getByTestId('segment-Dont').click()
  await page.getByTestId('goal-title-input').fill(title)
  await page.getByTestId('goal-next').click()
  await page.getByTestId('dont-mode-SpotCheck').click()
  await page.getByTestId('goal-next').click()
  await page.getByTestId('goal-friend-picker').getByText(friend).click()
  await page.getByTestId('goal-next').click()
  await page.getByTestId('goal-submit').click()

  await goalLink(page, title).click()
  await page.waitForURL(/\/goals\/[0-9a-f-]{36}$/)
  const goalId = page.url().split('/goals/')[1]!

  // Nothing is shown before the check is asked.
  await expect(page.getByTestId('spot-check')).toHaveCount(0)

  const asked = await page.request.post(`${apiBaseUrl}/api/diagnostics/spot-check/${goalId}`)
  expect(asked.status(), `asking the check: ${await asked.text()}`).toBe(204)

  // First on the start screen, counting down.
  await page.goto('/')
  const card = page.getByTestId('spot-check')
  await expect(card).toBeVisible()
  await expect(card).toContainText(title)
  await expect(card.getByTestId('spot-check-left')).toHaveText(/^noch [0-3]:\d{2}$/)
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('start.png') })

  // A camera and nothing else.
  await card.getByTestId('spot-check-answer').click()
  const camera = page.getByTestId('photo-camera')
  await expect.poll(() => camera.evaluate(element => (element as HTMLVideoElement).videoWidth)).toBeGreaterThan(0)
  await expect(page.getByTestId('photo-choose')).toHaveCount(0)
  await expect(page.getByTestId('photo-file')).toHaveCount(0)
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('camera.png') })

  await page.getByTestId('photo-shutter').click()
  await page.getByTestId('photo-confirm').click()

  // Answered: the card goes, and the photograph waits for its friends, live.
  await expect(page.getByTestId('spot-check')).toHaveCount(0)

  await page.goto(`/goals/${goalId}`)
  await page.getByTestId('goal-open-chat').click()
  const proof = page.getByTestId('chat-proof').last()
  await expect(proof.getByTestId('proof-source')).toHaveText('Live')
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('chat.png') })
})
