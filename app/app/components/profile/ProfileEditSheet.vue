<script setup lang="ts">
import type { Person } from '~/api/types'

/**
 * Changing your own name and picture.
 *
 * Two fields, and one of them is a photograph — which is why this is a sheet
 * rather than a screen: everything it changes is visible behind it, and the
 * result is the point.
 *
 * The handle is deliberately absent and says so. It is the string somebody's
 * friends searched for and wrote down; letting a rename move it would break the
 * one part of a profile other people rely on.
 *
 * Note the two different kinds of change here. The name is a draft that is
 * saved on "Speichern"; the picture is not — an upload has already happened by
 * the time it comes back, so it is applied at once. Pretending otherwise would
 * mean either an orphaned image on cancel or a sheet that cannot show what was
 * just taken.
 *
 * Taking the picture is *not* done from here. This sheet only asks for it, and
 * the screen answers by closing this one and opening the camera — because two
 * `UDrawer`s open at once deadlock, and there is no room for a sheet on a sheet
 * at 390 x 844 anyway. See `PhotoCapture`.
 */
const props = defineProps<{
  person: Person
  submitting: boolean
}>()

const emit = defineEmits<{
  save: [value: { displayName: string }]
  capture: []
  remove: []
}>()

const open = defineModel<boolean>('open', { required: true })

const t = useMessages()

const name = ref(props.person.displayName)
const confirmingRemoval = ref(false)

// The sheet is kept mounted between openings, so the draft has to be put back
// to what is actually stored each time — otherwise a cancelled rename is still
// sitting in the field the next time it opens.
watch(open, (isOpen) => {
  if (isOpen) name.value = props.person.displayName
})

watch(() => props.person.displayName, (updated) => {
  if (!open.value) name.value = updated
})

const trimmed = computed(() => name.value.trim())
const canSave = computed(() => trimmed.value.length > 0 && !props.submitting)

function onSave() {
  if (!canSave.value) return
  emit('save', { displayName: trimmed.value })
}

function onRemove() {
  confirmingRemoval.value = false
  emit('remove')
}
</script>

<template>
  <UDrawer
    v-model:open="open"
    :title="t.profileEdit.heading"
    :description="t.profileEdit.subtitle"
    :ui="{ container: 'max-w-[430px] mx-auto' }"
  >
    <template #body>
      <div class="flex flex-col gap-5 pb-2">
        <section
          class="flex flex-col items-center gap-3"
          :aria-label="t.profileEdit.photo"
        >
          <AppAvatar
            :initials="person.initials"
            :color="person.avatarColor"
            :image-id="person.avatarImageId"
            :size="96"
          />

          <p
            v-if="!person.avatarImageId"
            class="text-center text-[13px] font-semibold text-(--ui-text-muted)"
          >
            {{ t.profileEdit.photoNone }}
          </p>

          <div class="flex flex-wrap justify-center gap-2">
            <UButton
              size="lg"
              color="neutral"
              variant="outline"
              icon="i-lucide-camera"
              :label="person.avatarImageId ? t.profileEdit.changePhoto : t.profileEdit.addPhoto"
              :disabled="submitting"
              data-testid="profile-photo-change"
              @click="emit('capture')"
            />

            <UButton
              v-if="person.avatarImageId"
              size="lg"
              color="error"
              variant="ghost"
              icon="i-lucide-trash-2"
              :label="t.profileEdit.removePhoto"
              :disabled="submitting"
              data-testid="profile-photo-remove"
              @click="confirmingRemoval = true"
            />
          </div>
        </section>

        <UFormField
          :label="t.profileEdit.name"
          :error="trimmed.length === 0 ? t.profileEdit.nameRequired : undefined"
        >
          <UInput
            v-model="name"
            size="xl"
            :placeholder="t.profileEdit.namePlaceholder"
            :maxlength="80"
            autocomplete="name"
            class="w-full"
            data-testid="profile-name"
          />
        </UFormField>

        <p class="text-[13px] font-semibold text-(--ui-text-muted)">
          {{ t.profileEdit.handleFixed }}
        </p>

        <UButton
          block
          size="xl"
          icon="i-lucide-check"
          :label="t.profileEdit.save"
          :disabled="!canSave"
          :loading="submitting"
          data-testid="profile-save"
          @click="onSave"
        />
      </div>
    </template>
  </UDrawer>

  <AppConfirmDialog
    v-model:open="confirmingRemoval"
    :title="t.profileEdit.removeTitle"
    :description="t.profileEdit.removeBody"
    :confirm-label="t.profileEdit.remove"
    @confirm="onRemove"
  />
</template>
