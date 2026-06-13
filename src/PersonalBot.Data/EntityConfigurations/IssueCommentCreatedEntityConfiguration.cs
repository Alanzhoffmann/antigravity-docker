using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Data.EntityConfigurations;

public class IssueCommentCreatedEntityConfiguration : IEntityTypeConfiguration<IssueCommentCreated>
{
    public void Configure(EntityTypeBuilder<IssueCommentCreated> builder)
    {
        builder.HasBaseType<Workflow>();

        builder.Property(i => i.IssueNumber).HasColumnName(nameof(IssueCommentCreated.IssueNumber));
        builder.Property(i => i.CommentBody).HasColumnName(nameof(IssueCommentCreated.CommentBody));
        builder.Property(i => i.RepoName).HasColumnName(nameof(IssueCommentCreated.RepoName));
        builder.Property(i => i.CloneUrl).HasColumnName(nameof(IssueCommentCreated.CloneUrl));
    }
}
