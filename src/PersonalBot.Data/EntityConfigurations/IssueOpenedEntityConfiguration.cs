using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Data.EntityConfigurations;

public class IssueOpenedEntityConfiguration : IEntityTypeConfiguration<IssueOpened>
{
    public void Configure(EntityTypeBuilder<IssueOpened> builder)
    {
        builder.HasBaseType<Workflow>();

        builder.Property(i => i.IssueTitle).HasColumnName(nameof(IssueOpened.IssueTitle));
        builder.Property(i => i.IssueBody).HasColumnName(nameof(IssueOpened.IssueBody));
        builder.Property(i => i.RepoName).HasColumnName(nameof(IssueOpened.RepoName));
        builder.Property(i => i.CloneUrl).HasColumnName(nameof(IssueOpened.CloneUrl));
        builder.Property(i => i.IssueNumber).HasColumnName(nameof(IssueOpened.IssueNumber));
    }
}
