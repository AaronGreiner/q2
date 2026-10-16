import type { Page } from '@playwright/test'

/**
 * Every visible control on the page that is smaller than 44 × 44 CSS pixels,
 * named so a failure says which one.
 *
 * Shared by quality.spec.ts and onboarding.spec.ts, so the rule is measured one
 * way wherever it is checked.
 */
export async function undersizedTargets(page: Page, label: string): Promise<string[]> {
  return await page.locator(
    'a, button, input, select, textarea, [role="button"], [role="checkbox"], [role="radio"], [role="switch"]',
  ).evaluateAll((elements, currentLabel) => elements.flatMap((element) => {
    const box = element.getBoundingClientRect()
    const style = getComputedStyle(element)
    if (style.visibility === 'hidden' || style.display === 'none' || box.width === 0 || box.height === 0) return []
    if (element.classList.contains('sr-only')) return []

    // Scrolled sideways out of the screen, like a carousel's other slides:
    // nothing there can be tapped until it is scrolled in, and then it is measured.
    if (box.right <= 0 || box.left >= window.innerWidth) return []

    // Several compact controls intentionally grow their real pointer area
    // with an absolutely positioned ::after box. Its clicks target the
    // parent, so measuring only getBoundingClientRect() reports a false 26px.
    const after = getComputedStyle(element, '::after')
    const cssPixels = (value: string) => {
      const parsed = Number.parseFloat(value)
      return Number.isFinite(parsed) ? parsed : 0
    }
    const hasAfterTarget = after.content !== 'none' && after.content !== 'normal'
    const afterWidth = Number.parseFloat(after.width)
    const afterHeight = Number.parseFloat(after.height)
    const targetWidth = Math.max(
      box.width,
      hasAfterTarget && Number.isFinite(afterWidth) ? afterWidth : 0,
      hasAfterTarget ? box.width - cssPixels(after.left) - cssPixels(after.right) : 0,
    )
    const targetHeight = Math.max(
      box.height,
      hasAfterTarget && Number.isFinite(afterHeight) ? afterHeight : 0,
      hasAfterTarget ? box.height - cssPixels(after.top) - cssPixels(after.bottom) : 0,
    )
    if (targetWidth >= 43.5 && targetHeight >= 43.5) return []

    const identity = element.getAttribute('data-testid')
      ?? element.getAttribute('aria-label')
      ?? element.textContent?.trim().slice(0, 40)
      ?? element.tagName.toLowerCase()
    return [`${currentLabel}: ${identity} (${Math.round(box.width)}x${Math.round(box.height)})`]
  }), label)
}
