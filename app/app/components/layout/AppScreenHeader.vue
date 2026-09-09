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
  /** The title contains a person's name rather than product copy. */
  privateTitle?: boolean
}>(), {
  backTo: undefined,
  backLabel: undefined,
  eyebrow: undefined,
  privateTitle: false,
})
</script>

<template>
  <!-- A pushed screen gets a hairline under the bar, the way the chat thread
       has one: its title is small and sits right on top of the content, so
       without a rule the two run together. A main screen carries a large title
       that reads as a heading of the page rather than a bar over it. -->
  <header
    class="flex shrink-0 items-center gap-2 px-[18px] pt-1.5 pb-3"
    :class="backTo ? 'border-b border-(--ui-border)' : 'items-start justify-between'"
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
        class="q2-eyebrow"
      >
        {{ eyebrow }}
      </p>
      <!--
        A main screen's title is set the way a masthead is: 27px, 800, tracked
        in. It is the one piece of type in the app that is allowed to be loud,
        and it is what makes a screen read as a page rather than as a bar with
        something under it. A pushed screen keeps a small one, because there the
        content is the point and the title is only saying where you are.
      -->
      <h1
        class="truncate"
        :class="backTo ? 'text-[17px] font-extrabold tracking-tight' : 'q2-title'"
        :data-q2-private="privateTitle ? '' : undefined"
      >
        {{ title }}
      </h1>
    </div>

    <div class="flex shrink-0 items-center gap-2">
      <slot name="actions" />
    </div>
  </header>
</template>
