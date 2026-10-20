<script setup lang="ts">
import type { Image, Person } from '~/api/types'

/**
 * Editing your own profile: the sheet, and the camera it hands over to.
 *
 * A sheet and a camera, and never both at once. Editing owns the name and the
 * picture; taking the picture is its own full-screen camera. They are siblings
 * rather than nested: overlays stacked on overlays have deadlocked in this app
 * before — see `PhotoCapture` — and a camera over a half-hidden form does not
 * fit on a phone.
 *
 * A component of its own because two screens open it: the pencil on the
 * profile, and "Profil bearbeiten" in settings (#92). Saving stays with the
 * screen, which owns the profile it was read with.
 */
defineProps<{
  person: Person
  bio: string | null
  submitting: boolean
}>()

const emit = defineEmits<{
  save: [value: { displayName: string, bio: string }]
  photo: [image: Image]
  remove: []
}>()

const open = defineModel<boolean>('open', { required: true })

const capturing = ref(false)

function onCapture() {
  open.value = false
  capturing.value = true
}

/**
 * Brings the edit sheet back once the camera is gone, whether a picture was
 * taken or the sheet was simply dismissed. Watching the flag rather than
 * handling the two cases separately is what makes "swiped it away" behave like
 * "pressed cancel" without a third path to forget.
 */
watch(capturing, (isOpen) => {
  if (!isOpen) open.value = true
})

function onPhoto(image: Image) {
  capturing.value = false
  emit('photo', image)
}
</script>

<template>
  <ProfileEditSheet
    v-model:open="open"
    :person="person"
    :bio="bio"
    :submitting="submitting"
    @save="value => emit('save', value)"
    @capture="onCapture"
    @remove="emit('remove')"
  />

  <PhotoCapture
    v-model:open="capturing"
    purpose="Avatar"
    @uploaded="onPhoto"
  />
</template>
