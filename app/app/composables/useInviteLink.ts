import type { ApiFailure } from '~/api/errors'
import type { InvitePreview, Person } from '~/api/types'
import { isFirstLoad, placeholder } from '~/utils/firstLoad'

/**
 * Somebody else's invite link: whose it is, and accepting it.
 *
 * The other end of `useInvite`. The link lands on `/join/<code>`, which is
 * server-rendered — so whatever the page needs has to be *read* there rather
 * than carried over from a previous request. The first version put the code
 * into `useState` and redirected to the sign-up form; on the server that
 * redirect is a 302, a fresh request, and the code never arrived.
 *
 * A code that means nothing comes back as a `notFound` failure, which is an
 * answer rather than an incident: `report` keeps it out of Sentry.
 */
export function useInviteLink(code: string) {
  const api = useQ2Api()
  const { report } = useErrorReporter()
  const toast = useToastMessage()
  const t = useMessages()

  const { data, status, refresh } = useAsyncData(
    'invite-preview',
    async () => {
      try {
        return { preview: await api.invite.preview(code) as InvitePreview | null, failure: null as ApiFailure | null }
      }
      catch (caught) {
        return { preview: null, failure: report(caught, { feature: 'invite', action: 'preview' }) }
      }
    },
    { default: () => placeholder({ preview: null as InvitePreview | null, failure: null as ApiFailure | null }) },
  )

  const isAccepting = ref(false)
  const acceptFailure = ref<ApiFailure | null>(null)

  /**
   * Friends with whoever sent the link. Resolves to who that is, or null if
   * it did not work — in which case `acceptFailure` says why.
   *
   * The toast says it without the name. Toasts are teleported out of reach of
   * the attributes that keep a name out of Session Replay, and "you are
   * friends now" is the whole news anyway.
   */
  async function accept(): Promise<Person | null> {
    if (isAccepting.value) return null

    isAccepting.value = true
    acceptFailure.value = null

    try {
      const sender = await api.invite.accept(code)
      toast.show(t.value.toast.befriended)
      return sender
    }
    catch (caught) {
      acceptFailure.value = report(caught, { feature: 'invite', action: 'accept' })
      return null
    }
    finally {
      isAccepting.value = false
    }
  }

  return {
    preview: computed(() => data.value?.preview ?? null),
    error: computed(() => data.value?.failure ?? null),
    isLoading: computed(() => isFirstLoad(status.value, data.value)),
    isAccepting: computed(() => isAccepting.value),
    acceptFailure: computed(() => acceptFailure.value),
    refresh,
    accept,
  }
}
