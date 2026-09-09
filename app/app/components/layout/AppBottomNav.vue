<script setup lang="ts">
/**
 * The bottom navigation.
 *
 * Five slots, and the middle one is the create button rather than a fifth
 * destination. That is the shape this product wants: making a commitment is the
 * thing people come here to do, and it belongs under the thumb rather than
 * behind a header button on one particular screen.
 *
 * What went to make room for it is the friends tab — friends are now found
 * where you search for them, and the requests waiting for you are at the top of
 * that screen, so the badge moved with them rather than disappearing.
 *
 * Real links rather than buttons over a tab index: each screen has its own URL,
 * so the browser's back button, a deep link and a Capacitor hardware back
 * button all behave the way people expect without any of them being
 * implemented here. The create button is a link for the same reason — it opens
 * the goals screen with its sheet already up, which means the sheet survives a
 * reload and closes with the back gesture.
 *
 * The unread counts come from the pages that already load them. Fetching them
 * here would mean the navigation making its own requests on every screen.
 */
defineProps<{
  unreadChats?: number
  pendingRequests?: number
}>()

const t = useMessages()

const items = computed(() => [
  { to: '/', key: 'home', icon: 'i-lucide-house', label: t.value.nav.home },
  { to: '/search', key: 'search', icon: 'i-lucide-search', label: t.value.nav.search },
  { to: '/chats', key: 'chats', icon: 'i-lucide-message-circle', label: t.value.nav.chats },
  { to: '/profile', key: 'profile', icon: 'i-lucide-user', label: t.value.nav.profile },
])

const route = useRoute()

/** `/` only matches itself; everything else matches its sub-routes too. */
function isCurrent(to: string): boolean {
  return to === '/' ? route.path === '/' : route.path.startsWith(to)
}

function badgeFor(key: string, unreadChats?: number, pendingRequests?: number): number {
  if (key === 'chats') return unreadChats ?? 0
  if (key === 'search') return pendingRequests ?? 0
  return 0
}
</script>

<template>
  <nav
    class="flex items-stretch border-t border-(--ui-border) bg-(--ui-bg) pt-2 pb-(--q2-safe-bottom)"
    :aria-label="t.nav.label"
    data-testid="bottom-nav"
  >
    <template
      v-for="(item, index) in items"
      :key="item.key"
    >
      <!-- The create button sits between the second and third destination, so
           the four others stay two-and-two either side of it. -->
      <NuxtLink
        v-if="index === 2"
        to="/goals?create=1"
        class="flex flex-1 flex-col items-center justify-start py-1 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
        :aria-label="t.create.open"
        data-testid="nav-create"
      >
        <span class="flex size-[38px] items-center justify-center rounded-full bg-(--q2-accent-solid) text-(--q2-accent-contrast)">
          <UIcon
            name="i-lucide-plus"
            class="size-[22px]"
            aria-hidden="true"
          />
        </span>
        <span class="mt-0.5 text-[10px] font-bold text-(--ui-text-dimmed)">{{ t.nav.create }}</span>
      </NuxtLink>

      <NuxtLink
        :to="item.to"
        class="relative flex flex-1 flex-col items-center gap-1 py-1 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
        :class="isCurrent(item.to) ? 'text-(--ui-text)' : 'text-(--ui-text-dimmed)'"
        :aria-current="isCurrent(item.to) ? 'page' : undefined"
        :data-testid="`nav-${item.key}`"
      >
        <UIcon
          :name="item.icon"
          class="size-[22px]"
          aria-hidden="true"
        />
        <span class="text-[10px] font-bold">{{ item.label }}</span>

        <!-- The current tab is marked by a bar rather than by the accent: which
             screen you are on is a state, and the accent is reserved for
             something you can do. -->
        <span
          v-if="isCurrent(item.to)"
          class="absolute -top-2 h-[3px] w-7 rounded-full bg-(--q2-accent-solid)"
          aria-hidden="true"
        />

        <span
          v-if="badgeFor(item.key, unreadChats, pendingRequests) > 0"
          class="absolute top-0 start-1/2 ms-2 flex h-4 min-w-4 items-center justify-center rounded-full bg-(--q2-accent-solid) px-1 text-[9px] font-extrabold text-(--q2-accent-contrast)"
          data-q2-block
        >
          {{ badgeFor(item.key, unreadChats, pendingRequests) }}
        </span>
      </NuxtLink>
    </template>
  </nav>
</template>
