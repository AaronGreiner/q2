import { expect, test } from '@playwright/test'
import { apiBaseUrl } from './support/e2eEnvironment'

/**
 * q2 as an installable application.
 *
 * Everything here is invisible in the UI, which is exactly why it needs a
 * test: an icon path that stopped resolving, a manifest that lost `display`,
 * or a service worker that failed to register all look identical on screen —
 * the install offer simply stops appearing, and nobody notices for a release.
 *
 * The cache assertions are the important half. The service worker is allowed
 * to keep the build output and nothing else, because every rendered page in q2
 * is somebody's signed-in one. That rule cannot be checked by reading the
 * source, so it is checked here against what the browser actually stored.
 *
 * See docs/adr/0012-installable-pwa.md.
 */

interface WebManifest {
  name?: string
  short_name?: string
  start_url?: string
  scope?: string
  display?: string
  theme_color?: string
  background_color?: string
  icons?: { src: string, sizes: string, type: string, purpose?: string }[]
}

/**
 * Resolves once the worker is installed *and* controlling the page.
 *
 * Registration happens after hydration and precaching a hundred files takes a
 * moment, so every assertion below has to wait for it rather than assume it.
 */
async function waitForServiceWorker(page: import('@playwright/test').Page): Promise<void> {
  await expect.poll(
    () => page.evaluate(() => Boolean(navigator.serviceWorker?.controller)),
    { timeout: 20_000, message: 'The service worker never took control of the page.' },
  ).toBe(true)
}

/**
 * Every URL the service worker has put in a cache.
 *
 * Read as full URLs rather than paths because whether something belongs to the
 * API's origin is one of the things being asserted. Note that Workbox stores a
 * revisioned entry under a `?__WB_REVISION__=…` cache key, so these carry a
 * query string the network never sees — compare on `pathname`.
 */
async function cachedUrls(page: import('@playwright/test').Page): Promise<string[]> {
  return page.evaluate(async () => {
    const urls: string[] = []

    for (const name of await caches.keys()) {
      const cache = await caches.open(name)
      for (const request of await cache.keys()) urls.push(request.url)
    }

    return urls
  })
}

test.describe('the web app manifest', () => {
  test('is linked from every page and describes an installable app', async ({ page, request }) => {
    await page.goto('/')

    const href = await page.locator('link[rel="manifest"]').getAttribute('href')
    expect(href).toBe('/manifest.webmanifest')

    const response = await request.get(href!)
    expect(response.status()).toBe(200)

    // Chrome refuses to treat it as a manifest without this content type.
    expect(response.headers()['content-type']).toContain('application/manifest+json')

    const manifest = await response.json() as WebManifest

    // The fields an installability check actually looks at.
    expect(manifest.name).toBe('Qdos')
    expect(manifest.short_name).toBe('Qdos')
    expect(manifest.start_url).toBe('/')
    expect(manifest.scope).toBe('/')
    expect(manifest.display).toBe('standalone')

    // A 192 and a 512 are the minimum Chrome asks for, and the maskable one is
    // what keeps the icon from being letterboxed on an Android home screen.
    const sizes = manifest.icons?.map(icon => icon.sizes) ?? []
    expect(sizes).toContain('192x192')
    expect(sizes).toContain('512x512')
    expect(manifest.icons?.some(icon => icon.purpose === 'maskable')).toBe(true)
  })

  test('names icons that are all actually served', async ({ request }) => {
    const manifest = await (await request.get('/manifest.webmanifest')).json() as WebManifest

    // A 404 here costs nothing visible: the browser silently drops the icon and
    // the install offer disappears with it.
    for (const icon of manifest.icons ?? []) {
      const response = await request.get(icon.src)
      expect(response.status(), `${icon.src} is missing`).toBe(200)
      expect(response.headers()['content-type'], icon.src).toContain('image/png')
    }
  })
})

test.describe('the icons in the document', () => {
  test('point at files that exist', async ({ page, request }) => {
    await page.goto('/')

    const declared = await page.locator('link[rel="icon"], link[rel="apple-touch-icon"]').evaluateAll(
      links => links.map(link => link.getAttribute('href') ?? ''),
    )

    // The SVG for browsers that take one, the ICO for those that do not, and
    // the PNG iOS puts on the home screen.
    expect(declared).toContain('/favicon.svg')
    expect(declared).toContain('/favicon.ico')
    expect(declared).toContain('/apple-touch-icon.png')

    for (const href of declared) {
      expect((await request.get(href)).status(), `${href} is missing`).toBe(200)
    }
  })
})

