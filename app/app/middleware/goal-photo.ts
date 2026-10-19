/**
 * A notification about one of a goal's photographs arrives at the goal with
 * `?photo=` (#112): it knows the goal, not the conversation. The photograph is
 * shown and voted on in the goal's conversation
 * (docs/adr/0027-goal-conversations.md), so when the goal has one the address
 * is handed on before the goal's screen draws anything — on the server as a
 * redirect, in the app as a replaced navigation, so back leads to wherever the
 * notification was tapped.
 *
 * A goal with no conversation stays where it is and shows the photograph full
 * screen (`pages/goals/[id].vue`). A read that fails is left to the page, which
 * reads the goal again and says what went wrong in its own words.
 */
export default defineNuxtRouteMiddleware(async (to) => {
  const photo = typeof to.query.photo === 'string' ? to.query.photo : null
  if (!photo) return

  try {
    const goal = await useQ2Api().goals.get(String(to.params.id))

    if (goal.conversationId) {
      return navigateTo({ path: `/chats/${goal.conversationId}`, query: { photo } }, { replace: true })
    }
  }
  catch {
    // The page reads the goal itself, and reports and words a failure there.
  }
})
