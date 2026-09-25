<script setup lang="ts">
/**
 * Photographs or neutral initials keep identity readable without competing accents.
 *
 * With `expandTitle` set, a photograph opens full screen on a tap. It is
 * opt-in rather than everywhere because most avatars sit inside a row that is
 * already a link, and a button inside a link is two targets under one thumb.
 * Initials never expand — there is nothing bigger to show.
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
  /** Whose picture it is; setting it lets the photograph open full screen. */
  expandTitle?: string | null
}>(), {
  imageId: null,
  icon: null,
  size: 40,
  online: false,
  stacked: false,
  label: undefined,
  expandTitle: null,
})

const t = useMessages()
const picture = useImageSource(() => (props.icon ? null : props.imageId))

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
    ? picture.value
    : null)

const viewer = usePhotoViewer()

const isExpandable = computed(() => Boolean(props.expandTitle && source.value))

function expand() {
  if (!props.imageId || !props.expandTitle) return

  viewer.open({ imageId: props.imageId, title: props.expandTitle })
}

const style = computed(() => ({
  width: `${props.size}px`,
  height: `${props.size}px`,
  fontSize: `${Math.round(props.size * 0.38)}px`,
  background: 'var(--q2-avatar)',
  color: 'var(--ui-text-toned)',
}))
</script>

<template>
  <span
    class="relative inline-flex shrink-0"
    :class="stacked ? '-ms-2 rounded-full ring-2 ring-(--q2-surface) first:ms-0' : ''"
    data-q2-block
  >
    <component
      :is="isExpandable ? 'button' : 'span'"
      :type="isExpandable ? 'button' : undefined"
      class="relative flex items-center justify-center overflow-hidden rounded-full font-extrabold leading-none select-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :style="style"
      :aria-hidden="label || isExpandable ? undefined : 'true'"
      :aria-label="isExpandable ? t.viewer.avatar(expandTitle ?? '') : label"
      :role="label && !isExpandable ? 'img' : undefined"
      data-testid="avatar"
      @click="isExpandable ? expand() : undefined"
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
        crossorigin="use-credentials"
        :src="source"
        alt=""
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
    </component>

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
