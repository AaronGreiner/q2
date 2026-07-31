<script setup lang="ts">
/**
 * The bottom navigation.
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
  { to: '/', icon: 'i-lucide-house', label: t.value.nav.home, badge: 0 },
  { to: '/goals', icon: 'i-lucide-target', label: t.value.nav.goals, badge: 0 },
  { to: '/chats', icon: 'i-lucide-message-circle', label: t.value.nav.chats, badge: 0 },
  { to: '/friends', icon: 'i-lucide-users', label: t.value.nav.friends, badge: 0 },
  { to: '/profile', icon: 'i-lucide-user', label: t.value.nav.profile, badge: 0 },
])

const route = useRoute()

/** `/` only matches itself; everything else matches its sub-routes too. */
function isCurrent(to: string): boolean {
  return to === '/' ? route.path === '/' : route.path.startsWith(to)
}

function badgeFor(to: string, unreadChats?: number, pendingRequests?: number): number {
  if (to === '/chats') return unreadChats ?? 0
  if (to === '/friends') return pendingRequests ?? 0
  return 0
}
</script>

<template>
  <nav
    class="flex border-t border-(--ui-border) bg-(--q2-surface) pt-2 pb-(--q2-safe-bottom)"
    :aria-label="t.nav.label"
    data-testid="bottom-nav"
  >
    <NuxtLink
      v-for="item in items"
      :key="item.to"
      :to="item.to"
      class="relative flex flex-1 flex-col items-center gap-1 py-1 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :class="isCurrent(item.to) ? 'text-(--ui-primary)' : 'text-(--ui-text-muted)'"
      :aria-current="isCurrent(item.to) ? 'page' : undefined"
      :data-testid="`nav-${item.to === '/' ? 'home' : item.to.slice(1)}`"
    >
      <UIcon
        :name="item.icon"
        class="size-[22px]"
        aria-hidden="true"
      />
      <span class="text-[10px] font-bold">{{ item.label }}</span>

      <span
        v-if="badgeFor(item.to, unreadChats, pendingRequests) > 0"
        class="absolute top-0 start-1/2 ms-2 flex h-4 min-w-4 items-center justify-center rounded-full bg-(--q2-accent-solid) px-1 text-[9px] font-extrabold text-white"
      >
        {{ badgeFor(item.to, unreadChats, pendingRequests) }}
      </span>
    </NuxtLink>
  </nav>
</template>
