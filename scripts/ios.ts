/**
 * Builds the iOS app and hands it to Xcode.
 *
 *   bun run app:ios          # native web build + `cap sync ios`
 *   bun run app:ios --open   # the same, then open the project in Xcode
 *
 * The web build is the ordinary Nuxt app with Q2_NATIVE=1, which turns off
 * server rendering and the service worker (app/nuxt.config.ts). It points at
 * Staging unless told otherwise:
 *
 *   Q2_IOS_API_BASE_URL  where the API is            (default: Staging)
 *   Q2_IOS_SITE_URL      where q2 is opened on the web, for shared links
 *                                                    (default: Staging)
 *   Q2_IOS_APP_ENV       what the settings screen and Sentry call it
 *                                                    (default: staging)
 *   Q2_IOS_SENTRY_DSN    the public DSN of q2-app    (default: none, no reporting)
 *
 * Every public value is set here explicitly rather than read from app/.env:
 * that file holds a developer's local settings — the demo sign-in, the
 * diagnostics page, localhost — and none of it may end up inside an app that
 * signs in against Staging.
 *
 * What comes out is app/ios, ready to run from Xcode. Signing, TestFlight and
 * the App Store are not this script's business (issue #51).
 */
import { readdirSync, rmSync } from 'node:fs'
import { join } from 'node:path'
import { appDir } from './lib/paths.ts'
import { failure, info, runSequence, success } from './lib/proc.ts'

const staging = 'https://q2.aarongreiner.dev'

const apiBaseUrl = process.env.Q2_IOS_API_BASE_URL || staging
const siteUrl = process.env.Q2_IOS_SITE_URL || staging
const appEnv = process.env.Q2_IOS_APP_ENV || 'staging'
const sentryDsn = process.env.Q2_IOS_SENTRY_DSN || ''

const env = {
  Q2_NATIVE: '1',
  NUXT_PUBLIC_API_BASE_URL: apiBaseUrl,
  NUXT_PUBLIC_SITE_URL: siteUrl,
  NUXT_PUBLIC_APP_ENV: appEnv,
  NUXT_PUBLIC_DIAGNOSTICS_ENABLED: 'false',
  NUXT_PUBLIC_DEMO_EMAIL: '',
  NUXT_PUBLIC_DEMO_PASSWORD: '',
  NUXT_PUBLIC_SENTRY_DSN: sentryDsn,
  NUXT_PUBLIC_SENTRY_ENVIRONMENT: appEnv,
  NUXT_PUBLIC_SENTRY_ENABLED: '',
}

info(`  API       ${apiBaseUrl}`)
info(`  Links     ${siteUrl}`)
info(`  Env       ${appEnv}${sentryDsn ? ', reporting to Sentry' : ', no Sentry DSN'}`)

const built = await runSequence([
  { label: 'native web build', cmd: 'bun', args: ['x', 'nuxt', 'generate'], cwd: appDir, env },
])

if (built !== 0) process.exit(built)

/*
 * Source maps out before the bundle is copied into the app. They are emitted
 * for the Sentry upload (nuxt.config.ts), and that upload belongs to a release
 * that does not exist for the app yet (#51) — shipped inside the app they
 * would only hand the source to anybody who unzips it.
 */
const publicDir = join(appDir, '.output', 'public')
let removed = 0

for (const entry of readdirSync(publicDir, { recursive: true, encoding: 'utf8' })) {
  if (entry.endsWith('.map')) {
    rmSync(join(publicDir, entry))
    removed += 1
  }
}

info(`  removed ${removed} source map(s) from the bundle`)

const synced = await runSequence([
  { label: 'copy into the iOS project', cmd: 'bun', args: ['x', 'cap', 'sync', 'ios'], cwd: appDir },
  ...(process.argv.includes('--open')
    ? [{ label: 'open in Xcode', cmd: 'bun', args: ['x', 'cap', 'open', 'ios'], cwd: appDir }]
    : []),
])

if (synced !== 0) {
  failure('The iOS project could not be updated.')
  process.exit(synced)
}

success('app/ios is up to date — run it from Xcode (bun run app:ios --open)')
