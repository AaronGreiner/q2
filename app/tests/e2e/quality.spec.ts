import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'

const sharedGoalId = 'e2e00000-0000-4000-8000-000000000001'

/** The representative screens named in docs/next-steps.md. */
const accessibilityScreens = [
  { name: 'dashboard', path: '/' },
  { name: 'goal detail', path: `/goals/${sharedGoalId}` },
  { name: 'diagnostics', path: '/diagnostics' },
] as const

const signedInScreens = [
  '/',
  '/goals',
  '/goals?tab=goals',
  `/goals/${sharedGoalId}`,
  '/chats',
  '/search',
  '/profile',
  '/settings',
  '/diagnostics',
] as const

test.describe('server-rendered HTML', () => {
  test('contains personal data, progress and icons before JavaScript runs', async ({ page }) => {
    const response = await page.request.get('/')
    expect(response.ok()).toBe(true)

    const html = await response.text()
    expect(html).toContain('E2E shared goal with participants')
    expect(html).toContain('E2E goal three times a week')
    expect(html).toContain('3× pro Woche')

    // Nuxt Icon renders SVG on the server. Client-side hydration used to hide
    // that these were all missing from the first paint.
    expect((html.match(/<svg/g) ?? []).length).toBeGreaterThan(5)
  })
})

test.describe('WCAG A and AA', () => {
  for (const screen of accessibilityScreens) {
    for (const colorScheme of ['light', 'dark'] as const) {
      test(`${screen.name} has no ${colorScheme} accessibility violations`, async ({ page }) => {
        await page.emulateMedia({ colorScheme })
        await page.goto(screen.path)

        const results = await new AxeBuilder({ page })
          .withTags(['wcag2a', 'wcag2aa'])
          // The installed-app zoom decision is the one dated exception in
          // ADR 0013. Keeping only this rule out makes every other AA rule bite.
          .disableRules(['meta-viewport'])
          .analyze()

        const violations = results.violations.flatMap(violation => violation.nodes.map(node =>
          `${violation.id}: ${node.target.join(' ')}`,
        ))

        expect(violations).toEqual([])
      })
    }
  }
})

test.describe('phone geometry', () => {
  for (const path of signedInScreens) {
    test(`${path} never needs horizontal scrolling`, async ({ page }) => {
      await page.goto(path)

      const geometry = await page.evaluate(() => ({
        viewport: document.documentElement.clientWidth,
        content: document.documentElement.scrollWidth,
      }))

      expect(geometry.content).toBeLessThanOrEqual(geometry.viewport)
    })
  }

  test('visible controls meet the 44px touch-target rule', async ({ page }) => {
    const undersized: string[] = []

    for (const path of ['/', '/goals?tab=goals', '/goals/archive', '/diagnostics'] as const) {
      await page.goto(path)

      const onScreen = await page.locator(
        'a, button, input, select, textarea, [role="button"], [role="checkbox"], [role="radio"], [role="switch"]',
      ).evaluateAll((elements, currentPath) => elements.flatMap((element) => {
        const box = element.getBoundingClientRect()
        const style = getComputedStyle(element)
        if (style.visibility === 'hidden' || style.display === 'none' || box.width === 0 || box.height === 0) return []
        if (element.classList.contains('sr-only')) return []

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
        return [`${currentPath}: ${identity} (${Math.round(box.width)}x${Math.round(box.height)})`]
      }), path)

      undersized.push(...onScreen)
    }

    expect(undersized).toEqual([])
  })
})
