<script setup lang="ts">
/**
 * The application frame: skip link, header, content, footer.
 *
 * Outside production the current environment is shown in the header, so a
 * screenshot or a bug report is never ambiguous about which data it came from.
 */
const { public: config } = useRuntimeConfig()

const isProduction = computed(() => config.appEnv === 'production')
const diagnosticsEnabled = computed(() => Boolean(config.diagnosticsEnabled))
</script>

<template>
  <div class="min-h-screen bg-(--ui-bg) text-(--ui-text)">
    <!-- First tab stop: lets keyboard users jump past the header. -->
    <a
      href="#main"
      class="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-50 focus:rounded-(--ui-radius) focus:bg-(--ui-bg-elevated) focus:px-4 focus:py-2 focus:ring-2 focus:ring-(--ui-primary)"
    >
      Skip to content
    </a>

    <header class="border-b border-(--ui-border) bg-(--ui-bg-elevated)/60">
      <div class="mx-auto flex max-w-6xl flex-wrap items-center gap-3 px-4 py-3 sm:px-6">
        <NuxtLink
          to="/"
          class="flex items-center gap-2 font-semibold"
        >
          <UIcon
            name="i-lucide-sparkles"
            class="size-5 text-(--ui-primary)"
            aria-hidden="true"
          />
          <span>Kudos</span>
          <span class="text-(--ui-text-muted) font-normal">q2</span>
        </NuxtLink>

        <UBadge
          v-if="!isProduction"
          color="neutral"
          variant="subtle"
          size="sm"
          data-testid="environment-badge"
        >
          {{ config.appEnv }}
        </UBadge>

        <nav
          class="ms-auto flex items-center gap-1"
          aria-label="Main"
        >
          <UButton
            to="/"
            variant="ghost"
            color="neutral"
            size="sm"
          >
            Goals
          </UButton>
          <UButton
            v-if="diagnosticsEnabled"
            to="/diagnostics"
            variant="ghost"
            color="neutral"
            size="sm"
            data-testid="nav-diagnostics"
          >
            Diagnostics
          </UButton>
        </nav>
      </div>
    </header>

    <main
      id="main"
      class="mx-auto max-w-6xl px-4 py-8 sm:px-6"
    >
      <slot />
    </main>

    <!--
      Muted, not dimmed: `--ui-text-dimmed` is 2.63:1 against the background
      and fails WCAG AA. Only purely decorative elements may use it.
    -->
    <footer class="mx-auto max-w-6xl px-4 pb-8 text-sm text-(--ui-text-muted) sm:px-6">
      <p>Kudos (q2) — pursue self-care goals together. Reference build.</p>
    </footer>
  </div>
</template>