test.describe('the service worker', () => {
  test('takes control of the page', async ({ page }) => {
    await page.goto('/')
    await waitForServiceWorker(page)
  })

  test('is served so that a new one is actually picked up', async ({ request }) => {
    const response = await request.get('/sw.js')
    expect(response.status()).toBe(200)

    // Nitro sends no cache header for a public asset on its own, which leaves
    // the browser to guess a lifetime from Last-Modified — and a worker the
    // browser considers fresh is a deployment that never arrives. This comes
    // from `registerWebManifestInRouteRules` in nuxt.config.ts.
    expect(response.headers()['cache-control']).toContain('must-revalidate')
  })

  test('caches the build output', async ({ page }) => {
    await page.goto('/')
    await waitForServiceWorker(page)

    await expect.poll(
      async () => (await cachedUrls(page)).some(url => /\/_nuxt\/.+\.js$/.test(new URL(url).pathname)),
      { timeout: 20_000, message: 'Nothing from the build was precached.' },
    ).toBe(true)

    const paths = (await cachedUrls(page)).map(url => new URL(url).pathname)

    // Everything here is content-hashed or revisioned and identical for every
    // person, which is what makes caching it safe.
    expect(paths).toContain('/manifest.webmanifest')
    expect(paths).toContain('/favicon.svg')
  })

  test('caches no rendered page and no API response', async ({ page }) => {
    // The rule this whole feature is built around: a cached document is one
    // person's goals left on a device that somebody else may pick up next.
    await page.goto('/')
    await waitForServiceWorker(page)

    // Visit the screens a person would, so anything that wanted to cache a
    // document or a response has had the chance.
    await page.goto('/goals')
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
    await page.goto('/profile')
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()

    const urls = await cachedUrls(page)

    const documents = urls.filter((url) => {
      const { pathname } = new URL(url)
      return pathname === '/' || pathname.endsWith('.html') || /^\/(goals|profile|chats|search|login)/.test(pathname)
    })
    expect(documents, 'a rendered page was cached').toEqual([])

    const fromApi = urls.filter(url => url.startsWith(apiBaseUrl))
    expect(fromApi, 'an API response was cached').toEqual([])
  })
})

test.describe('with no connection', () => {
  // Fixed rather than inherited from the machine running the suite: the offline
  // page picks its language from the browser, so leaving that to chance would
  // make the assertions below depend on the locale of whoever runs them.
  test.use({ locale: 'de-DE' })

  test('a navigation lands on the offline page instead of the browser error', async ({ page, context }) => {
    await page.goto('/')
    await waitForServiceWorker(page)

    await context.setOffline(true)

    try {
      await page.goto('/goals')

      await expect(page.getByRole('heading', { level: 1 })).toHaveText('Du bist offline')
      await expect(page.getByRole('button', { name: 'Erneut versuchen' })).toBeVisible()

      // The address bar still shows where the person was going, so the retry
      // button takes them there rather than home.
      expect(new URL(page.url()).pathname).toBe('/goals')
    }
    finally {
      await context.setOffline(false)
    }
  })

  test('shows the app icon, because it was precached', async ({ page, context }) => {
    await page.goto('/')
    await waitForServiceWorker(page)

    await context.setOffline(true)

    try {
      await page.goto('/goals')

      // Not decoration: it is the one proof on screen that the precache is
      // being served rather than merely populated.
      const loaded = await page.locator('img').evaluate(
        (image: HTMLImageElement) => image.complete && image.naturalWidth > 0,
      )
      expect(loaded).toBe(true)
    }
    finally {
      await context.setOffline(false)
    }
  })
})

