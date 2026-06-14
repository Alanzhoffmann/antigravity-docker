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
    }
}
