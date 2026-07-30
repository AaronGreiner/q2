<script setup lang="ts">
/**
 * UApp provides the overlay/toast context Nuxt UI components expect and must
 * wrap the whole application.
 */
useHead({
  titleTemplate: title => (title ? `${title} · Kudos` : 'Kudos (q2)'),
  htmlAttrs: { lang: 'en' },
  meta: [
    { name: 'description', content: 'Kudos (q2) — pursue self-care goals together with friends.' },
    { name: 'color-scheme', content: 'light dark' },
  ],
})
</script>

<template>
  <UApp>
    <NuxtLayout>
      <NuxtPage />
    </NuxtLayout>
  </UApp>
</template>
