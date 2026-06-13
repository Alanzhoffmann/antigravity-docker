using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Data.EntityConfigurations;

public class TaskApprovedEntityConfiguration : IEntityTypeConfiguration<TaskApproved>
{
    public void Configure(EntityTypeBuilder<TaskApproved> builder)
    {
        builder.HasBaseType<Workflow>();

        builder.Property(t => t.SessionId).HasColumnName(nameof(TaskApproved.SessionId));
        builder.Property(t => t.IssueNumber).HasColumnName(nameof(TaskApproved.IssueNumber));
        builder.Property(t => t.RepoPath).HasColumnName(nameof(TaskApproved.RepoPath));
    }
}
