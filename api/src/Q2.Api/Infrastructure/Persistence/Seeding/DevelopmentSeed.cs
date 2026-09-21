namespace Q2.Api.Infrastructure.Persistence.Seeding;

/// <summary>
/// A working q2 so a fresh checkout shows the real thing rather than an empty
/// shell — every screen has something on it.
/// </summary>
/// <remarks>
/// Only inserted into an empty Development database, and never re-inserted on
/// later starts — your local data is yours.
/// </remarks>
public sealed class DevelopmentSeed : ISeedDataSource
{
    public SeedProfile Profile => SeedProfile.Development;

    public string Description => "The demonstration world: 10 people, 8 shared goals with a conversation each, 4 free conversations.";

    public SeedData Create(SeedContext context)
    {
        var build = new SeedBuilder(Profile, context);
        KudosWorld.Compose(build);
        return build.Build();
    }
}
