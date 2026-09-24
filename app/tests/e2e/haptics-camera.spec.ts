import { expect, test } from '@playwright/test'

// Browser launch flags force a worker of their own, so Playwright accepts them
// only at the top of a file. The synthetic camera is therefore kept apart from
// the kudos cases in haptics.spec.ts, which need no camera at all.
test.use({
  launchOptions: { args: ['--use-fake-device-for-media-stream', '--use-fake-ui-for-media-stream'] },
  permissions: ['camera'],
})

test('the camera shutter requests a single pulse and still produces a preview', async ({ page }, testInfo) => {
  await page.emulateMedia({ reducedMotion: 'no-preference' })
  await page.addInitScript(() => {
    const calls: number[] = []
    Reflect.set(window, 'hapticCalls', calls)
    Object.defineProperty(navigator, 'vibrate', {
      configurable: true,
      value: (duration: number) => {
        calls.push(duration)
        return true
      },
    })
  })
  await page.goto('/')
  await page.getByTestId('window-deliver').first().click()
  const camera = page.getByTestId('photo-camera')
  await expect.poll(() => camera.evaluate(element => (element as HTMLVideoElement).videoWidth)).toBeGreaterThan(0)
  expect(await page.evaluate(() => Reflect.get(window, 'hapticCalls'))).toEqual([])
  await page.getByTestId('photo-shutter').click()
  await expect(page.getByTestId('photo-preview')).toBeVisible()
  expect(await page.evaluate(() => Reflect.get(window, 'hapticCalls'))).toEqual([15])
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('camera-preview.png') })
  // Closing without uploading leaves the goal unchanged.
  await page.keyboard.press('Escape')
  await expect(page.getByRole('dialog')).toHaveCount(0)
})
