import { describe, expect, it } from 'vitest'
import { liftResetTokenScript, parseResetFragment } from '~/utils/resetLink'

/**
 * The token in a reset link, and the inline script that takes it out of the
 * address bar before anything else on the page can see it.
 */

describe('parseResetFragment', () => {
  it.each([
    ['#token=abc.def-_', 'abc.def-_'],
    ['#from=mail&token=abc.def', 'abc.def'],
    ['#token=abc.def&from=mail', 'abc.def'],
  ])('finds the token in %s', (hash, token) => {
    expect(parseResetFragment(hash)).toBe(token)
  })

  it.each(['', '#', '#token=', '#mytoken=abc', '?token=abc'])('finds nothing in "%s"', (hash) => {
    expect(parseResetFragment(hash)).toBeNull()
  })
})

describe('the inline script', () => {
  /** Runs the script against a stand-in for the page it is written into. */
  function runAt(address: string) {
    const url = new URL(address)
    const page: Record<string, unknown> = {}
    const replaced: string[] = []

    const location = { hash: url.hash, pathname: url.pathname, search: url.search }
    const history = {
      state: null,
      replaceState: (_state: unknown, _title: string, next: string) => replaced.push(next),
    }

    new Function('window', 'location', 'history', liftResetTokenScript)(page, location, history)

    return { parked: page.__q2ResetToken, replaced }
  }

  it('parks the token and takes it out of the address bar', () => {
    const { parked, replaced } = runAt('https://q2.example.com/reset-password#token=abc.def')

    expect(parked).toBe('abc.def')
    expect(replaced).toEqual(['/reset-password'])
  })

  it('reads the fragment the same way parseResetFragment does', () => {
    const address = 'https://q2.example.com/reset-password#from=mail&token=abc.def'

    expect(runAt(address).parked).toBe(parseResetFragment(new URL(address).hash))
  })

  it('leaves an address without a token alone', () => {
    const { parked, replaced } = runAt('https://q2.example.com/reset-password')

    expect(parked).toBeUndefined()
    expect(replaced).toEqual([])
  })
})
