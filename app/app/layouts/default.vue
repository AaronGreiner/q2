<script setup lang="ts">
/**
 * The application frame for the five main screens: a phone-shaped column with
 * the navigation pinned to the bottom.
 *
 * `h-dvh` with an inner scroll region rather than a page that scrolls: this is
 * what makes the bottom bar stay put under a thumb instead of sliding away, and
 * `dvh` rather than `vh` is what keeps it above the browser chrome when that
 * chrome hides on scroll.
 *
 * On a wide screen the column is centred at phone width. q2 is a phone
 * application that currently happens to run in a browser (app/AGENTS.md
 * section 8); stretching these layouts across a desktop would be designing a
 * second product.
 */
const t = useMessages()

// Shared with the profile screen under the same `useAsyncData` key, so the
// badges cost no extra request. Pages that change the counts refresh it.
const { profile } = useProfile()
</script>

<template>
  <div class="mx-auto flex h-dvh w-full max-w-[430px] flex-col overflow-hidden bg-(--ui-bg) text-(--ui-text)">
    <!-- First tab stop: lets keyboard users jump past the header of each screen. -->
    <a
      href="#main"
      class="sr-only focus:not-sr-only focus:absolute focus:start-4 focus:top-4 focus:z-50 focus:rounded-(--q2-radius-lg) focus:bg-(--ui-bg-elevated) focus:px-4 focus:py-2 focus:ring-2 focus:ring-(--ui-primary)"
    >
      {{ t.app.skipToContent }}
    </a>

    <main
      id="main"
      class="flex min-h-0 flex-1 flex-col pt-[max(0.5rem,env(safe-area-inset-top))]"
    >
      <slot />
    </main>

    <AppBottomNav
      :unread-chats="profile?.unreadChats ?? 0"
      :pending-requests="profile?.pendingFriendRequests ?? 0"
    />
  </div>
</template>
