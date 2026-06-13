using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalBot.Data.Models;

namespace PersonalBot.Data.EntityConfigurations;

internal class GitHubWebhookEntityConfiguration : IEntityTypeConfiguration<GitHubWebhook>
{
    public void Configure(EntityTypeBuilder<GitHubWebhook> builder)
    {
        builder.HasKey(g => g.DeliveryId);
        builder.Property(g => g.CreatedAt);
    }
}
