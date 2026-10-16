<script setup lang="ts">
/**
 * The arrow at the top left of a pushed screen.
 *
 * It goes back to the screen this one was opened from (#118), not to `to`:
 * `to` is only where it goes when there is nothing in q2 to go back to — see
 * useGoBack. It is still a link to that address, so it is announced as one, the
 * server renders somewhere real for it to point, and a modified click opens
 * that address the way a browser would.
 */
const props = defineProps<{
  /** The screen's parent, for when this screen was the first thing q2 showed. */
  to: string
  label?: string
}>()

const t = useMessages()
const router = useRouter()
const goBack = useGoBack()

const href = computed(() => router.resolve(props.to).href)

function onClick(event: MouseEvent) {
  if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return

  event.preventDefault()
  goBack(props.to)
}
</script>

<template>
  <!-- `-ms-3`: the box is bigger than the arrow, and lining up the box would
       leave the glyph looking indented against every other screen's title. -->
  <a
    :href="href"
    class="-ms-3 flex size-11 shrink-0 items-center justify-center rounded-full text-(--ui-text) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
    :aria-label="label ?? t.common.back"
    data-testid="back-link"
    @click="onClick"
  >
    <UIcon
      name="i-lucide-chevron-left"
      class="size-6"
      aria-hidden="true"
    />
  </a>
</template>
