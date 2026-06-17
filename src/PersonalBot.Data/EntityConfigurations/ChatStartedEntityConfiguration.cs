using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Data.EntityConfigurations;

public class ChatStartedEntityConfiguration : IEntityTypeConfiguration<ChatStarted>
{
    public void Configure(EntityTypeBuilder<ChatStarted> builder)
    {
        builder.HasBaseType<Workflow>();

        builder.Property(c => c.RepoPath).HasColumnName(nameof(ChatStarted.RepoPath));
        builder.Property(c => c.IssueNumber).HasColumnName(nameof(ChatStarted.IssueNumber));
        builder.Property(c => c.Prompt).HasColumnName(nameof(ChatStarted.Prompt));
        builder.Property(c => c.AgentPhase).HasColumnName(nameof(ChatStarted.AgentPhase));
        builder.Property(c => c.ChatOutput).HasColumnName(nameof(ChatStarted.ChatOutput));
        builder.Property(c => c.Session).HasColumnName(nameof(ChatStarted.Session));
        builder.Property(c => c.ArtifactOutput).HasColumnName(nameof(ChatStarted.ArtifactOutput));
        builder.Property(c => c.AgentName).HasColumnName(nameof(ChatStarted.AgentName));
    }
}
