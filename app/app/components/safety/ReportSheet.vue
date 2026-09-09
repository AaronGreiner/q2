<script setup lang="ts">
import type { ReportReason, ReportTargetKind } from '~/api/types'
import { reportNoteLimit, reportReasons } from '~/api/types'

/**
 * Asking for something to be looked at.
 *
 * The one sheet in q2 whose tone is deliberately flat. Everywhere else the app
 * encourages; here somebody has just seen something they did not want to, and
 * cheerfulness would read as not taking it seriously.
 *
 * Three things about it are rules rather than layout:
 *
 * - **It says the report is anonymous, where the button is.** People do not
 *   report a friend if they think the friend will find out, and the ones who
 *   most need to report are the ones who will assume the worst unless told.
 * - **The note is optional.** Requiring a sentence means somebody in a hurry
 *   sends nothing at all.
 * - **Blocking is offered beside it, not instead of it.** They are different
 *   things — one asks somebody else to act, the other acts now — and the person
 *   who needs one usually wants both.
 */
withDefaults(defineProps<{
  targetKind: ReportTargetKind
  targetId: string
  /**
   * Whose it is, when there is a person to block as well.
   *
   * Absent for a photograph in the swipe feed: the uploader is a friend on a
   * shared goal, and offering "block" under a picture somebody is halfway
   * through judging would put a decision about a person under a decision about
   * one photograph.
   */
  personId?: string | null
  busy?: boolean
}>(), {
  personId: null,
  busy: false,
})

const emit = defineEmits<{
  report: [reason: ReportReason, note: string]
  block: []
}>()

const open = defineModel<boolean>('open', { required: true })

const t = useMessages()

const reason = ref<ReportReason | null>(null)
const note = ref('')

watch(open, (isOpen) => {
  if (!isOpen) return

  reason.value = null
  note.value = ''
})

/** The wording for each reason lives in the catalogue, keyed by its name. */
const labels = computed<Record<ReportReason, string>>(() => ({
  Faked: t.value.safety.reasonFaked,
  Inappropriate: t.value.safety.reasonInappropriate,
  Harassment: t.value.safety.reasonHarassment,
  Spam: t.value.safety.reasonSpam,
  Other: t.value.safety.reasonOther,
}))

function submit() {
  if (!reason.value) return

  emit('report', reason.value, note.value.trim())
}
</script>

<template>
  <UDrawer
    v-model:open="open"
    :title="t.safety.reportHeading"
    :description="t.safety.reportIntro"
    :ui="{ container: 'max-w-[430px] mx-auto' }"
  >
    <template #body>
      <div class="flex flex-col gap-4 pb-2">
        <div
          class="flex flex-col"
          role="radiogroup"
          :aria-label="t.safety.reportHeading"
        >
          <button
            v-for="option in reportReasons"
            :key="option"
            type="button"
            role="radio"
            :aria-checked="reason === option"
            class="flex min-h-11 items-center gap-3 border-b border-(--ui-border) py-3 text-left last:border-b-0 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-(--ui-primary)"
            :data-testid="`report-reason-${option}`"
            @click="reason = option"
          >
            <span
              class="flex size-5 shrink-0 items-center justify-center rounded-full border-2"
              :class="reason === option ? 'border-(--q2-accent-solid)' : 'border-(--ui-border-accented)'"
              aria-hidden="true"
            >
              <span
                v-if="reason === option"
                class="size-2.5 rounded-full bg-(--q2-accent-solid)"
              />
            </span>

            <span class="min-w-0 flex-1 text-sm font-semibold">{{ labels[option] }}</span>
          </button>
        </div>

        <UFormField :label="t.safety.reportNote">
          <UTextarea
            v-model="note"
            :rows="3"
            :maxlength="reportNoteLimit"
            :placeholder="t.safety.reportNotePlaceholder"
            class="w-full"
            data-testid="report-note"
          />
        </UFormField>

        <UButton
          block
          size="xl"
          color="error"
          icon="i-lucide-flag"
          :label="t.safety.reportSubmit"
          :disabled="!reason || busy"
          :loading="busy"
          data-testid="report-submit"
          @click="submit"
        />

        <!--
          Beside the report, never instead of it. Reporting asks somebody else
          to act; blocking acts now, and the person who needs one usually wants
          both.
        -->
        <UButton
          v-if="personId"
          block
          size="lg"
          color="neutral"
          variant="outline"
          icon="i-lucide-shield"
          :label="t.safety.block"
          :disabled="busy"
          data-testid="report-block"
          @click="emit('block')"
        />
      </div>
    </template>
  </UDrawer>
</template>
