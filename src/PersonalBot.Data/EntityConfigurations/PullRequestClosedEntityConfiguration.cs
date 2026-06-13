using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Data.EntityConfigurations;

public class PullRequestClosedEntityConfiguration : IEntityTypeConfiguration<PullRequestClosed>
{
    public void Configure(EntityTypeBuilder<PullRequestClosed> builder)
    {
        builder.HasBaseType<Workflow>();

        builder.Property(p => p.PrMerged).HasColumnName(nameof(PullRequestClosed.PrMerged));
        builder.Property(p => p.HeadRef).HasColumnName(nameof(PullRequestClosed.HeadRef));
        builder.Property(p => p.RepoName).HasColumnName(nameof(PullRequestClosed.RepoName));
    }
}
