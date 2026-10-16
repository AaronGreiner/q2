import type { Locator, Page } from '@playwright/test'

/**
 * A goal on the Dos screen, whichever row draws it (#77).
 *
 * Your own goal that is due today is a window row carrying the camera or the
 * log button; everything else is a `GoalCard`. Both lead to the goal through
 * one link that carries its title, so this finds it either way — and a spec
 * does not have to know whether a goal it just created counts as due.
 */
export function goalLink(page: Page, title: string): Locator {
  return page.getByTestId('goal-list').getByRole('link').filter({ hasText: title })
}
