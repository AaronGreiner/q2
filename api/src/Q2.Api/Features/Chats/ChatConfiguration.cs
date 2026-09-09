using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Q2.Api.Features.Goals;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Chats;

/// <summary>EF Core mapping for conversations and their messages.</summary>
public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Kind)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Property(c => c.Title)
            .HasMaxLength(Conversation.MaxTitleLength);

        builder.Property(c => c.Icon)
            .HasMaxLength(Conversation.MaxIconLength);

        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        // A pinned goal is a reference, not ownership: deleting the goal leaves
        // the conversation standing, it just stops showing the progress bar.
        builder.HasOne<Goal>()
            .WithMany()
            .HasForeignKey(c => c.GoalId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Participants)
            .WithOne()
            .HasForeignKey(p => p.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Messages)
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Conversation.Participants))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Conversation.Messages))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ConversationParticipantConfiguration : IEntityTypeConfiguration<ConversationParticipant>
{
    public void Configure(EntityTypeBuilder<ConversationParticipant> builder)
    {
        builder.ToTable("ConversationParticipants");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.LastReadAt)
            .HasConversion(InstantConversion.Optional);

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(p => p.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.ConversationId, p.PersonId }).IsUnique();
    }
}

public sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Text)
            .IsRequired()
            .HasMaxLength(ChatMessage.MaxTextLength);

        builder.Property(m => m.SentAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(m => m.SenderPersonId)
            .OnDelete(DeleteBehavior.Cascade);

        // The thread is always read in order, and the list screen wants the
        // newest message per conversation.
        builder.HasIndex(m => new { m.ConversationId, m.SentAt });

        builder.HasMany(m => m.Reactions)
            .WithOne()
            .HasForeignKey(r => r.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(ChatMessage.Reactions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class MessageReactionConfiguration : IEntityTypeConfiguration<MessageReaction>
{
    public void Configure(EntityTypeBuilder<MessageReaction> builder)
    {
        builder.ToTable("MessageReactions");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Kind)
            .IsRequired()
            .HasMaxLength(16)
            .HasConversion<string>();

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(r => r.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.MessageId, r.PersonId, r.Kind }).IsUnique();
    }
}
