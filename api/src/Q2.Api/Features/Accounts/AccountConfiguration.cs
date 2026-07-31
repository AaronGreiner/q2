using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Q2.Api.Features.People;

namespace Q2.Api.Features.Accounts;

/// <summary>
/// EF Core mapping for the account, on top of what Identity already configures.
/// </summary>
/// <remarks>
/// The table names stay Identity's own — <c>AspNetUsers</c> and the three that
/// come with it. They are the framework's tables, not q2's model, and renaming
/// them would turn every future Identity upgrade into a migration review. The
/// tables q2 owns are named q2's way; these are on loan.
/// </remarks>
public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.Property(u => u.Email).HasMaxLength(AccountPolicy.MaximumEmailLength);
        builder.Property(u => u.NormalizedEmail).HasMaxLength(AccountPolicy.MaximumEmailLength);
        builder.Property(u => u.UserName).HasMaxLength(AccountPolicy.MaximumEmailLength);
        builder.Property(u => u.NormalizedUserName).HasMaxLength(AccountPolicy.MaximumEmailLength);

        builder.Property(u => u.PersonId).IsRequired();

        // One account per person, enforced by the database rather than by
        // whoever remembers to check: two accounts signing in as the same
        // person would give one of them somebody else's chats.
        builder.HasIndex(u => u.PersonId).IsUnique();

        // Restrict, not Cascade: deleting a person out from under a live
        // account should fail loudly. Erasure is a deliberate flow that removes
        // both, in order — see docs/privacy.md.
        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(u => u.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
