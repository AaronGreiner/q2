/**
 * Downloading everything q2 holds about the signed-in person (#36).
 *
 * The server writes the ZIP; this fetches it with the session — the cookie in
 * a browser, the token in the iOS app — and hands it over (`saveFile`).
 */
export function useDataExport() {
  const api = useQ2Api()
  const { report } = useErrorReporter()
  const toast = useToastMessage()
  const t = useMessages()

  // Present only inside the iOS app (plugins/native.client.ts).
  const native = Boolean(useNuxtApp().$sessionTokens)

  const isExporting = ref(false)

  async function exportData() {
    if (isExporting.value) return

    isExporting.value = true

    try {
      const blob = await api.accounts.exportData()
      await saveFile(blob, `qdos-${new Date().toISOString().slice(0, 10)}.zip`, { native })
    }
    catch (caught) {
      // Closing the share sheet is not a failure.
      if (caught instanceof DOMException && caught.name === 'AbortError') return

      report(caught, { feature: 'account', action: 'export' })
      toast.show({ icon: 'i-lucide-circle-alert', text: t.value.settings.exportFailed })
    }
    finally {
      isExporting.value = false
    }
  }

  return { isExporting, exportData }
}
