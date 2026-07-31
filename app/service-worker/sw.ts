/// <reference lib="webworker" />

/**
 * The q2 service worker.
 *
 * It exists so the application can be installed — Chrome and the Android
 * WebView only offer "install" to a site whose service worker handles `fetch`
 * — and it deliberately does very little beyond that.
 *
 * What it caches: the hashed build output. Nothing else.
 * What it never caches: **HTML and API responses.**
 *
 * That second line is the whole design. q2 renders on the server and every
 * screen is somebody's signed-in one, so a cached document is one person's
 * goals sitting in a cache directory on a device that another person may pick
 * up next. The build assets have no such problem: they are the same bytes for
 * everybody and their file names already contain a content hash, so a cached
 * copy can never be the wrong version of itself.
 *
 * The consequence is that q2 does not work offline, and is not meant to. What
 * the cache buys is a fast, JavaScript-complete start on a slow connection,
 * and an honest page instead of the browser's dinosaur when there is no
 * connection at all. See docs/adr/0012-installable-pwa.md.
 *
 * Built by @vite-pwa/nuxt in `injectManifest` mode: the precache list below is
 * substituted at build time, everything else here is ours.
 */
import { cleanupOutdatedCaches, precacheAndRoute } from 'workbox-precaching'
import { NavigationRoute, registerRoute } from 'workbox-routing'
import { NetworkOnly } from 'workbox-strategies'
import { de, en, type Messages } from '../app/i18n/messages'

declare const self: ServiceWorkerGlobalScope & {
  /** Replaced at build time with the precache list; see `injectManifest` in nuxt.config.ts. */
  __WB_MANIFEST: Array<{ url: string, revision: string | null }>
}

precacheAndRoute(self.__WB_MANIFEST)

// A deployment changes the precache name; without this the previous one stays
// on the device for good.
cleanupOutdatedCaches()

/**
 * Every navigation goes to the network, exactly as it would without a service
 * worker — and when the network is not there, this is where the offline page
 * comes from.
 *
 * `NetworkOnly` rather than a caching strategy is the point, not an
 * oversight: see the note about signed-in HTML at the top of this file.
 *
 * API calls need no route at all. They go to a different origin and nothing
 * here matches them, so they reach the network untouched — which is the
 * behaviour we want and the reason it is written down rather than configured.
 */
registerRoute(new NavigationRoute(new NetworkOnly({
  plugins: [{ handlerDidError: async () => offlinePage() }],
})))

/*
 * Take over as soon as a new version is installed, rather than waiting for
 * every tab to close.
 *
 * q2 is used as an app: it is opened, used and left running for weeks, so
 * "the update applies once you have closed all windows" would mean it never
 * applies. The usual objection — a running page suddenly served by a newer
 * worker — does not arise here, because the worker serves that page no HTML
 * and no data, only content-hashed assets that are still the ones its own
 * document asked for.
 */
self.addEventListener('install', () => {
  void self.skipWaiting()
})

self.addEventListener('activate', (event) => {
  event.waitUntil(self.clients.claim())
})

/**
 * The page a person sees when a navigation cannot reach the server.
 *
 * Written out here rather than precached as a file so that it cannot fall out
 * of step with the worker that serves it, and because it is the one page in q2
 * that has to render with the application stopped: no bundle, no design
 * tokens, no fonts.
 *
 * It therefore uses the CSS system colours — `Canvas` and `CanvasText` follow
 * the operating system's light or dark setting on their own. That is also why
 * this file names no colour, which is the rule app/assets/css/main.css sets:
 * a hex value here would be a copy of a token, and copies drift.
 */
function offlinePage(): Response {
  const t = language()
  const lang = t === de ? 'de' : 'en'

  const html = `<!doctype html>
<html lang="${lang}">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<title>${escape(t.offline.title)} · ${escape(t.app.name)}</title>
<style>
  :root { color-scheme: light dark; }
  body {
    margin: 0;
    min-height: 100dvh;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 1rem;
    padding: 2rem 1.5rem calc(2rem + env(safe-area-inset-bottom));
    box-sizing: border-box;
    background: Canvas;
    color: CanvasText;
    font-family: system-ui, -apple-system, "Segoe UI", sans-serif;
    text-align: center;
  }
  img { width: 4.5rem; height: 4.5rem; border-radius: 1.125rem; }
  h1 { margin: 0; font-size: 1.375rem; line-height: 1.3; }
  p { margin: 0; max-width: 26rem; line-height: 1.5; opacity: 0.75; }
  button {
    margin-top: 0.5rem;
    min-height: 44px;
    padding: 0 1.5rem;
    border: 1px solid;
    border-radius: 0.75rem;
    background: Canvas;
    color: CanvasText;
    font: inherit;
    font-weight: 600;
    cursor: pointer;
  }
</style>
</head>
<body>
<img src="/favicon.svg" alt="" width="72" height="72">
<h1>${escape(t.offline.heading)}</h1>
<p>${escape(t.offline.body)}</p>
<button type="button" id="retry">${escape(t.offline.retry)}</button>
<script>document.getElementById('retry').addEventListener('click', function () { location.reload() })</script>
</body>
</html>`

  return new Response(html, {
    status: 200,
    headers: {
      'content-type': 'text/html; charset=utf-8',

      // The offline page is a stand-in for a real page at this URL. Storing it
      // would leave it in front of the real one after the connection is back.
      'cache-control': 'no-store',
    },
  })
}

/**
 * Which language to say it in.
 *
 * The person's actual preference is stored on their account, and this runs at
 * the one moment the account is unreachable. The browser's language is the
 * best guess available; German is the product default and the fallback.
 */
function language(): Messages {
  return self.navigator.language.toLowerCase().startsWith('en') ? en : de
}

/** These strings come from our own catalogue, but they are still going into markup. */
function escape(value: string): string {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
}
