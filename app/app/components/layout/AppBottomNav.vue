<script setup lang="ts">
/**
 * The bottom navigation.
 *
 * Five destinations, and the one in the middle is your To-Dos: what is due
 * today and the goals behind it. It used to be a create button that opened the
 * goals screen with its sheet already up, which made "Neu" the only visible way
 * to reach your own goals — people went looking for them under a plus. So the
 * middle is a place now, and creating one is the plus on that screen, the only
 * place a goal is made from.
 *
 * What went to make room for the middle, back when it was the create button,
 * is the friends tab — friends are found where you search for them, and the
 * requests waiting for you are at the top of that screen, so the badge moved
 * with them rather than disappearing.
 *
 * Real links rather than buttons over a tab index: each screen has its own URL,
 * so the browser's back button, a deep link and a Capacitor hardware back
 * button all behave the way people expect without any of them being
 * implemented here.
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
  { to: '/goals', key: 'todos', icon: 'i-lucide-list-checks', label: t.value.nav.todos },
  { to: '/chats', key: 'chats', icon: 'i-lucide-message-circle', label: t.value.nav.chats },
  { to: '/profile', key: 'profile', icon: 'i-lucide-user', label: t.value.nav.profile },
])

const route = useRoute()
const currentSlot = computed(() => items.value.findIndex(item => isCurrent(item.to)))

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
    class="relative flex items-stretch border-t border-(--ui-border) bg-(--q2-surface) pt-2 pb-(--q2-safe-bottom)"
    :aria-label="t.nav.label"
    data-testid="bottom-nav"
  >
    <span
      class="q2-nav-indicator"
      :style="{ '--q2-nav-index': Math.max(0, currentSlot), '--q2-nav-visible': currentSlot < 0 ? 0 : 1 }"
      aria-hidden="true"
    />
    <NuxtLink
      v-for="item in items"
      :key="item.key"
      :to="item.to"
      class="q2-press relative flex flex-1 flex-col items-center gap-1 py-1 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :class="isCurrent(item.to) ? 'text-(--ui-text)' : 'text-(--ui-text-dimmed)'"
      :aria-current="isCurrent(item.to) ? 'page' : undefined"
      :data-testid="`nav-${item.key}`"
    >
      <UIcon
        :name="item.icon"
        class="q2-nav-icon size-[22px]"
        aria-hidden="true"
      />
      <span class="text-[10px] font-bold">{{ item.label }}</span>

      <span
        v-if="badgeFor(item.key, unreadChats, pendingRequests) > 0"
        class="absolute top-0 start-1/2 ms-2 flex h-4 min-w-4 items-center justify-center rounded-full bg-(--q2-accent-solid) px-1 text-[9px] font-extrabold text-(--q2-accent-contrast)"
        data-q2-block
      >
        {{ badgeFor(item.key, unreadChats, pendingRequests) }}
      </span>
    </NuxtLink>
  </nav>
</template>
