<script setup lang="ts">
import { PULL_THRESHOLD } from '~/utils/pullToRefresh'

/**
 * The circle that comes down with a pull (#19): it turns as it is pulled, is
 * whole once letting go would refresh, and spins while the screen is read
 * again.
 *
 * Grey, not the accent: it shows a state — how far, and whether it is
 * working — not something to press. Turning with the finger is direct
 * manipulation and stays under reduced motion; the spin and the spring back
 * are motion, and do not.
 */
const props = defineProps<{
  distance: number
  armed: boolean
  refreshing: boolean
}>()

const t = useMessages()

const isFollowing = computed(() => props.distance > 0 && !props.refreshing)

const style = computed(() => ({
  transform: `translateY(${props.distance - 44}px) rotate(${Math.round((props.distance / PULL_THRESHOLD) * 270)}deg)`,
  opacity: props.refreshing ? 1 : Math.min(1, props.distance / PULL_THRESHOLD),
}))
</script>

<template>
  <div
    class="pointer-events-none relative z-10 flex h-0 justify-center"
    data-testid="pull-indicator"
    :data-armed="armed ? '' : undefined"
    :data-refreshing="refreshing ? '' : undefined"
  >
    <!-- Follows the finger without delay; springs back once it is let go. -->
    <span
      class="absolute top-0 flex size-9 items-center justify-center rounded-full bg-(--ui-bg-elevated) text-(--ui-text) shadow-md ring ring-(--ui-border)"
      :class="isFollowing ? '' : 'motion-safe:transition-[transform,opacity] motion-safe:duration-(--q2-motion-normal)'"
      :style="style"
      aria-hidden="true"
    >
      <UIcon
        name="i-lucide-refresh-cw"
        class="size-4"
        :class="[refreshing ? 'motion-safe:animate-spin' : '', armed || refreshing ? 'opacity-100' : 'opacity-60']"
      />
    </span>

    <span
      class="sr-only"
      role="status"
    >{{ refreshing ? t.common.refreshing : '' }}</span>
  </div>
</template>
