using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Data.EntityConfigurations;

public class WebhookReceivedEntityConfiguration : IEntityTypeConfiguration<WebhookReceived>
{
    public void Configure(EntityTypeBuilder<WebhookReceived> builder)
    {
        builder.HasBaseType<Workflow>();

        builder.Property(w => w.EventType).HasColumnName(nameof(WebhookReceived.EventType));
        builder.Property(w => w.DeliveryId).HasColumnName(nameof(WebhookReceived.DeliveryId));
        builder.Property(w => w.RepoName).HasColumnName(nameof(WebhookReceived.RepoName));
        builder.Property(w => w.RawBody).HasColumnName(nameof(WebhookReceived.RawBody));
        builder.Property(w => w.CloneUrl).HasColumnName(nameof(WebhookReceived.CloneUrl));
        builder.Property(w => w.CommentBody).HasColumnName(nameof(WebhookReceived.CommentBody));
        builder.Property(w => w.HeadRef).HasColumnName(nameof(WebhookReceived.HeadRef));
        builder.Property(w => w.IssueBody).HasColumnName(nameof(WebhookReceived.IssueBody));
        builder.Property(w => w.IssueNumber).HasColumnName(nameof(WebhookReceived.IssueNumber));
        builder.Property(w => w.IssueTitle).HasColumnName(nameof(WebhookReceived.IssueTitle));
        builder.Property(w => w.PrMerged).HasColumnName(nameof(WebhookReceived.PrMerged));
        builder.Property(w => w.ReactionContent).HasColumnName(nameof(WebhookReceived.ReactionContent));
    }
}
