<script setup lang="ts">
import type { Balance, SharedGoal, Week } from '~/api/types'

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
 * sentences that would otherwise both read as "0 · 0". It names each of those
 * goals and opens it (#86): "1 gemeinsames Do" with no way to it left the
 * reader to guess which one the record was about.
 *
 * On your own profile it also says this week (#27): the windows of your own
 * goals that end between Monday and Sunday, done, missed and still open — all
 * grey, because they are states, not something to press (ADR 0015).
 */
const props = withDefaults(defineProps<{
  balance: Balance
  /** The goals the two people share, as the server scoped them. Omit on your own profile. */
  sharedGoals?: readonly SharedGoal[] | null
  /** This week, on your own profile only (#27). */
  week?: Week | null
}>(), {
  sharedGoals: null,
  week: null,
})

const t = useMessages()

const hasNothingShared = computed(() => props.sharedGoals?.length === 0)

/** "3 geschafft · 1 verpasst · 2 offen", and a relapse only when there is one. */
const weekLine = computed(() => {
  const week = props.week
  if (!week) return ''

  return [
    `${week.done} ${t.value.balance.done}`,
    `${week.missed} ${t.value.balance.missed}`,
    `${week.open} ${t.value.balance.open}`,
    ...(week.relapsed > 0 ? [t.value.balance.relapsed(week.relapsed)] : []),
  ].join(' · ')
})
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

      <!-- A relapse is its own number and never one of the misses
           (docs/adr/0037-donts.md); only said when there is one. -->
      <p
        v-if="balance.relapsed > 0"
        class="mt-1.5 text-[12px] font-semibold text-(--ui-text-muted)"
        data-testid="balance-relapsed"
      >
        {{ t.balance.relapsed(balance.relapsed) }}
      </p>

      <p
        v-if="balance.missed === 0"
        class="mt-1.5 text-[12px] font-semibold text-(--ui-text-dimmed)"
      >
        {{ t.balance.clean }}
      </p>

      <div
        v-if="week"
        class="mt-3 border-t border-(--ui-border-muted) pt-3"
        data-testid="balance-week"
      >
        <p class="text-[11px] font-bold tracking-wide text-(--ui-text-muted) uppercase">
          {{ t.balance.thisWeek }}
        </p>
        <p class="mt-1 text-sm font-bold">
          {{ weekLine }}
        </p>
      </div>

      <template v-if="sharedGoals !== null">
        <p
          id="balance-scope"
          class="mt-3 text-[12px] font-semibold text-(--ui-text-dimmed)"
          data-testid="balance-scope"
        >
          {{ t.balance.sharedGoals(sharedGoals.length) }}
        </p>

        <!-- Ended goals stay: they are still counted above. -->
        <ul
          class="mt-1 flex flex-col"
          aria-labelledby="balance-scope"
        >
          <li
            v-for="goal in sharedGoals"
            :key="goal.id"
          >
            <NuxtLink
              :to="`/goals/${goal.id}`"
              class="-mx-1.5 flex min-h-11 items-center gap-2.5 rounded-(--q2-radius-md) px-1.5 py-1 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
              data-testid="balance-goal"
            >
              <span
                class="flex size-8 shrink-0 items-center justify-center rounded-(--q2-radius-sm) bg-(--ui-bg-accented) text-(--ui-text)"
                aria-hidden="true"
              >
                <UIcon
                  :name="goalIconName(goal.icon)"
                  class="size-4"
                />
              </span>
              <span class="min-w-0 flex-1">
                <span class="block truncate text-sm font-bold">{{ goal.title }}</span>
                <span
                  v-if="goal.status !== 'Active'"
                  class="block text-[11px] font-semibold text-(--ui-text-muted)"
                >{{ t.status[goal.status] }}</span>
              </span>
              <UIcon
                name="i-lucide-chevron-right"
                class="size-4 shrink-0 text-(--ui-text-dimmed)"
                aria-hidden="true"
              />
            </NuxtLink>
          </li>
        </ul>
      </template>
    </template>
  </section>
</template>
