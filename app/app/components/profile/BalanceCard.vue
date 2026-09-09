<script setup lang="ts">
import type { Balance } from '~/api/types'

/**
 * "47 geschafft · 5 verpasst".
 *
 * The record, and the thing q2 could not previously say. The two numbers are
 * always shown together because neither means anything alone: "47" is a boast,
 * and "47 · 5" is a record.
 *
 * The second number is grey rather than red. A miss is a fact, not an alarm —
 * red in this product means something final and immediate, and a count of past
 * windows is neither. Colouring it would be the first step towards the card
 * becoming a punishment, which is the one thing it must not be
 * ("kein Nachtreten").
 *
 * On somebody else's profile it covers only the goals the two of them share.
 * That scoping is the server's, and `sharedGoals` is what lets this card tell
 * "nothing in common" apart from "a clean record" — two very different
 * sentences that would otherwise both read as "0 · 0".
 */
const props = withDefaults(defineProps<{
  balance: Balance
  /** How many goals the two people share. Omit on your own profile. */
  sharedGoals?: number | null
}>(), {
  sharedGoals: null,
})

const t = useMessages()

const hasNothingShared = computed(() => props.sharedGoals === 0)
</script>

<template>
  <section
    class="q2-card px-3.5 py-3.5"
    aria-labelledby="balance-heading"
    data-testid="balance-card"
    data-q2-private
  >
    <h3
      id="balance-heading"
      class="text-[11px] font-bold tracking-wide text-(--ui-text-muted) uppercase"
    >
      {{ t.balance.heading }}
    </h3>

    <template v-if="hasNothingShared">
      <p class="mt-2 text-sm font-bold">
        {{ t.balance.nothingShared }}
      </p>
      <p class="mt-1 text-[12px] font-semibold text-(--ui-text-muted)">
        {{ t.balance.nothingSharedHint }}
      </p>
    </template>

    <template v-else>
      <dl class="mt-2 flex items-baseline gap-4">
        <div class="flex items-baseline gap-1.5">
          <dd class="text-[26px] leading-none font-extrabold">
            {{ balance.done }}
          </dd>
          <dt class="text-[12px] font-bold text-(--ui-text-muted)">
            {{ t.balance.done }}
          </dt>
        </div>

        <span
          class="text-(--ui-text-dimmed)"
          aria-hidden="true"
        >·</span>

        <div class="flex items-baseline gap-1.5">
          <dd
            class="text-[26px] leading-none font-extrabold text-(--ui-text-muted)"
            data-testid="balance-missed"
          >
            {{ balance.missed }}
          </dd>
          <dt class="text-[12px] font-bold text-(--ui-text-muted)">
            {{ t.balance.missed }}
          </dt>
        </div>
      </dl>

      <p
        v-if="balance.missed === 0"
        class="mt-1.5 text-[12px] font-semibold text-(--ui-text-dimmed)"
      >
        {{ t.balance.clean }}
      </p>

      <p
        v-if="sharedGoals !== null"
        class="mt-1.5 text-[12px] font-semibold text-(--ui-text-dimmed)"
        data-testid="balance-scope"
      >
        {{ t.balance.sharedGoals(sharedGoals) }}
      </p>
    </template>
  </section>
</template>
