<script setup lang="ts">
/**
 * The page an invite link lands on.
 *
 * It says whose link it is and offers the step that turns it into a
 * friendship: an account for somebody who has none — the sign-up form carries
 * the code, and registering makes the friendship — or signing in and coming
 * back here to accept it.
 *
 * It used to show nothing: it put the code into state and redirected to the
 * sign-up form. Server-rendered, that redirect was a 302, a new request that
 * arrived without the state, and no link ever made a friendship. Now nothing
 * has to survive a redirect: the code travels in the address, to the sign-up
 * form as `?invite=` and back from the sign-in form as `?next=`. Sentry drops
 * query strings on both runtimes (`sentry.shared.ts`).
 */
definePageMeta({ layout: 'plain' })

const route = useRoute()
const t = useMessages()
const { isSignedIn } = useSession()

const code = String(route.params.code ?? '')

const { preview, error, isLoading, isAccepting, acceptFailure, refresh, accept } = useInviteLink(code)

const registerTo = `/register?invite=${encodeURIComponent(code)}`
const signInTo = `/login?next=${encodeURIComponent(`/join/${encodeURIComponent(code)}`)}`

async function onAccept() {
  const sender = await accept()

  // To their record with you, which is what a new friend is for. The toast
  // already said what happened.
  if (sender) {
    await navigateTo(`/people/${sender.id}`, { replace: true })
  }
}

useHead({ title: () => t.value.join.title })

/*
 * What a messenger shows when the link is pasted: a title, a sentence and the
 * app icon instead of a bare address. The preview names nobody — it sits in
 * the chat for everybody in it, and it is fetched by the messenger's servers.
 *
 * That is not a promise the name never reaches them: the page body is rendered
 * on the server and carries it, so a crawler reading past the tags can see it.
 * Keeping it out would mean reading the preview in the browser only, and a
 * loading flash on every open for somebody who was sent the link on purpose.
 *
 * `noindex`, because a page behind somebody's code is nothing a search engine
 * should keep.
 */
const origin = useRequestURL().origin

useSeoMeta({
  ogTitle: () => t.value.join.previewTitle,
  ogDescription: () => t.value.join.intro,
  ogImage: `${origin}/pwa-512x512.png`,
  ogType: 'website',
  ogSiteName: () => t.value.app.name,
  twitterCard: 'summary',
  robots: 'noindex, nofollow',
})
</script>

<template>
  <InviteWelcome
    :preview="preview"
    :failure="error"
    :is-loading="isLoading"
    :is-signed-in="isSignedIn"
    :is-accepting="isAccepting"
    :accept-failure="acceptFailure"
    :register-to="registerTo"
    :sign-in-to="signInTo"
    @accept="onAccept"
    @retry="refresh"
  />
</template>
