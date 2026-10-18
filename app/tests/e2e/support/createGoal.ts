import type { Page } from '@playwright/test'
import { gotoHydrated } from './hydration'

/**
 * Creating a goal through the sheet, the way a person does: a title, the
 * rhythm, who checks it, and the summary.
 *
 * `rhythm` runs on the second step for a spec that wants something other than
 * "every day"; the default is left as the sheet sets it. `description` is what
 * counts, written under the title (#95); left out, nothing is said.
 */
export async function createGoal(
  page: Page,
  { title, friend, rhythm, description }: {
    title: string
    friend: string
    rhythm?: (page: Page) => Promise<void>
    description?: string
  },
) {
  await gotoHydrated(page, '/goals?create=1')
  await page.getByTestId('goal-title-input').fill(title)
  if (description) await page.getByTestId('goal-description-input').fill(description)
  await page.getByTestId('goal-next').click()

  if (rhythm) await rhythm(page)
  await page.getByTestId('goal-next').click()

  await page.getByTestId('goal-friend-picker').getByText(friend).click()
  await page.getByTestId('goal-next').click()

  await page.getByTestId('goal-submit').click()
}
