/**
 * Draws the q2 app icon and writes every raster size the platforms want.
 *
 *   bun run icons          (from app/)
 *   bun run app:icons      (from the repository root)
 *
 * The icon is lucide's `sparkles` — the same glyph the app already bundles for
 * goals — in white on the near-black `--ui-bg` of the dark theme. One shape,
 * drawn once here, so the tab, the home screen and the install dialog cannot
 * drift apart.
 *
 * Why a script rather than six committed drawings: the geometry differs per
 * platform (a maskable icon needs a safe zone, iOS masks the corners itself,
 * a 16 px favicon needs a bigger glyph to stay readable) and every one of those
 * numbers is a decision. Keeping them in code means the decision is written
 * down and the whole set is regenerated consistently after a change.
 *
 * The output is committed: it is an input to the build, not a product of it.
 *
 * Rasterising uses the Chromium that Playwright already installs for E2E, so
 * nothing new has to be present on the machine or in CI. It is the only
 * SVG renderer this repository has, and it is a good one.
 */
import { mkdir, writeFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { chromium } from '@playwright/test'

const appDir = join(dirname(fileURLToPath(import.meta.url)), '..')
const publicDir = join(appDir, 'public')

/**
 * lucide `sparkles`, verbatim from @iconify-json/lucide 1.2.120 — a 24×24
 * viewBox drawn with a stroke width of 2.
 *
 * Copied rather than imported because this script produces static files: the
 * icon on a person's home screen must not change because a dependency bumped.
 * If it is ever updated deliberately, re-run the script and commit the result.
 */
const sparklesBody = '<g fill="none" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="2"><path d="M11.017 2.814a1 1 0 0 1 1.966 0l1.051 5.558a2 2 0 0 0 1.594 1.594l5.558 1.051a1 1 0 0 1 0 1.966l-5.558 1.051a2 2 0 0 0-1.594 1.594l-1.051 5.558a1 1 0 0 1-1.966 0l-1.051-5.558a2 2 0 0 0-1.594-1.594l-5.558-1.051a1 1 0 0 1 0-1.966l5.558-1.051a2 2 0 0 0 1.594-1.594zM20 2v4m2-2h-4"/><circle cx="4" cy="20" r="2"/></g>'

/** `--ui-bg` and `--ui-text-highlighted` of the dark theme in app/assets/css/main.css. */
const background = '#0a0a0a'
const foreground = '#ffffff'

interface IconShape {
  /** Edge length of the square canvas, in pixels. */
  readonly size: number
  /** How much of that canvas the 24×24 glyph is scaled to fill. */
  readonly glyph: number
  /** Corner radius as a fraction of the canvas, or 0 for a full-bleed square. */
  readonly radius: number
}

function drawIcon({ size, glyph, radius }: IconShape): string {
  const scale = (size * glyph) / 24
  const offset = (size * (1 - glyph)) / 2
  const corner = size * radius

  return [
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${size} ${size}" width="${size}" height="${size}">`,
    `<rect width="${size}" height="${size}"${corner > 0 ? ` rx="${round(corner)}"` : ''} fill="${background}"/>`,
    `<g transform="translate(${round(offset)} ${round(offset)}) scale(${round(scale)})">`,
    sparklesBody.replaceAll('currentColor', foreground),
    '</g>',
    '</svg>',
  ].join('')
}

/** Three decimals is well below a pixel at every size here and keeps the file readable. */
function round(value: number): number {
  return Math.round(value * 1000) / 1000
}

/**
 * A maskable icon is cropped by the platform to whatever shape it likes — a
 * circle, a squircle, a teardrop — and is only guaranteed to keep the middle
 * 80 %. The largest square inside that circle is about 57 % of the canvas, so a
 * glyph at 50 % survives every mask with room to spare, and the artwork must
 * reach the edges: a rounded corner here would be cropped into a notch.
 */
const maskable: IconShape = { size: 512, glyph: 0.5, radius: 0 }

/**
 * iOS applies its own rounded mask to `apple-touch-icon`, so this one is a
 * plain square too. A pre-rounded source would show the mask cutting into
 * corners that are already dark.
 */
const appleTouch: IconShape = { size: 180, glyph: 0.58, radius: 0 }

/** `purpose: any` — shown as drawn, so it carries the rounding itself. */
const anyPurpose = (size: number): IconShape => ({ size, glyph: 0.62, radius: 0.225 })

/**
 * The favicon lives at 16–32 px in a tab strip. The glyph is pushed wider than
 * anywhere else because at that size the small circle and the little cross are
 * two or three pixels each, and any more padding loses them entirely.
 */
const favicon: IconShape = { size: 512, glyph: 0.68, radius: 0.2 }

async function main(): Promise<void> {
  await mkdir(publicDir, { recursive: true })

  // The SVG favicon is the one browsers prefer and the only icon that stays
  // sharp at every size, so it is written as source rather than rasterised.
  const faviconSvg = drawIcon(favicon)
  await write('favicon.svg', faviconSvg)

  const browser = await chromium.launch()
  try {
    const rasterise = createRasteriser(browser)

    await write('favicon.ico', toIco(await rasterise(faviconSvg, 32), 32))
    await write('apple-touch-icon.png', await rasterise(drawIcon(appleTouch), 180))
    await write('pwa-192x192.png', await rasterise(drawIcon(anyPurpose(192)), 192))
    await write('pwa-512x512.png', await rasterise(drawIcon(anyPurpose(512)), 512))
    await write('maskable-512x512.png', await rasterise(drawIcon(maskable), 512))
  }
  finally {
    await browser.close()
  }
}

function createRasteriser(browser: Awaited<ReturnType<typeof chromium.launch>>) {
  return async function rasterise(svg: string, size: number): Promise<Buffer> {
    // A viewport exactly the size of the icon means the screenshot is the icon:
    // no cropping, no scaling, and no half-pixel seam at the edges.
    const page = await browser.newPage({ viewport: { width: size, height: size }, deviceScaleFactor: 1 })
    try {
      await page.setContent(`<style>html,body{margin:0}svg{display:block;width:${size}px;height:${size}px}</style>${svg}`)
      return await page.screenshot({ type: 'png' })
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

process.stdout.write('Generating q2 app icons\n')
await main()
