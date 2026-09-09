<script setup lang="ts">
import { imageUrl } from '~/api/images'

/**
 * Somebody's avatar: their photograph, or their initials on their own colour.
 *
 * The initials are not a placeholder that goes away. They are what is drawn
 * while the picture loads, when somebody has never uploaded one, when they
 * delete it, and when the request for it fails — so there is always something
 * to see, and a screen of empty grey circles is not a state this app has. The
 * colours are the one place identity is allowed to bring hue onto a black
 * screen, because telling two people apart is meaning rather than decoration.
 *
 * A group has an icon instead. It is drawn on a neutral raised tile rather than
 * a person colour: a group is not somebody, and giving it one would make the
 * chat list read as though it were.
 */
const props = withDefaults(defineProps<{
  initials: string
  color: string
  /** Their photograph, when they have one. */
  imageId?: string | null
  /** A group's avatar, as a bare Lucide name. Overrides the initials. */
  icon?: string | null
  /** Diameter in pixels. */
  size?: number
  /** Shows the presence dot when true.  */
  online?: boolean
  /** Overlaps the previous avatar, for the stacks on a shared goal. */
  stacked?: boolean
  /** Set only when no adjacent text already names this person. */
  label?: string
}>(), {
  imageId: null,
  icon: null,
  size: 40,
  online: false,
  stacked: false,
  label: undefined,
})

const { public: config } = useRuntimeConfig()

/*
 * A picture that failed to load is not tried again for as long as this avatar
 * is on screen. Without it, the `<img>` retries on every re-render and each
 * failure fires `error` again — a signed-out session would turn one broken
 * avatar into a request loop.
 */
const failed = ref(false)

watch(() => props.imageId, () => {
  failed.value = false
})

const source = computed(() =>
  !props.icon && props.imageId && !failed.value
    ? imageUrl(config.apiBaseUrl, props.imageId)
    : null)

const style = computed(() => ({
  width: `${props.size}px`,
  height: `${props.size}px`,
  fontSize: `${Math.round(props.size * 0.38)}px`,
  background: props.icon ? 'var(--ui-bg-elevated)' : props.color,
  color: props.icon ? 'var(--ui-text)' : '#ffffff',
}))
</script>

<template>
  <span
    class="relative inline-flex shrink-0"
    :class="stacked ? '-ms-2 rounded-full ring-2 ring-(--q2-surface) first:ms-0' : ''"
    data-q2-block
  >
    <span
      class="relative flex items-center justify-center overflow-hidden rounded-full font-extrabold leading-none select-none"
      :style="style"
      :aria-hidden="label ? undefined : 'true'"
      :aria-label="label"
      :role="label ? 'img' : undefined"
      data-testid="avatar"
    >
      <!--
        `use-credentials`, because the session is a cookie and an `<img>` on
        another origin sends none by default — without it every photograph is a
        401 and every avatar silently falls back to initials.

        The initials stay underneath rather than beside: the image covers them
        once it has loaded, so a slow connection shows a person rather than a
        hole. `alt` is empty because the picture says nothing the row does not
        already say in words, and `aria-label` on the parent covers the one
        place it stands alone.
      -->
      <img
        v-if="source"
        :src="source"
        alt=""
        crossorigin="use-credentials"
        decoding="async"
        loading="lazy"
        class="absolute inset-0 size-full object-cover"
        data-testid="avatar-image"
        @error="failed = true"
      >

      <UIcon
        v-if="icon"
        :name="groupIconName(icon)"
        :style="{ width: `${Math.round(size * 0.46)}px`, height: `${Math.round(size * 0.46)}px` }"
        aria-hidden="true"
      />
      <template v-else>{{ initials }}</template>
    </span>

    <!--
      Presence is decorative here: every place it appears, the row also says
      "Online" or when somebody was last active in words.
    -->
    <span
      v-if="online"
      class="absolute end-0 bottom-0 rounded-full ring-2 ring-(--ui-bg)"
      :style="{
        width: `${Math.max(9, Math.round(size * 0.26))}px`,
        height: `${Math.max(9, Math.round(size * 0.26))}px`,
        background: 'var(--q2-online)',
      }"
      aria-hidden="true"
    />
  </span>
</template>
