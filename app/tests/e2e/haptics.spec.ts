import { expect, test } from '@playwright/test'

for (const feedback of ['supported', 'missing', 'blocked', 'reduced'] as const) {
  test(`kudos still work with haptics ${feedback}`, async ({ page }) => {
    await page.emulateMedia({ reducedMotion: feedback === 'reduced' ? 'reduce' : 'no-preference' })
    await page.addInitScript((mode) => {
      const calls: number[] = []
      Reflect.set(window, 'hapticCalls', calls)
      Object.defineProperty(navigator, 'vibrate', {
        configurable: true,
        value: mode === 'missing'
          ? undefined
          : (duration: number) => {
              calls.push(duration)
              if (mode === 'blocked') throw new Error('Vibration unavailable')
              return true
            },
      })
    }, feedback)

    await page.goto('/activity')
    const button = page.getByTestId('kudos-button').first()
    await expect(button).toBeVisible()
    expect(await page.evaluate(() => Reflect.get(window, 'hapticCalls'))).toEqual([])
    const wasPressed = await button.getAttribute('aria-pressed')
    await button.click()
    await expect(button).toHaveAttribute('aria-pressed', wasPressed === 'true' ? 'false' : 'true')
    // Restore the shared seed, while proving that taking kudos back works too.
    await button.click()
    await expect(button).toHaveAttribute('aria-pressed', wasPressed!)
    expect(await page.evaluate(() => Reflect.get(window, 'hapticCalls')))
      .toEqual(feedback === 'supported' || feedback === 'blocked' ? [15, 15] : [])
  })
}
