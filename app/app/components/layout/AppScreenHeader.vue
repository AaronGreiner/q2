<script setup lang="ts">
/**
 * The fixed bar at the top of a screen.
 *
 * Two shapes, because the app has exactly two: a main screen with a large
 * title, and a pushed screen with a back arrow and a small one. Anything else
 * a screen needs goes in the `actions` slot.
 */
withDefaults(defineProps<{
  title: string
  /** Where the back arrow goes. Omit it and no arrow is drawn. */
  backTo?: string
  backLabel?: string
  /** Small line above the title, e.g. the greeting on the start screen. */
  eyebrow?: string
}>(), {
  backTo: undefined,
  backLabel: undefined,
  eyebrow: undefined,
})
</script>

<template>
  <header
    class="flex shrink-0 items-center gap-2 px-[18px] pt-1.5 pb-2"
    :class="backTo ? '' : 'items-start justify-between'"
  >
    <NuxtLink
      v-if="backTo"
      :to="backTo"
      class="-ms-3 flex size-11 shrink-0 items-center justify-center rounded-full text-(--ui-text) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :aria-label="backLabel"
      data-testid="back-link"
    >
      <UIcon
        name="i-lucide-chevron-left"
        class="size-6"
        aria-hidden="true"
      />
    </NuxtLink>

    <div class="min-w-0 flex-1">
      <p
        v-if="eyebrow"
        class="text-[13px] font-medium text-(--ui-text-muted)"
      >
        {{ eyebrow }}
      </p>
      <h1
        class="truncate font-extrabold tracking-tight"
        :class="backTo ? 'text-base' : 'text-[23px]'"
      >
        {{ title }}
      </h1>
    </div>

    <div class="flex shrink-0 items-center gap-2">
      <slot name="actions" />
    </div>
  </header>
</template>
