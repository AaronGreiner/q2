namespace Q2.Api.Features.Goals;

/// <summary>
/// What kind of thing a goal is about (#18).
/// </summary>
/// <remarks>
/// Five, and closed: a choice that tells q2 something, where a free icon only
/// decorated a card. A badge can be earned with a kind of goal — "zehn Fenster
/// bei einem Lesen-Ziel" (<c>BadgeRules</c>) — which twelve icons could never
/// say. Stored as text, like every enum in q2.
/// </remarks>
public enum GoalCategory
{
    Sport,
    Learning,
    Reading,
    Mindfulness,
    Other,
}

/// <summary>
/// The icon each category is drawn with, and what a goal without one is.
/// </summary>
/// <remarks>
/// The icon is derived, never chosen: a goal's card, its tile and its
/// conversation's avatar all show its category's. The names are bare Lucide
/// names, as every icon in the contract is, and the frontend bundles exactly
/// these (app/nuxt.config.ts) — adding a category means adding its icon there
/// in the same change.
/// </remarks>
public static class GoalCategories
{
    public const GoalCategory Default = GoalCategory.Other;

    public static string IconFor(GoalCategory category) => category switch
    {
        GoalCategory.Sport => "dumbbell",
        GoalCategory.Learning => "graduation-cap",
        GoalCategory.Reading => "book-open",
        GoalCategory.Mindfulness => "leaf",
        _ => "target",
    };
}
