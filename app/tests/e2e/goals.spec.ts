import { expect, test } from '@playwright/test'

/**
 * The central user flow: see the seeded goals, create one, see it appear.
 *
 * Runs against a freshly created and seeded temporary SQLite database, so the
 * starting state is identical on every run.
 */

/** From the E2E seed — see api/.../Seeding/E2ESeed.cs. */
const seeded = {
  sharedGoal: 'E2E shared goal with participants',
  completedGoal: 'E2E completed goal',
  zeroProgressGoal: 'E2E goal without any progress',
  overdueGoal: 'E2E overdue goal',
  sharedGoalId: 'e2e00000-0000-4000-8000-000000000001',
}

test.describe('goals', () => {
  test('shows the seeded goals with their states', async ({ page }) => {
    await page.goto('/')

    await expect(page.getByRole('heading', { name: 'Shared goals' })).toBeVisible()

    const list = page.getByTestId('goal-list')
    await expect(list).toBeVisible()

    await expect(page.getByText(seeded.sharedGoal)).toBeVisible()
    await expect(page.getByText(seeded.completedGoal)).toBeVisible()
    await expect(page.getByText(seeded.zeroProgressGoal)).toBeVisible()

    // The card for the overdue goal must say so, and not only through colour.
    const overdueCard = page.getByTestId('goal-card').filter({ hasText: seeded.overdueGoal })
    await expect(overdueCard.getByText('Overdue', { exact: true })).toBeVisible()

    await expect(page.getByTestId('goal-card')).toHaveCount(4)
  })

  test('marks the environment so a screenshot is never ambiguous', async ({ page }) => {
    await page.goto('/')

    await expect(page.getByTestId('environment-badge')).toHaveText('e2e')
  })

  test('opens a goal detail page', async ({ page }) => {
    await page.goto('/')

    await page.getByRole('link', { name: seeded.sharedGoal }).click()

    await expect(page).toHaveURL(`/goals/${seeded.sharedGoalId}`)
    await expect(page.getByRole('heading', { name: seeded.sharedGoal })).toBeVisible()
    await expect(page.getByText('E2E Participant One')).toBeVisible()
  })

  test('filtering to a status with no matches shows the empty state', async ({ page }) => {
    await page.goto('/')

    await page.getByTestId('filter-archived').click()

    // The seed deliberately contains no archived goal.
    await expect(page.getByTestId('goal-list-empty')).toBeVisible()
    await expect(page.getByTestId('goal-list-empty')).toContainText('No archived goals')
    await expect(page.getByTestId('goal-card')).toHaveCount(0)
  })

  test('the filter lives in the URL, so it survives a reload and the back button', async ({ page }) => {
    await page.goto('/')

    await page.getByTestId('filter-completed').click()
    await expect(page).toHaveURL(/\?status=Completed$/)
    await expect(page.getByTestId('goal-card')).toHaveCount(1)

    // Shareable and bookmarkable: the same URL reproduces the same view.
    await page.reload()
    await expect(page.getByTestId('goal-card')).toHaveCount(1)
    await expect(page.getByTestId('filter-completed')).toHaveAttribute('aria-checked', 'true')

    await page.goBack()
    await expect(page.getByTestId('goal-card')).toHaveCount(4)
  })

  test('an unknown status in the URL falls back to showing everything', async ({ page }) => {
    await page.goto('/?status=NotAStatus')

    await expect(page.getByTestId('goal-card')).toHaveCount(4)
    await expect(page.getByTestId('filter-all')).toHaveAttribute('aria-checked', 'true')
  })

  test('creates a goal and shows it in the list', async ({ page }) => {
    const title = `E2E created goal ${Date.now()}`

    await page.goto('/')
    await expect(page.getByTestId('goal-list')).toBeVisible()

    await page.getByTestId('goal-title-input').fill(title)
    await page.getByTestId('goal-description-input').fill('Created by the E2E suite.')
    await page.getByTestId('goal-participants-input').fill('E2E Author, E2E Reviewer')
    await page.getByTestId('goal-submit').click()

    // Confirmation, then the goal itself.
    await expect(page.getByText('Goal created', { exact: true })).toBeVisible()

    const created = page.getByTestId('goal-card').filter({ hasText: title })
    await expect(created).toBeVisible()
    await expect(created.getByText('E2E Author and E2E Reviewer')).toBeVisible()

    // And it is really persisted, not just added to the client-side list.
    await page.reload()
    await expect(page.getByTestId('goal-card').filter({ hasText: title })).toBeVisible()
  })

  test('rejects an empty title without contacting the server', async ({ page }) => {
    await page.goto('/')

    await expect(page.getByTestId('goal-submit')).toBeDisabled()

    await page.getByTestId('goal-title-input').fill('   ')
    await expect(page.getByTestId('goal-submit')).toBeDisabled()
  })

  test('shows a friendly message when a goal does not exist', async ({ page }) => {
    await page.goto('/goals/11111111-1111-4111-8111-111111111111')

    const state = page.getByTestId('goal-not-found')
    await expect(state).toBeVisible()
    await expect(state).toContainText('We could not find that goal')

    // Nothing technical leaks into the page.
    const body = await page.textContent('body')
    expect(body).not.toContain('ResourceNotFoundException')
    expect(body).not.toContain('at Q2.Api')
  })
})
