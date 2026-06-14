using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalBot.Data.Migrations;

/// <inheritdoc />
public partial class _20260614211009_AddWebhookReceivedWorkflow : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Webhooks");

        migrationBuilder.AddColumn<string>(
            name: "Action",
            table: "Workflows",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            table: "Workflows",
            type: "TEXT",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

        migrationBuilder.AddColumn<string>(
            name: "DeliveryId",
            table: "Workflows",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EventType",
            table: "Workflows",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RawBody",
            table: "Workflows",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Action",
            table: "Workflows");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            table: "Workflows");

        migrationBuilder.DropColumn(
            name: "DeliveryId",
            table: "Workflows");

        migrationBuilder.DropColumn(
            name: "EventType",
            table: "Workflows");

        migrationBuilder.DropColumn(
            name: "RawBody",
            table: "Workflows");

        migrationBuilder.CreateTable(
            name: "Webhooks",
            columns: table => new
            {
                DeliveryId = table.Column<string>(type: "TEXT", nullable: false),
                Action = table.Column<string>(type: "TEXT", nullable: true),
                CloneUrl = table.Column<string>(type: "TEXT", nullable: true),
                CommentBody = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                EventType = table.Column<string>(type: "TEXT", nullable: false),
                HeadRef = table.Column<string>(type: "TEXT", nullable: true),
                IssueBody = table.Column<string>(type: "TEXT", nullable: true),
                IssueNumber = table.Column<string>(type: "TEXT", nullable: true),
                IssueTitle = table.Column<string>(type: "TEXT", nullable: true),
                PrMerged = table.Column<bool>(type: "INTEGER", nullable: false),
                RawBody = table.Column<string>(type: "TEXT", nullable: false),
                ReactionContent = table.Column<string>(type: "TEXT", nullable: true),
                RepoName = table.Column<string>(type: "TEXT", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Webhooks", x => x.DeliveryId);
            });
    }
}
