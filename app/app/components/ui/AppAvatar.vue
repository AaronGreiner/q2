<script setup lang="ts">
/**
 * Somebody's avatar: their initials on their own colour.
 *
 * There are no photographs in q2 and none are planned — an initial on a colour
 * identifies a friend well enough at 40 pixels, and it means no image upload,
 * no storage and no face to lose control of (docs/privacy.md).
 *
 * A group's "initials" are a single emoji. Those need more room than two
 * letters, and they carry their own colour, so they are drawn on the accent
 * tint instead of the person colour.
 */
const props = withDefaults(defineProps<{
  initials: string
  color: string
  /** Diameter in pixels. */
  size?: number
  /** Shows the presence dot when true.  */
  online?: boolean
  /** Overlaps the previous avatar, for the stacks on a shared goal. */
  stacked?: boolean
  /** Set only when no adjacent text already names this person. */
  label?: string
}>(), {
  size: 40,
  online: false,
  stacked: false,
  label: undefined,
})

const isEmoji = computed(() => /\p{Extended_Pictographic}/u.test(props.initials))

const style = computed(() => ({
  width: `${props.size}px`,
  height: `${props.size}px`,
  fontSize: `${Math.round(props.size * (isEmoji.value ? 0.52 : 0.38))}px`,
  background: isEmoji.value ? 'var(--q2-accent-soft)' : props.color,
  color: isEmoji.value ? 'var(--q2-accent-soft-text)' : '#ffffff',
}))
</script>

<template>
  <span
    class="relative inline-flex shrink-0"
    :class="stacked ? '-ms-2 rounded-full ring-2 ring-(--q2-surface) first:ms-0' : ''"
    data-q2-block
  >
    <span
      class="flex items-center justify-center rounded-full font-extrabold leading-none select-none"
      :style="style"
      :aria-hidden="label ? undefined : 'true'"
      :aria-label="label"
      :role="label ? 'img' : undefined"
      data-testid="avatar"
    >{{ initials }}</span>

    <!--
      Presence is decorative here: every place it appears, the row also says
      "Online" or when somebody was last active in words.
    -->
    <span
      v-if="online"
      class="absolute end-0 bottom-0 rounded-full ring-2 ring-(--ui-bg)"
      :style="{
        width: `${Math.max(9, Math.round(size * 0.26))}px`,
        height: `${Math.max(9, Math.round(size * 0.26))}px`,
        background: 'var(--q2-online)',
      }"
      aria-hidden="true"
    />
  </span>
</template>
