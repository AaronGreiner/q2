<script setup lang="ts">
/**
 * The page an invite link lands on.
 *
 * It shows nothing and keeps nothing: the code goes into the state the sign-up
 * form reads, and the visitor is sent straight on. A landing page with a
 * paragraph of marketing on it would be a second thing to read before the one
 * thing they came here to do.
 *
 * Somebody already signed in is sent home instead. A link makes its friendship
 * *at registration* and the server offers no way to redeem one afterwards, so a
 * screen that promised otherwise would be a button that fails.
 */
definePageMeta({ layout: 'plain' })

const route = useRoute()
const { isSignedIn } = useSession()
const pendingInvite = usePendingInvite()

const code = String(route.params.code ?? '')

if (code) {
  pendingInvite.value = code
}

await navigateTo(isSignedIn.value ? '/' : '/register', { replace: true })
</script>

<template>
  <div />
</template>
