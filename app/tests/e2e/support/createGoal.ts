import type { Page } from '@playwright/test'

/**
 * Creating a goal through the sheet, the way a person does: a title, the
 * rhythm, who checks it, and the summary.
 *
 * `rhythm` runs on the second step for a spec that wants something other than
 * "every day"; the default is left as the sheet sets it.
 */
export async function createGoal(
  page: Page,
  { title, friend, rhythm }: { title: string, friend: string, rhythm?: (page: Page) => Promise<void> },
) {
  await page.goto('/goals?create=1')
  await page.getByTestId('goal-title-input').fill(title)
  await page.getByTestId('goal-next').click()

  if (rhythm) await rhythm(page)
  await page.getByTestId('goal-next').click()

  await page.getByTestId('goal-friend-picker').getByText(friend).click()
  await page.getByTestId('goal-next').click()

  await page.getByTestId('goal-submit').click()
}
