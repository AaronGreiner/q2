<script setup lang="ts">
/**
 * A password field whose typing can be shown, and hidden again.
 *
 * On a phone keyboard a mistyped character is the usual reason a sign-in
 * fails, and a field that only ever shows dots gives no way to find it (#114).
 * Every password field in q2 is this one, so the button looks the same and is
 * named the same wherever a password is asked for.
 *
 * Attributes — `autocomplete`, `placeholder`, `size`, `data-testid` — reach the
 * field itself rather than a wrapper, which is where password managers and the
 * tests look for them. Revealed, the field still must not be "corrected" by the
 * keyboard, hence the three attributes that switch that off.
 */
defineOptions({ inheritAttrs: false })

const model = defineModel<string>({ required: true })

const t = useMessages()
const isRevealed = ref(false)
</script>

<template>
  <UInput
    v-bind="$attrs"
    v-model="model"
    :type="isRevealed ? 'text' : 'password'"
    autocapitalize="none"
    autocorrect="off"
    spellcheck="false"
    :ui="{ trailing: 'pe-0' }"
  >
    <template #trailing>
      <!-- One name with aria-pressed, rather than a label that flips: a
           toggle that renames itself reads as two different buttons. -->
      <button
        type="button"
        class="flex size-11 items-center justify-center rounded-(--q2-radius-md) text-(--ui-text-muted) hover:text-(--ui-text) focus-visible:outline-2 focus-visible:outline-(--ui-primary)"
        :aria-label="t.auth.showPassword"
        :aria-pressed="isRevealed"
        data-testid="password-reveal"
        @click="isRevealed = !isRevealed"
      >
        <UIcon
          :name="isRevealed ? 'i-lucide-eye-off' : 'i-lucide-eye'"
          class="size-5"
          aria-hidden="true"
        />
      </button>
    </template>
  </UInput>
</template>
