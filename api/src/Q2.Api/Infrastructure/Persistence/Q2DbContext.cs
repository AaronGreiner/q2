using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Q2.Api.Features.Accounts;
using Q2.Api.Features.Activity;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Goals;
using Q2.Api.Features.People;
using Q2.Api.Features.Settings;

namespace Q2.Api.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context for q2.
/// </summary>
/// <remarks>
/// Entity configuration lives next to each feature (see
/// <see cref="GoalConfiguration"/>) and is picked up by assembly scanning, so
/// adding a feature does not mean editing this file — only adding the sets a
/// service needs to reach.
///
/// The base type is <see cref="IdentityUserContext{TUser,TKey}"/> rather than
/// <c>IdentityDbContext</c>: q2 has no roles, and the four tables that come
/// with users are already more than it uses. Roles would be three more tables
/// nothing queries (docs/adr/0011-authentication-with-identity.md).
/// </remarks>
public sealed class Q2DbContext(DbContextOptions<Q2DbContext> options)
    : IdentityUserContext<AppUser, Guid>(options)
{
    public DbSet<Person> People => Set<Person>();

    public DbSet<DailyCheckIn> DailyCheckIns => Set<DailyCheckIn>();

    public DbSet<PersonBadge> PersonBadges => Set<PersonBadge>();

    public DbSet<Friendship> Friendships => Set<Friendship>();

    public DbSet<Goal> Goals => Set<Goal>();

    public DbSet<GoalParticipant> GoalParticipants => Set<GoalParticipant>();

    public DbSet<GoalContribution> GoalContributions => Set<GoalContribution>();

    public DbSet<GoalTask> GoalTasks => Set<GoalTask>();

    public DbSet<ActivityEvent> ActivityEvents => Set<ActivityEvent>();

    public DbSet<ActivityKudos> ActivityKudos => Set<ActivityKudos>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();

    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    public DbSet<MessageReaction> MessageReactions => Set<MessageReaction>();

    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // First, not last: the base call is what puts Identity's own entity
        // types into the model, and both the feature configurations below and
        // the key rule after them have to be able to see them.
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Q2DbContext).Assembly);

        /*
         * Identifiers are always supplied by IIdGenerator or by a seed, never
         * by the database (AGENTS.md section 4).
         *
         * Saying so is not redundant. EF Core's default for a GUID key is
         * ValueGeneratedOnAdd, and that default changes how a *new* entity
         * found inside a tracked aggregate is classified: a key that already
         * has a value reads as "this row exists already", so the insert is
         * silently downgraded — a check-in added through Person.CheckIn or a
         * kudos added through ActivityEvent.GiveKudos produced either an UPDATE
         * against a row that was never there, or no statement at all. Nothing
         * failed loudly; a streak simply never grew.
         *
         * Applied here rather than in fifteen configurations because it is one
         * rule about this whole model, and the fifteenth is exactly the one
         * somebody would forget. It covers Identity's user key too, which is
         * why the base call above has to come first: an account id is handed
         * out by IIdGenerator or by a seed like every other id here.
         */
        foreach (var key in modelBuilder.Model.GetEntityTypes().Select(entity => entity.FindPrimaryKey()).OfType<IMutableKey>())
        {
            foreach (var property in key.Properties.Where(property => property.ClrType == typeof(Guid)))
            {
                property.ValueGenerated = ValueGenerated.Never;
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}
