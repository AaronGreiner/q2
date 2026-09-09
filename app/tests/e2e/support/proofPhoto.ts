import { deflateSync } from 'node:zlib'
import type { Page } from '@playwright/test'

/**
 * Delivering a photograph, the way a person does it.
 *
 * Since stage 4 a window is closed by a picture other people vote on, so an
 * E2E test that wants a delivered window has to produce actual bytes. The
 * camera is not available to a headless browser, which is exactly why
 * `PhotoCapture` has a file fallback — the same one somebody without a working
 * camera uses.
 */

/** Big enough to pass `StoredImage.MinDimension` and nothing more. */
const edge = 64

/**
 * A valid PNG of one flat colour, built here rather than committed.
 *
 * A binary fixture in the repository is a file nobody can review and one more
 * thing to keep in step with the size rules it has to satisfy. Twenty lines of
 * encoder are cheaper, and they say what the bytes are.
 */
export function pngBytes(): Buffer {
  const raw = Buffer.alloc(edge * (edge * 3 + 1))

  for (let y = 0; y < edge; y++) {
    const row = y * (edge * 3 + 1)
    raw[row] = 0 // filter: none

    for (let x = 0; x < edge; x++) {
      raw.writeUInt8(60, row + 1 + x * 3)
      raw.writeUInt8(90, row + 2 + x * 3)
      raw.writeUInt8(140, row + 3 + x * 3)
    }
  }

  const header = Buffer.alloc(13)
  header.writeUInt32BE(edge, 0)
  header.writeUInt32BE(edge, 4)
  header.writeUInt8(8, 8) // bit depth
  header.writeUInt8(2, 9) // colour type: truecolour

  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
    chunk('IHDR', header),
    chunk('IDAT', deflateSync(raw)),
    chunk('IEND', Buffer.alloc(0)),
  ])
}

/**
 * Delivers a photograph into whatever window the open screen is offering.
 *
 * The caller has already pressed the button that opens the camera; this is the
 * file fallback and the confirmation behind it.
 */
export async function deliverPhoto(page: Page) {
  await page.getByTestId('photo-file').setInputFiles({
    name: 'proof.png',
    mimeType: 'image/png',
    buffer: pngBytes(),
  })

  await page.getByTestId('photo-confirm').click()
}

function chunk(type: string, body: Buffer): Buffer {
  const length = Buffer.alloc(4)
  length.writeUInt32BE(body.length, 0)

  const typed = Buffer.concat([Buffer.from(type, 'ascii'), body])
  const crc = Buffer.alloc(4)
  crc.writeUInt32BE(crc32(typed), 0)

  return Buffer.concat([length, typed, crc])
}

const crcTable = Array.from({ length: 256 }, (_, index) => {
  let value = index

  for (let bit = 0; bit < 8; bit++) {
    value = value & 1 ? 0xEDB88320 ^ (value >>> 1) : value >>> 1
  }

  return value >>> 0
})

function crc32(bytes: Buffer): number {
  let value = 0xFFFFFFFF

  for (const byte of bytes) {
    value = crcTable[(value ^ byte) & 0xFF]! ^ (value >>> 8)
  }

  return (value ^ 0xFFFFFFFF) >>> 0
}