test.describe('with no connection, in an English browser', () => {
  test.use({ locale: 'en-US' })

  test('the offline page follows the browser rather than defaulting to German', async ({ page, context }) => {
    /*
     * The person's real choice of language is stored on their account, and
     * this is the one moment the account cannot be reached. The browser's
     * language is the only signal left — German remains the fallback, which
     * the German case above covers.
     */
    await page.goto('/')
    await waitForServiceWorker(page)

    await context.setOffline(true)

    try {
      await page.goto('/goals')
      await expect(page.getByRole('heading', { level: 1 })).toHaveText('You are offline')
    }
    finally {
      await context.setOffline(false)
    }
  })
})

/** What the worker shows, collected where the lock screen would have it. */
interface Shown {
  title: string
  body?: string
}

/**
 * Hands the worker a push the way a push service would, through DevTools, and
 * returns what it then showed.
 *
 * A real push needs VAPID keys, which E2E deliberately has none of, and the
 * headless browser grants no notification permission whatever it is told. So
 * the one thing replaced is the operating system's half — `showNotification`
 * records instead of drawing. Reading the payload, choosing the language and
 * writing the sentence are the shipped worker's own code.
 */
async function deliverPush(page: import('@playwright/test').Page, payload: object): Promise<Shown[]> {
  const worker = page.context().serviceWorkers()[0]
  expect(worker, 'a service worker to deliver the push to').toBeDefined()

  await worker!.evaluate(() => {
    const scope = self as unknown as ServiceWorkerGlobalScope & { q2Shown: Shown[] }
    scope.q2Shown = []
    scope.registration.showNotification = async (title: string, options?: NotificationOptions) => {
      scope.q2Shown.push({ title, body: options?.body })
    }
  })

  const origin = new URL(page.url()).origin
  const cdp = await page.context().newCDPSession(page)
  const registration = new Promise<string>((resolve) => {
    cdp.on('ServiceWorker.workerRegistrationUpdated', ({ registrations }) => {
      const ours = registrations.find(entry => !entry.isDeleted && entry.scopeURL.startsWith(origin))
      if (ours) resolve(ours.registrationId)
    })
  })

  await cdp.send('ServiceWorker.enable')
  await cdp.send('ServiceWorker.deliverPushMessage', {
    origin,
    registrationId: await registration,
    data: JSON.stringify(payload),
  })

  const shown = () => worker!.evaluate(() => (self as unknown as { q2Shown: Shown[] }).q2Shown)
  await expect.poll(async () => (await shown()).length, { message: 'the worker showed the push' }).toBe(1)
  return shown()
}

/** A friend request as the server pushes it; `language` is what #39 added. */
function friendRequest(language?: 'German' | 'English') {
  return {
    id: null,
    kind: 'FriendRequestReceived',
    actor: {
      id: 'e2e00000-0001-4000-8000-000000000002',
      displayName: 'E2E Jonas',
      handle: '@e2e.jonas',
      initials: 'EJ',
      avatarColor: '#4f46e5',
      isOnline: false,
      avatarImageId: null,
    },
    subject: null,
    excerpt: null,
    amount: null,
    target: 'Person',
    targetId: 'e2e00000-0001-4000-8000-000000000002',
    focusId: null,
    occurredAt: '2026-06-15T09:00:00+00:00',
    isNew: true,
    ...(language ? { language } : {}),
  }
}

test.describe('a push, on a phone set to German', () => {
  test.use({ locale: 'de-DE' })

  test('is written in the language chosen in q2, not the browser\'s (#39)', async ({ page }) => {
    await page.goto('/')
    await waitForServiceWorker(page)

    expect(await deliverPush(page, friendRequest('English'))).toEqual([
      { title: 'Friends', body: 'E2E Jonas wants to be friends with you.' },
    ])
  })

  test('falls back to the browser only when the payload does not say', async ({ page }) => {
    await page.goto('/')
    await waitForServiceWorker(page)

    expect(await deliverPush(page, friendRequest())).toEqual([
      { title: 'Freunde', body: 'E2E Jonas möchte mit dir befreundet sein.' },
    ])
  })
})

test.describe('a push, on a phone set to English', () => {
  test.use({ locale: 'en-US' })

  test('is written in German when German was chosen in q2', async ({ page }) => {
    await page.goto('/')
    await waitForServiceWorker(page)

    expect(await deliverPush(page, friendRequest('German'))).toEqual([
      { title: 'Freunde', body: 'E2E Jonas möchte mit dir befreundet sein.' },
    ])
  })
})
