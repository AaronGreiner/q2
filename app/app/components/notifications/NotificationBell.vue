<script setup lang="ts">
/**
 * The bell, with the number of things that arrived since it was last opened.
 *
 * It used to lead to the friends' feed and deliberately carried no number: a
 * count on "have my friends done anything" turns their doing into something to
 * clear. It leads to what concerns *you* now — a verdict, an acceptance,
 * somebody's kudos — and a number on that is the same honest count the chat
 * tab has (docs/adr/0024-one-notification-pipeline.md).
 *
 * The number is drawn exactly as the tab bar draws its badges, so the three
 * places the app says "something is waiting" look like one idea.
 */
const props = defineProps<{
  /** Lines in the bell that arrived since it was last opened. */
  count: number
}>()

const t = useMessages()

const label = computed(() => (props.count > 0 ? t.value.notify.openWithCount(props.count) : t.value.notify.open))

// Past two digits the exact number stops meaning anything and starts
// widening the badge off the button.
const shown = computed(() => (props.count > 99 ? '99+' : String(props.count)))
</script>

<template>
  <UButton
    to="/notifications"
    icon="i-lucide-bell"
    color="neutral"
    variant="outline"
    size="lg"
    :ui="{ base: 'relative size-11 justify-center rounded-full' }"
    :aria-label="label"
    data-testid="open-notifications"
  >
    <span
      v-if="count > 0"
      class="absolute -end-1 -top-1 flex h-[18px] min-w-[18px] items-center justify-center rounded-full bg-(--q2-accent-solid) px-1 text-[10px] font-extrabold text-(--q2-accent-contrast)"
      aria-hidden="true"
      data-testid="bell-count"
      data-q2-block
    >{{ shown }}</span>
  </UButton>
</template>
