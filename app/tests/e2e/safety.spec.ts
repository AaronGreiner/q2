import { expect, test } from '@playwright/test'

/**
 * Getting away from somebody, end to end.
 *
 * Narrow on purpose. What only a browser can prove is the round trip: block
 * from a profile, watch the person disappear from the screens that would
 * otherwise show them, and put it back. Who a block reaches across the API is
 * asserted where it belongs, in `BlockEndpointTests`.
 *
 * The spec unblocks whatever it blocked, because the suite shares one database
 * (docs/testing.md) — and because reversibility is the feature's own rule, that
 * clean-up is an assertion rather than housekeeping.
 */
const friend = 'E2E Jonas'

async function openFriendProfile(page: import('@playwright/test').Page) {
  await page.goto('/search')
  await page.getByTestId('friend-search').fill(friend)
  await page.getByTestId('person-result').filter({ hasText: friend }).first().getByTestId('person-link').click()
  await expect(page.getByTestId('person-actions')).toBeVisible()
}

test.describe('blocking somebody', () => {
  test('takes them off the screens that would show them, and can be undone', async ({ page }) => {
    await openFriendProfile(page)

    await page.getByTestId('person-actions').click()
    await page.getByTestId('report-block').click()
    await page.getByTestId('confirm-accept').click()

    // Straight out of the profile: a blocked person has no profile any more,
    // so staying there would leave somebody looking at a 404.
    await expect(page).toHaveURL(/\/search$/)

    await page.getByTestId('friend-search').fill(friend)
    await expect(page.getByTestId('person-result').filter({ hasText: friend })).toHaveCount(0)

    await page.goto('/settings/blocked')
    await expect(page.getByTestId('blocked-row').filter({ hasText: friend })).toBeVisible()

    await page.getByTestId('unblock').first().click()
    await page.getByTestId('confirm-accept').click()

    await expect(page.getByTestId('blocked-empty')).toBeVisible()

    // Findable again — but not a friend again, which is the honest
    // consequence and is asserted on the API side.
    await page.goto('/search')
    await page.getByTestId('friend-search').fill(friend)
    await expect(page.getByTestId('person-result').filter({ hasText: friend }).first()).toBeVisible()
  })

  test('the blocked list is reachable from the settings and starts empty', async ({ page }) => {
    await page.goto('/settings')
    await page.getByTestId('open-blocked').click()

    await expect(page).toHaveURL(/\/settings\/blocked$/)
    await expect(page.getByTestId('blocked-empty')).toBeVisible()
  })
})

test.describe('reporting something', () => {
  test('asks what is wrong, says it is anonymous, and takes it', async ({ page }) => {
    await openFriendProfile(page)

    await page.getByTestId('person-actions').click()

    // Said where the buttons are: people do not report a friend if they think
    // the friend will find out.
    await expect(page.getByText('Die gemeldete Person erfährt nicht, wer gemeldet hat.')).toBeVisible()

    await page.getByTestId('report-reason-Spam').click()
    await page.getByTestId('report-note').fill('E2E: nur ein Test.')
    await page.getByTestId('report-submit').click()

    // `.first()`, because a toast is announced twice: once visibly and once in
    // the aria-live region that reads it out.
    await expect(page.getByText('Danke — wir sehen es uns an').first()).toBeVisible()
  })
})
