/**
 * Builds the iOS app and hands it to Xcode.
 *
 *   bun run app:ios             # native web build + `cap sync ios`
 *   bun run app:ios --open      # the same, then open the project in Xcode
 *   bun run app:ios:testflight  # the same, then archive and upload to App
 *                               # Store Connect, where it becomes a TestFlight build
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
 * What comes out is app/ios, ready to run from Xcode.
 *
 * With --testflight it is archived and uploaded as well (docs/deployment.md
 * section 11). The version is the latest release tag, like every other
 * artefact; the build number is the commit count, which only ever grows —
 * Q2_IOS_BUILD_NUMBER overrides it when the same commit has to go up twice.
 * Signing is automatic, with the team named in the Xcode project and the
 * Apple ID Xcode is signed in with; that is also who uploads.
 */
import { readdirSync, readFileSync, rmSync } from 'node:fs'
import { join } from 'node:path'
import { appDir, repoRoot } from './lib/paths.ts'
import { failure, info, runSequence, success } from './lib/proc.ts'

const staging = 'https://q2.aarongreiner.dev'

const apiBaseUrl = process.env.Q2_IOS_API_BASE_URL || staging
const siteUrl = process.env.Q2_IOS_SITE_URL || staging
const appEnv = process.env.Q2_IOS_APP_ENV || 'staging'
const sentryDsn = process.env.Q2_IOS_SENTRY_DSN || ''

const testflight = process.argv.includes('--testflight')
const iosProjectDir = join(appDir, 'ios', 'App')

function git(...args: string[]): string {
  const result = Bun.spawnSync(['git', ...args], { cwd: repoRoot })
  return result.exitCode === 0 ? result.stdout.toString().trim() : ''
}

/*
 * Everything an upload needs is checked before the minute-long web build, so
 * a missing team or tag fails at once rather than at the end.
 */
let version = ''
let buildNumber = ''

if (testflight) {
  const project = readFileSync(join(iosProjectDir, 'App.xcodeproj', 'project.pbxproj'), 'utf8')

  if (!/DEVELOPMENT_TEAM = \w+;/.test(project)) {
    failure('The Xcode project names no team. Open it (bun run app:ios --open), choose the team')
    failure('under App → Signing & Capabilities, and run this again.')
    process.exit(1)
  }

  const tag = git('describe', '--tags', '--abbrev=0', '--match', 'v*')
  version = tag.replace(/^v/, '')
  buildNumber = process.env.Q2_IOS_BUILD_NUMBER || git('rev-list', '--count', 'HEAD')

  // App Store Connect takes up to three integers, no pre-release suffix.
  if (!/^\d+\.\d+\.\d+$/.test(version)) {
    failure(`The latest tag is '${tag || 'none'}', which is not a version App Store Connect accepts (1.2.3).`)
    process.exit(1)
  }

  if (!/^\d+$/.test(buildNumber)) {
    failure(`'${buildNumber}' is not a build number.`)
    process.exit(1)
  }

  info(`  Version   ${version} (${buildNumber})`)

  if (git('rev-list', '-n', '1', tag) !== git('rev-parse', 'HEAD'))
    info(`  note: HEAD is past ${tag} — the build carries ${version} but not only its code`)

  if (git('status', '--porcelain') !== '')
    info('  note: the working tree has uncommitted changes, and they are in this build')
}

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

if (!testflight) {
  success('app/ios is up to date — run it from Xcode (bun run app:ios --open)')
  process.exit(0)
}

// Under App/build, which is ignored; replaced by every upload.
const archivePath = join(iosProjectDir, 'build', 'App.xcarchive')
const exportPath = join(iosProjectDir, 'build', 'export')
rmSync(archivePath, { recursive: true, force: true })
rmSync(exportPath, { recursive: true, force: true })

const uploaded = await runSequence([
  {
    label: 'archive',
    cmd: 'xcodebuild',
    args: [
      '-project', join(iosProjectDir, 'App.xcodeproj'),
      '-scheme', 'App',
      '-configuration', 'Release',
      '-destination', 'generic/platform=iOS',
      '-archivePath', archivePath,
      '-allowProvisioningUpdates',
      `MARKETING_VERSION=${version}`,
      `CURRENT_PROJECT_VERSION=${buildNumber}`,
      'archive',
    ],
  },
  {
    label: 'upload to App Store Connect',
    cmd: 'xcodebuild',
    args: [
      '-exportArchive',
      '-archivePath', archivePath,
      '-exportPath', exportPath,
      '-exportOptionsPlist', join(iosProjectDir, 'ExportOptions.plist'),
      '-allowProvisioningUpdates',
    ],
  },
])

if (uploaded !== 0) {
  failure('The build was not uploaded.')
  process.exit(uploaded)
}

success(`${version} (${buildNumber}) is uploaded — it shows up in TestFlight once App Store Connect has processed it`)
