import { afterEach, describe, expect, it, vi } from 'vitest'
import { saveFile } from '~/utils/saveFile'

/**
 * Handing over the data export (#36): a download, or the share sheet in the
 * app. Here rather than with the unit tests for the DOM a link is clicked in.
 */
describe('saveFile', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('downloads the file under its name in a browser', async () => {
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    vi.stubGlobal('URL', Object.assign(URL, { createObjectURL: vi.fn(() => 'blob:export'), revokeObjectURL: vi.fn() }))

    await saveFile(new Blob(['zip'], { type: 'application/zip' }), 'qdos-2026-10-20.zip', { native: false })

    expect(click).toHaveBeenCalledOnce()
    const link = click.mock.contexts[0] as HTMLAnchorElement
    expect(link.download).toBe('qdos-2026-10-20.zip')
    expect(link.href).toBe('blob:export')
  })

  it('offers it to the system in the app, where a download goes nowhere', async () => {
    const share = vi.fn().mockResolvedValue(undefined)
    vi.stubGlobal('navigator', { ...navigator, canShare: () => true, share })
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})

    await saveFile(new Blob(['zip']), 'qdos-2026-10-20.zip', { native: true })

    expect(share).toHaveBeenCalledOnce()
    expect((share.mock.calls[0]?.[0] as { files: File[] }).files[0]?.name).toBe('qdos-2026-10-20.zip')
    expect(click).not.toHaveBeenCalled()
  })
})
