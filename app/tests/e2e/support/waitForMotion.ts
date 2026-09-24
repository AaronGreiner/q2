import type { Page } from '@playwright/test'

/** Measure the settled screen, not the temporary opacity of an entrance.
 * Infinite loading indicators are excluded; finite motion must actually end.
 */
export async function waitForMotion(page: Page) {
  await page.waitForFunction(() => document.getAnimations().every(animation =>
    animation.effect?.getTiming().iterations === Infinity
    || (!animation.pending && animation.playState !== 'running'),
  ))
}
