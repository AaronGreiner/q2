/**
 * Reading the token out of a reset link without letting anything else see it.
 *
 * The link is `/reset-password#token=…`. A fragment never leaves the browser —
 * not to Caddy, not to the server rendering the page — which is why the token
 * is there rather than in the query (docs/adr/0026-mail-and-password-reset.md).
 * What is left is the browser itself: Session Replay records the address of the
 * page it starts on, and it starts before any component does.
 *
 * So the reset page puts `liftResetTokenScript` into its <head>. An inline
 * script runs while the HTML is still being parsed — before the bundle, before
 * Sentry, before the router — takes the token out of the address bar and parks
 * it on `window` for the page to collect once, with `takeResetToken`.
 */

/** Where the inline script parks the token until the page takes it. */
const parkedTokenKey = '__q2ResetToken'

/** `#token=…`, alone or among other fragment parameters. */
const tokenPattern = /(?:^#|&)token=([^&]+)/

/**
 * The script the reset page puts into its <head>.
 *
 * Plain ES5 with nothing imported, because it runs before the bundle does. It
 * reads the fragment with the same pattern as `parseResetFragment`, which is
 * the one with the tests.
 */
export const liftResetTokenScript
  = `(function(){var m=${tokenPattern.toString()}.exec(location.hash);if(!m)return;`
    + `window.${parkedTokenKey}=m[1];`
    + `history.replaceState(history.state,'',location.pathname+location.search)})()`

/** The token in a fragment such as `#token=abc`, or null. */
export function parseResetFragment(hash: string): string | null {
  return tokenPattern.exec(hash)?.[1] ?? null
}

/**
 * Takes the token, once.
 *
 * From where the inline script parked it — or, when the page was reached
 * without a full load and the script never ran, from the address bar, which is
 * then cleared the same way.
 */
export function takeResetToken(): string | null {
  const parked = Reflect.get(window, parkedTokenKey)
  Reflect.deleteProperty(window, parkedTokenKey)

  if (typeof parked === 'string' && parked) return parked

  const fromAddressBar = parseResetFragment(window.location.hash)

  if (fromAddressBar) {
    window.history.replaceState(window.history.state, '', window.location.pathname + window.location.search)
  }

  return fromAddressBar
}
