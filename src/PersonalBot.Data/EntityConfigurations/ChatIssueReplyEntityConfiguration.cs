using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Data.EntityConfigurations;

public class ChatIssueReplyEntityConfiguration : IEntityTypeConfiguration<ChatIssueReply>
{
    public void Configure(EntityTypeBuilder<ChatIssueReply> builder)
    {
        builder.HasBaseType<Workflow>();
    }
}
