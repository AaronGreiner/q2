import { expect, test } from '@playwright/test'
import { goalLink } from './support/goalList'
import { gotoHydrated } from './support/hydration'

/**
 * Browser transactions are named after the screen, not the address (#66).
 *
 * Named after the address, every goal, person and chat is a transaction of its
 * own, performance data never groups by screen, and every name carries an id.
 * The router-aware tracing integration names them after the route template
 * instead — but only if it is the one installed, which is what this guards:
 * a bare `browserTracingIntegration()` in sentry.client.config.ts takes its
 * place silently. Asserted on the wire, because the configuration looks right
 * either way.
 *
 * The router-aware integration starts the page load later than a listed one
 * did, which is why Replay now starts after it (sentry.client.config.ts): the
 * page load has to carry its replay's id, or the two are not linked.
 */

/** From the E2E seed — see api/.../Seeding/E2ESeed.cs. */
const seeded = {
  sharedGoal: 'E2E shared goal with participants',
  sharedGoalId: 'e2e00000-0000-4000-8000-000000000001',
}

interface Transaction {
  op?: string
  name?: string
  source?: string
  replayId?: string
}

interface TransactionItem {
  transaction?: string
  transaction_info?: { source?: string }
  contexts?: { trace?: { op?: string } }
}

function parse(line: string | undefined): unknown {
  try {
    return line ? JSON.parse(line) : undefined
  }
  catch {
    // A replay item is compressed, and not a line of JSON.
    return undefined
  }
}

/** Every transaction in the envelopes, as op, name and the source of the name. */
function transactionsIn(envelopes: string[]): Transaction[] {
  const found: Transaction[] = []

  for (const envelope of envelopes) {
    const lines = envelope.split('\n')
    const header = parse(lines[0]) as { trace?: { replay_id?: string } } | undefined

    // An envelope is its header, then an item header and its payload per item.
    for (let index = 1; index < lines.length - 1; index++) {
      if ((parse(lines[index]) as { type?: string } | undefined)?.type !== 'transaction') continue

      const item = parse(lines[index + 1]) as TransactionItem | undefined
      found.push({
        op: item?.contexts?.trace?.op,
        name: item?.transaction,
        source: item?.transaction_info?.source,
        replayId: header?.trace?.replay_id,
      })
    }
  }

  return found
}

test('a navigation and a page load are named after the route', async ({ page }) => {
  const envelopes: string[] = []

  // Intercepted in the browser, so nothing leaves the machine even though a
  // DSN is configured.
  await page.route('**/sentry.q2.invalid/**', async (route) => {
    envelopes.push(route.request().postData() ?? '')
    await route.fulfill({ status: 200, body: '{}' })
  })

  const named = (op: string) => transactionsIn(envelopes)
    .filter(transaction => transaction.op === op)
    .map(transaction => `${transaction.name} (${transaction.source})`)

  // A navigation, rendered on the client: from the list into a Do.
  await gotoHydrated(page, '/goals')
  await goalLink(page, seeded.sharedGoal).click()
  await expect(page).toHaveURL(`/goals/${seeded.sharedGoalId}`)

  // A navigation is sent when it has gone idle, so it is waited for before the
  // reload below would throw it away.
  await expect.poll(() => named('navigation'), { timeout: 15_000 }).toContain('/goals/:id() (route)')

  // A page load, rendered by the server: the same Do again.
  await page.reload()
  await expect.poll(() => named('pageload'), { timeout: 15_000 }).toContain('/goals/:id() (route)')

  // No transaction anywhere in the run is named after an address with an id.
  for (const transaction of transactionsIn(envelopes)) {
    expect(transaction.name).not.toContain(seeded.sharedGoalId)
  }

  // And the page load is linked to its replay.
  const pageLoad = transactionsIn(envelopes).find(transaction => transaction.op === 'pageload')
  expect(pageLoad?.replayId).toMatch(/^[0-9a-f]{32}$/)
})
