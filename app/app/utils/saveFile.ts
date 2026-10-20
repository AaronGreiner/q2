/**
 * Hands a file to the person: a download in a browser, the share sheet in the
 * iOS app (#36).
 *
 * A WebView has no download manager — a link with `download` does nothing in
 * it — so the app offers the file to the system, which can save it to Files or
 * send it on. Elsewhere the ordinary download, which is what a desktop expects
 * and what Safari and Chrome on a phone already turn into a saved file.
 */
export async function saveFile(blob: Blob, name: string, options: { native: boolean }): Promise<void> {
  const file = new File([blob], name, { type: blob.type || 'application/zip' })

  if (options.native && typeof navigator.canShare === 'function' && navigator.canShare({ files: [file] })) {
    await navigator.share({ files: [file] })
    return
  }

  const url = URL.createObjectURL(file)
  const link = document.createElement('a')
  link.href = url
  link.download = name
  document.body.append(link)
  link.click()
  link.remove()

  // Long enough for the browser to have started on it.
  setTimeout(() => URL.revokeObjectURL(url), 60_000)
}
