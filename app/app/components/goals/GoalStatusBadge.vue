<script setup lang="ts">
import type { GoalStatus } from '~/api/types'

/**
 * Status as a badge. Colour is never the only signal — the label and icon
 * carry the same information for anyone who cannot distinguish them.
 */
const props = defineProps<{
  status: GoalStatus
  overdue?: boolean
}>()

const presentation = computed(() => goalStatusPresentation(props.status))
</script>

<template>
  <div class="flex flex-wrap items-center gap-1.5">
    <UBadge
      :color="presentation.color"
      :icon="presentation.icon"
      variant="subtle"
      size="sm"
    >
      {{ presentation.label }}
    </UBadge>

    <UBadge
      v-if="overdue"
      color="warning"
      icon="i-lucide-clock-alert"
      variant="subtle"
      size="sm"
    >
      Overdue
    </UBadge>
  </div>
</template>
