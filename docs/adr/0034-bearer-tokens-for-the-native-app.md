# 0034 — The iOS app: a client-only build that signs in with bearer tokens

**Status:** Accepted
**Date:** 2026-09-25
**Amends:** [0011 — Accounts with ASP.NET Core Identity and a session cookie](0011-authentication-with-identity.md),
decision 2, which named this as the point to revisit; and
[0033 — Invite links that work](0033-invite-links-for-new-and-existing-accounts.md),
where the link's origin was left to this change.
**Issue:** [#7](https://github.com/AaronGreiner/q2/issues/7)

## Context

q2 is designed for a phone and ships as a Capacitor app; until now it only
installed from the browser ([0012](0012-installable-pwa.md)). Wrapping the same
Nuxt application in an iOS shell runs into three things the browser never
had to deal with:

- **The page is not served, it is bundled.** A WebView loads the app from
  `capacitor://localhost`, so there is no Nitro server to render the first
  frame, and none to copy a session cookie onto the API calls it makes.
- **The API is another site.** From `capacitor://localhost`, a request to
  `https://q2.aarongreiner.dev` is cross-site. The session cookie is
  `SameSite=Lax` and would not be sent. Loosening it to `None` would hand the
  cookie to WebKit's tracking prevention, which blocks third-party cookies in an
  app's WebView by default. That is the cost ADR 0011 wrote down in advance.
- **An `<img>` cannot send a header,** and every picture in q2 is behind the
  session (`/api/images/{id}`).

## Decision

**The browser keeps the cookie, and the iOS app signs in for bearer tokens.**
Both are ASP.NET Core Identity's own: the app gets the `BearerToken` handler's
sealed access and refresh tokens, not a JWT written here. Nothing about
authenticating is written by hand, as ADR 0006 and 0011 require.

1. **One scheme in front of two.** `AccountPolicy.SessionOrTokenScheme` is a
   policy scheme. A request that carries `Authorization: Bearer …` is
   authenticated as a token and everything else as the cookie. Never both: a
   cookie a WebView happens to hold cannot rescue an expired token. Every
   existing endpoint, `CurrentPerson` and `RequireAuthorization` work unchanged,
   because the principal looks the same either way.

2. **Two routes, the same checks.** `POST /api/auth/token` signs in with the
   password exactly as `/api/auth/login` does, lockout included, and Identity's
   handler writes the tokens as the answer. `POST /api/auth/token/refresh` trades
   a refresh token for a new pair, with the checks `MapIdentityApi` makes (which
   q2 still does not map): sealed here, not expired, and **the account's
   security stamp unchanged**. Signing up in the app is the ordinary
   registration followed by a token sign-in.

3. **An access token lives a minute; a refresh token a fortnight.** An access
   token is read without the database, so it cannot be checked against the
   security stamp, and its lifetime therefore *is* the recheck interval
   (`AccountPolicy.AccessTokenLifetime = SessionRecheckInterval`). After a
   password reset the app is out within a minute, the same as a browser. A
   refresh token lives as long as an unused cookie session (14 days) and is
   replaced on every refresh, so it slides the same way.

4. **The token travels in an address only to the live connection.** A browser's
   WebSocket cannot send a header, so SignalR puts the token in `access_token`.
   It is read there and only there (`/api/live`); anywhere else a token in the
   query is ignored. Caddy writes no access log for this site, and Sentry drops
   query strings on both runtimes.

5. **The app is built client-only.** `Q2_NATIVE=1` (set by `bun run app:ios`,
   `scripts/ios.ts`) turns off server rendering and the service worker in
   `nuxt.config.ts`, and `nuxt generate` produces the static bundle Capacitor
   copies into `app/ios`. Everything else is decided at runtime by the platform:
   `plugins/native.client.ts` provides a token keeper and a picture cache inside
   the app and `null` in the browser, and `useQ2Api`, `useSession`,
   `useLiveConnection` and `useImageSource` take the token path when those are
   there. One codebase, and the browser path is unchanged.

6. **The refresh token lives in the Keychain.** It is stored through
   `@aparajita/capacitor-secure-storage`, "when unlocked, this device only",
   never synchronised through iCloud. A refresh token is a key to the account,
   and it must not arrive on a new phone from a backup. The access token is only
   ever in memory. `@capacitor/preferences`, the official alternative, writes to
   UserDefaults: unencrypted, and included in unencrypted backups.

7. **Pictures are fetched with the token.** `useImageSource` gives a component
   the `src` for a stored picture. In the browser that is `imageUrl` as before.
   In the app it is the picture fetched with the token and shown as a `blob:`
   address from a bounded, per-app cache that is emptied on sign-out.
   Components keep their own `@error` handling.

8. **Links the app hands out use the public address.** The invite link is built
   from `siteUrl` (`NUXT_PUBLIC_SITE_URL`) when it is set, because the app's own
   page is `capacitor://localhost`, which nobody else can open. Opening such a
   link *in* the app (Universal Links) is [#50](https://github.com/AaronGreiner/q2/issues/50).

9. **The keyboard resizes the WebView.** In the app, WebKit's scroll-to-reveal
   lands after `useKeyboardViewport` has put the shell back, which left the app
   pushed up behind the status bar with the form cut off. `@capacitor/keyboard`
   with `resize: 'native'` shrinks the WebView the way a native screen shrinks,
   so there is nothing to scroll and `useKeyboardViewport` measures no covered
   area (see app/AGENTS.md section 9b).

## Consequences

**Good**

- The browser does not change. The cookie, server rendering, the service
  worker and every test that relies on them are untouched.
- One application, not two: the iOS app is the same pages and components,
  and the differences are two build switches and one runtime check.
- A changed password or a deleted account ends the app's session at the next
  refresh, which is tested (`TokenSignInTests`).

**Bad, and accepted**

- **Signing out of the app does not revoke anything on the server.** It
  forgets both tokens on the device. A refresh token copied off a phone stays
  usable until it expires, or until the password changes. Revoking one token
  would need a table of issued tokens, which is exactly the state these sealed
  tokens exist to avoid; a password reset remains the "sign out everywhere".
- **The Keychain outlives the app.** Deleting and reinstalling q2 on the same
  phone finds the old refresh token and is signed in again, if it has not
  expired. That is how iOS keeps Keychain items, and the token is still bound
  to this device.
- **An access token outlives a password reset by up to a minute,** and an open
  live connection keeps the principal it was opened with. Both match what the
  cookie already allowed (`SessionRecheckInterval`).
- **Signing up in the app is two requests.** If the second one — the token
  sign-in — fails, the account exists and the person signs in by hand.
- **The permission prompts are German only.** They come from `Info.plist`,
  outside the message catalogue; an English `InfoPlist.strings` is not there
  yet.
- **`app/ios/App/CapApp-SPM/Package.swift` points into bun's package store**,
  which `cap sync` rewrites on every run. Open the project through
  `bun run app:ios --open`, never directly after an install.
- **The app is built against Staging only.** Production does not exist yet;
  when it does, it needs `capacitor://localhost` in its CORS origins as well.

## When to revisit

- when Android is packaged ([#49](https://github.com/AaronGreiner/q2/issues/49)):
  its WebView origin is `https://localhost`, and Keystore storage goes through
  the same plugin;
- when native push arrives ([#12](https://github.com/AaronGreiner/q2/issues/12)),
  which registers a device against the signed-in account;
- when somebody needs to sign out *another* device, which is the point at
  which issued refresh tokens have to be stored.
