export default defineAppConfig({
  ui: {
    colors: { neutral: 'stone' },
    button: {
      slots: { base: 'q2-press rounded-(--q2-radius-md) shadow-none' },
      compoundVariants: [
        {
          color: 'primary',
          variant: 'solid',
          class: 'bg-(--q2-accent-solid) text-(--q2-accent-contrast) hover:bg-(--q2-accent-solid)/90 disabled:bg-transparent disabled:text-(--ui-text-dimmed) disabled:ring disabled:ring-(--ui-border)',
        },
        {
          color: 'primary',
          variant: 'soft',
          class: 'bg-(--q2-accent-soft) text-(--q2-accent-soft-text) hover:bg-(--q2-accent-soft)/80',
        },
      ],
    },
    input: { slots: { base: 'min-h-11 rounded-(--q2-radius-md) bg-(--ui-bg-muted) shadow-none' } },
    textarea: { slots: { base: 'rounded-(--q2-radius-md) bg-(--ui-bg-muted) shadow-none' } },
    card: { slots: { root: 'shadow-none divide-none' } },
  },
})
