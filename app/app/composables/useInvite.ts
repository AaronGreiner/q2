import type { ApiFailure } from '~/api/errors'

/**
 * The invite link, and replacing it.
 *
 * The server hands back a code and nothing else, on purpose: it does not know
 * which host the app is served from, and a link with the wrong origin in it is
 * worse than no link. The URL is built here, from the page the person is
 * actually looking at.
 */
export function useInvite() {
  const api = useQ2Api()
  const { report } = useErrorReporter()
  const toast = useToastMessage()
  const t = useMessages()

  const { data, status, refresh } = useAsyncData(
    'invite-code',
    async () => {
      try {
        return { code: (await api.invite.get()).code, failure: null as ApiFailure | null }
      }
      catch (caught) {
        return { code: '', failure: report(caught, { feature: 'invite', action: 'get' }) }
      }
    },
    { default: () => ({ code: '', failure: null as ApiFailure | null }) },
  )

  /**
   * The link itself.
   *
   * Empty during server rendering, because there is no origin to build it from
   * there — the button that uses it is disabled until hydration rather than
   * rendering a link to nowhere.
   */
  const url = computed(() => {
    const code = data.value?.code

    if (!code || !import.meta.client) return ''

    return `${window.location.origin}/join/${encodeURIComponent(code)}`
  })

  const isReplacing = ref(false)

  async function replace() {
    if (isReplacing.value) return

    isReplacing.value = true

    try {
      data.value = { code: (await api.invite.regenerate()).code, failure: null }
    }
    catch (caught) {
      report(caught, { feature: 'invite', action: 'regenerate' })
      await refresh()
    }
    finally {
      isReplacing.value = false
    }
  }

  /**
   * Hands the link to whatever the device offers.
   *
   * The share sheet where there is one, the clipboard otherwise. Both end in
   * the same confirmation, because "did that work?" is the question a person
   * has either way — and a share sheet the person dismisses is not an error.
   */
  async function share() {
    const link = url.value

    if (!link) return

    try {
      if (navigator.share) {
        await navigator.share({
          title: t.value.invite.shareTitle,
          text: t.value.invite.shareText,
          url: link,
        })
        return
      }

      await navigator.clipboard.writeText(link)
      toast.show(t.value.toast.inviteCopied)
    }
    catch {
      // A dismissed share sheet and a clipboard the browser refused both land
      // here, and neither is a defect worth an issue. The link is on screen
      // and can be selected by hand.
    }
  }

  return {
    code: computed(() => data.value?.code ?? ''),
    url,
    error: computed(() => data.value?.failure ?? null),
    isLoading: computed(() => status.value === 'pending'),
    isReplacing: computed(() => isReplacing.value),
    refresh,
    replace,
    share,
  }
}
