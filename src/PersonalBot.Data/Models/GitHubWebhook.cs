using System.Text.Json;
using PersonalBot.Utils;

namespace PersonalBot.Data.Models;

public class GitHubWebhook
{
    public required string EventType { get; set; }
    public required string DeliveryId { get; set; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public WebhookStatus Status { get; set; } = WebhookStatus.Pending;
    public required string RawBody
    {
        get;
        set
        {
            field = value;
            using var document = JsonDocument.Parse(field);
            Action = document.RootElement.GetStringSafe("action");
            CloneUrl = document.RootElement.GetNestedStringSafe("repository", "clone_url");
            IssueNumber = document.RootElement.GetNestedStringSafe("issue", "number");
            IssueTitle = document.RootElement.GetNestedStringSafe("issue", "title");
            IssueBody = document.RootElement.GetNestedStringSafe("issue", "body");
            ReactionContent = document.RootElement.GetNestedStringSafe("reaction", "content");
            CommentBody = document.RootElement.GetNestedStringSafe("comment", "body");
            PrMerged =
                document.RootElement.TryGetProperty("pull_request", out var pr)
                && pr.TryGetProperty("merged", out var m)
                && m.GetBoolean();
            HeadRef = document.RootElement.GetNestedStringSafe("pull_request", "head", "ref");
        }
    }
    public required string RepoName { get; set; }
    public string? CloneUrl { get; private set; }
    public string? Action { get; private set; }
    public string? IssueNumber { get; private set; }
    public string? IssueTitle { get; private set; }
    public string? IssueBody { get; private set; }
    public string? ReactionContent { get; private set; }
    public string? CommentBody { get; private set; }
    public bool PrMerged { get; private set; }
    public string? HeadRef { get; private set; }

    public override string ToString() =>
        $"event='{EventType}' action='{Action}' delivery='{DeliveryId}' repo='{RepoName}'";
}
