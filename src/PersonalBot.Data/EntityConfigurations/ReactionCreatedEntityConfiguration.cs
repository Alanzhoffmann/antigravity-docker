using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Data.EntityConfigurations;

public class ReactionCreatedEntityConfiguration : IEntityTypeConfiguration<ReactionCreated>
{
    public void Configure(EntityTypeBuilder<ReactionCreated> builder)
    {
        builder.HasBaseType<Workflow>();

        builder.Property(rc => rc.ReactionContent).HasColumnName(nameof(ReactionCreated.ReactionContent));
        builder.Property(rc => rc.IssueNumber).HasColumnName(nameof(ReactionCreated.IssueNumber));
        builder.Property(rc => rc.RepoName).HasColumnName(nameof(ReactionCreated.RepoName));
        builder.Property(rc => rc.CloneUrl).HasColumnName(nameof(ReactionCreated.CloneUrl));
    }
}
