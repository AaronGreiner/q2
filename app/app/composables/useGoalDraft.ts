import type { ApiFailure } from '~/api/errors'
import type { GoalDraft, SaveGoalDraftRequest } from '~/api/types'
import { isFirstLoad, placeholder } from '~/utils/firstLoad'

interface DraftPayload {
  draft: GoalDraft | null
  hasFriends: boolean
  failure: ApiFailure | null
}

/**
 * The Do somebody without friends has waiting, and whether they have a friend
 * at all (docs/adr/0039-first-run-onboarding.md).
 *
 * One small read that answers both, because the two always travel together:
 * the start screen, the Dos tab and the create sheet each need to know whether
 * there is a waiting Do to show and, if there is none, whether the way forward
 * is a goal or the invite link.
 *
 * Under its own key, which the live connection refreshes with the goals: the
 * moment a friend arrives through the link, the server turns the draft into a
 * goal and every open screen swaps the waiting card for it.
 */
export function useGoalDraft() {
  const api = useQ2Api()
  const { report } = useErrorReporter()

  const { data, status, refresh } = useAsyncData<DraftPayload>(
    'goal-draft',
    async () => {
      try {
        const state = await api.goals.draft()
        return { draft: state.draft ?? null, hasFriends: state.hasFriends, failure: null }
      }
      catch (caught) {
        // Assumes a friend: on a failed read, the ordinary create sheet is the
        // safer thing to show than an offer to wait for somebody.
        return { draft: null, hasFriends: true, failure: report(caught, { feature: 'goals', action: 'draft' }) }
      }
    },
    { default: (): DraftPayload => placeholder({ draft: null, hasFriends: true, failure: null }) },
  )

  const isSaving = ref(false)
  const saveError = ref<ApiFailure | null>(null)

  /** Writes the waiting Do. Resolves to it, or to null with the reason in `saveError`. */
  async function save(request: SaveGoalDraftRequest): Promise<GoalDraft | null> {
    if (isSaving.value) return null

    isSaving.value = true
    saveError.value = null

    try {
      const draft = await api.goals.saveDraft(request)
      data.value = { draft, hasFriends: false, failure: null }
      return draft
    }
    catch (caught) {
      saveError.value = report(caught, { feature: 'goals', action: 'saveDraft' })
      return null
    }
    finally {
      isSaving.value = false
    }
  }

  /** Throws the waiting Do away. Resolves to whether it went. */
  async function discard(): Promise<boolean> {
    if (isSaving.value) return false

    isSaving.value = true

    try {
      await api.goals.discardDraft()
      data.value = { draft: null, hasFriends: data.value?.hasFriends ?? false, failure: null }
      return true
    }
    catch (caught) {
      report(caught, { feature: 'goals', action: 'discardDraft' })
      return false
    }
    finally {
      isSaving.value = false
    }
  }

  return {
    draft: computed(() => data.value?.draft ?? null),
    hasFriends: computed(() => data.value?.hasFriends ?? true),
    error: computed(() => data.value?.failure ?? null),
    isLoading: computed(() => isFirstLoad(status.value, data.value)),
    isSaving: computed(() => isSaving.value),
    saveError: computed(() => saveError.value),
    refresh,
    save,
    discard,
  }
}
