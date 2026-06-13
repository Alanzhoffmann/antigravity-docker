using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalBot.Data.Migrations;

/// <inheritdoc />
public partial class _20260613222850_InitialMigration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AiTasks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                RepoPath = table.Column<string>(type: "TEXT", nullable: false),
                IssueNum = table.Column<string>(type: "TEXT", nullable: false),
                Prompt = table.Column<string>(type: "TEXT", nullable: false),
                Phase = table.Column<int>(type: "INTEGER", nullable: false),
                Session = table.Column<string>(type: "TEXT", nullable: true),
                Agent = table.Column<string>(type: "TEXT", nullable: true),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                RetryCount = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AiTasks", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Webhooks",
            columns: table => new
            {
                DeliveryId = table.Column<string>(type: "TEXT", nullable: false),
                EventType = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                RawBody = table.Column<string>(type: "TEXT", nullable: false),
                RepoName = table.Column<string>(type: "TEXT", nullable: false),
                CloneUrl = table.Column<string>(type: "TEXT", nullable: true),
                Action = table.Column<string>(type: "TEXT", nullable: true),
                IssueNumber = table.Column<string>(type: "TEXT", nullable: true),
                IssueTitle = table.Column<string>(type: "TEXT", nullable: true),
                IssueBody = table.Column<string>(type: "TEXT", nullable: true),
                ReactionContent = table.Column<string>(type: "TEXT", nullable: true),
                CommentBody = table.Column<string>(type: "TEXT", nullable: true),
                PrMerged = table.Column<bool>(type: "INTEGER", nullable: false),
                HeadRef = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Webhooks", x => x.DeliveryId);
            });

        migrationBuilder.CreateTable(
            name: "Workflows",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Discriminator = table.Column<string>(type: "TEXT", maxLength: 21, nullable: false),
                IssueNumber = table.Column<string>(type: "TEXT", nullable: true),
                CommentBody = table.Column<string>(type: "TEXT", nullable: true),
                RepoName = table.Column<string>(type: "TEXT", nullable: true),
                CloneUrl = table.Column<string>(type: "TEXT", nullable: true),
                IssueTitle = table.Column<string>(type: "TEXT", nullable: true),
                IssueBody = table.Column<string>(type: "TEXT", nullable: true),
                PrMerged = table.Column<bool>(type: "INTEGER", nullable: true),
                HeadRef = table.Column<string>(type: "TEXT", nullable: true),
                ReactionContent = table.Column<string>(type: "TEXT", nullable: true),
                SessionId = table.Column<string>(type: "TEXT", nullable: true),
                RepoPath = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Workflows", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AiTasks");

        migrationBuilder.DropTable(
            name: "Webhooks");

        migrationBuilder.DropTable(
            name: "Workflows");
    }
}
