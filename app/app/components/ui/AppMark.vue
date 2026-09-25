<script setup lang="ts">
/**
 * The q2 mark, drawn from the same outline as the app icon (`utils/q2Mark.ts`).
 *
 * It takes the text colour it sits in, so it is neutral in both themes: the
 * mark is the name, not something that can be done, and the accent is kept for
 * that (ADR 0015). Size it by height; the width follows the mark's proportion.
 * Decorative wherever it appears next to the written name, hence `aria-hidden`.
 */
const viewBox = `0 0 ${q2MarkWidth} ${q2MarkHeight}`
const path = q2MarkPath
</script>

<template>
  <svg
    :viewBox="viewBox"
    class="block w-auto"
    fill="currentColor"
    aria-hidden="true"
    focusable="false"
    data-testid="q2-mark"
  >
    <path
      fill-rule="evenodd"
      :d="path"
    />
  </svg>
</template>
