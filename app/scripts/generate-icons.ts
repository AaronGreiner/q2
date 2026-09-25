/**
 * Draws the q2 app icon and writes every raster size the platforms want.
 *
 *   bun run icons          (from app/)
 *   bun run app:icons      (from the repository root)
 *
 * The icon is the Q2 ligature, light on charcoal in the Ruhe palette. One shape, drawn once here, so the tab, the home screen and the install
 * dialog cannot drift apart.
 *
 * Why a script rather than six committed drawings: the geometry differs per
 * platform (a maskable icon needs a safe zone, iOS masks the corners itself,
 * a 16 px favicon needs a bigger glyph to stay readable) and every one of those
 * numbers is a decision. Keeping them in code means the decision is written
 * down and the whole set is regenerated consistently after a change.
 *
 * The output is committed: it is an input to the build, not a product of it.
 * That includes the iOS app's icon and launch screen inside `ios/`, which are
 * written here rather than drawn in Xcode for the same reason.
 *
 * Rasterising uses the Chromium that Playwright already installs for E2E, so
 * nothing new has to be present on the machine or in CI. It is the only
 * SVG renderer this repository has, and it is a good one.
 */
import { mkdir, writeFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { chromium } from '@playwright/test'
import { q2MarkHeight, q2MarkPath, q2MarkWidth } from '../app/utils/q2Mark'
import { installedThemeColor } from '../app/utils/themeColors'

const appDir = join(dirname(fileURLToPath(import.meta.url)), '..')
const publicDir = join(appDir, 'public')
const iosAssetsDir = join(appDir, 'ios', 'App', 'App', 'Assets.xcassets')

/**
 * The dark theme's `--ui-text` on its `--q2-surface`, from
 * app/assets/css/main.css (ADR 0028).
 *
 * Neutral on purpose: the accent is a per-device choice and an icon is drawn
 * once for everybody, so it cannot follow one. The surface rather than
 * `--ui-bg` because it is the charcoal the content sits on, and it keeps the
 * tile from reading as a hole in a dark home screen. The shape of the mark,
 * not a colour, is what tells q2 apart from the other apps there.
 */
const background = '#1c1b19'
const foreground = '#edecea'

interface IconShape {
  /** Edge length of the square canvas, in pixels. */
  readonly size: number
  /** How much of the canvas's width the mark is scaled to fill; it is centred on both axes. */
  readonly glyph: number
  /** Corner radius as a fraction of the canvas, or 0 for a full-bleed square. */
  readonly radius: number
}

function drawIcon({ size, glyph, radius }: IconShape): string {
  const scale = (size * glyph) / q2MarkWidth
  const left = (size - q2MarkWidth * scale) / 2
  const top = (size - q2MarkHeight * scale) / 2
  const corner = size * radius

  return [
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${size} ${size}" width="${size}" height="${size}">`,
    `<rect width="${size}" height="${size}"${corner > 0 ? ` rx="${round(corner)}"` : ''} fill="${background}"/>`,
    `<path transform="translate(${round(left)} ${round(top)}) scale(${round(scale)})" fill="${foreground}" fill-rule="evenodd" d="${q2MarkPath}"/>`,
    '</svg>',
  ].join('')
}

function drawBackground(size: number): string {
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${size} ${size}" width="${size}" height="${size}"><rect width="${size}" height="${size}" fill="${installedThemeColor}"/></svg>`
}

/** Three decimals is well below a pixel at every size here and keeps the file readable. */
function round(value: number): number {
  return Math.round(value * 1000) / 1000
}

/**
 * A maskable icon is cropped by the platform to whatever shape it likes — a
 * circle, a squircle, a teardrop — and is only guaranteed to keep the middle
 * 80 %. The mark is wider than it is tall, so what has to fit in that circle is
 * its diagonal — about 1.18 × its width. At 60 % of the canvas the diagonal is
 * 71 %, inside the 80 % with room to spare, and the artwork must reach the
 * edges: a rounded corner here would be cropped into a notch.
 */
const maskable: IconShape = { size: 512, glyph: 0.6, radius: 0 }

/**
 * iOS applies its own rounded mask to `apple-touch-icon`, so this one is a
 * plain square too. A pre-rounded source would show the mask cutting into
 * corners that are already dark. The mark spans 72 % of the width — the
 * proportion of the supplied artwork — which leaves its height at 45 %, well
 * clear of the mask's corners.
 */
const appleTouch: IconShape = { size: 180, glyph: 0.72, radius: 0 }

/**
 * The iOS app's icon: one 1024 px square, from which Xcode derives every other
 * size. The apple-touch geometry, because it is the same home screen and the
 * same mask. No alpha channel — App Store Connect refuses an icon that has
 * one, and a screenshot of an opaque page has none.
 */
const iosAppIcon: IconShape = { size: 1024, glyph: 0.72, radius: 0 }

/**
 * The launch screen: the background the app opens on, and nothing else.
 *
 * It is shown for the moment before the WebView paints, so anything drawn on
 * it would appear and vanish again. `installedThemeColor` is what the web app
 * manifest paints behind an installed PWA and what capacitor.config.ts puts
 * behind the WebView, so the three agree and a launch never flashes.
 */
const iosLaunchScreenSize = 2732

/** `purpose: any` — shown as drawn, so it carries the rounding itself. */
const anyPurpose = (size: number): IconShape => ({ size, glyph: 0.7, radius: 0.225 })

/**
 * The favicon lives at 16–32 px in a tab strip, where the mark is only nine
 * pixels tall at the smaller size and each stroke is barely one of them. It is
 * pushed wider than anywhere else because any more padding closes the counter
 * of the Q and the gap between the tail and the 2.
 */
const favicon: IconShape = { size: 512, glyph: 0.86, radius: 0.2 }

async function main(): Promise<void> {
  await mkdir(publicDir, { recursive: true })

  // The SVG favicon is the one browsers prefer and the only icon that stays
  // sharp at every size, so it is written as source rather than rasterised.
  const faviconSvg = drawIcon(favicon)
  await write('favicon.svg', faviconSvg)

  const browser = await chromium.launch()
  try {
    const rasterise = createRasteriser(browser)

    await write('favicon.ico', toIco(await rasterise(faviconSvg, 32, 'rounded'), 32))
    await write('apple-touch-icon.png', await rasterise(drawIcon(appleTouch), 180))
    await write('pwa-192x192.png', await rasterise(drawIcon(anyPurpose(192)), 192, 'rounded'))
    await write('pwa-512x512.png', await rasterise(drawIcon(anyPurpose(512)), 512, 'rounded'))
    await write('maskable-512x512.png', await rasterise(drawIcon(maskable), 512))

    await writeIos('AppIcon.appiconset/AppIcon-512@2x.png', await rasterise(drawIcon(iosAppIcon), iosAppIcon.size))

    // Three files because the image set names one per scale; they are the
    // same flat colour at every scale.
    const launch = await rasterise(drawBackground(iosLaunchScreenSize), iosLaunchScreenSize)
    for (const name of ['splash-2732x2732.png', 'splash-2732x2732-1.png', 'splash-2732x2732-2.png']) {
      await writeIos(`Splash.imageset/${name}`, launch)
    }
  }
  finally {
    await browser.close()
  }
}

function createRasteriser(browser: Awaited<ReturnType<typeof chromium.launch>>) {
  /**
   * `rounded` is for the icons that carry their own corners: outside them the
   * page has to stay transparent, or the corners come out as white wedges on
   * every dark surface. Everything else is left opaque on purpose, because a
   * PNG with an alpha channel is what App Store Connect refuses.
   */
  return async function rasterise(svg: string, size: number, corners: 'square' | 'rounded' = 'square'): Promise<Buffer> {
    // A viewport exactly the size of the icon means the screenshot is the icon:
    // no cropping, no scaling, and no half-pixel seam at the edges.
    const page = await browser.newPage({ viewport: { width: size, height: size }, deviceScaleFactor: 1 })
    try {
      await page.setContent(`<style>html,body{margin:0}svg{display:block;width:${size}px;height:${size}px}</style>${svg}`)
      return await page.screenshot({ type: 'png', omitBackground: corners === 'rounded' })
    }
    finally {
      await page.close()
    }
  }
}

/**
 * Wraps a PNG in an ICO container.
 *
 * `favicon.ico` is only there for the browsers that ignore `favicon.svg`, and
 * every one of those understands a PNG inside the container — so this is a
 * 22-byte header in front of the image rather than a bitmap encoder.
 */
function toIco(png: Buffer, size: number): Buffer {
  const header = Buffer.alloc(22)

  header.writeUInt16LE(0, 0) // reserved
  header.writeUInt16LE(1, 2) // 1 = icon
  header.writeUInt16LE(1, 4) // one image

  header.writeUInt8(size, 6) // width
  header.writeUInt8(size, 7) // height
  header.writeUInt8(0, 8) // palette size: 0 = truecolour
  header.writeUInt8(0, 9) // reserved
  header.writeUInt16LE(1, 10) // colour planes
  header.writeUInt16LE(32, 12) // bits per pixel
  header.writeUInt32LE(png.length, 14)
  header.writeUInt32LE(header.length, 18) // the image starts right after the header

  return Buffer.concat([header, png])
}

async function write(name: string, contents: string | Buffer): Promise<void> {
  await writeFile(join(publicDir, name), contents)
  process.stdout.write(`  public/${name} (${contents.length} bytes)\n`)
}

async function writeIos(name: string, contents: Buffer): Promise<void> {
  await writeFile(join(iosAssetsDir, name), contents)
  process.stdout.write(`  ios/App/App/Assets.xcassets/${name} (${contents.length} bytes)\n`)
}

process.stdout.write('Generating q2 app icons\n')
await main()
