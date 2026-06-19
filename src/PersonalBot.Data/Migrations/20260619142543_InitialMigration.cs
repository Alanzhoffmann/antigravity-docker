using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalBot.Data.Migrations;

/// <inheritdoc />
public partial class _20260619142543_InitialMigration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Workflows",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                Output = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                ParentWorkflowId = table.Column<Guid>(type: "TEXT", nullable: true),
                Discriminator = table.Column<string>(type: "TEXT", maxLength: 21, nullable: false),
                RepoName = table.Column<string>(type: "TEXT", nullable: true),
                IssueNumber = table.Column<string>(type: "TEXT", nullable: true),
                Prompt = table.Column<string>(type: "TEXT", nullable: true),
                AgentPhase = table.Column<int>(type: "INTEGER", nullable: true),
                ChatOutput = table.Column<string>(type: "TEXT", nullable: true),
                Session = table.Column<string>(type: "TEXT", nullable: true),
                ArtifactOutput = table.Column<string>(type: "TEXT", nullable: true),
                AgentName = table.Column<string>(type: "TEXT", nullable: true),
                CommentBody = table.Column<string>(type: "TEXT", nullable: true),
                CloneUrl = table.Column<string>(type: "TEXT", nullable: true),
                IssueTitle = table.Column<string>(type: "TEXT", nullable: true),
                IssueBody = table.Column<string>(type: "TEXT", nullable: true),
                PrMerged = table.Column<bool>(type: "INTEGER", nullable: true),
                HeadRef = table.Column<string>(type: "TEXT", nullable: true),
                ReactionContent = table.Column<string>(type: "TEXT", nullable: true),
                EventType = table.Column<string>(type: "TEXT", nullable: true),
                DeliveryId = table.Column<string>(type: "TEXT", nullable: true),
                RawBody = table.Column<string>(type: "TEXT", nullable: true),
                Action = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Workflows", x => x.Id);
                table.ForeignKey(
                    name: "FK_Workflows_Workflows_ParentWorkflowId",
                    column: x => x.ParentWorkflowId,
                    principalTable: "Workflows",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateIndex(
            name: "IX_Workflows_ParentWorkflowId",
            table: "Workflows",
            column: "ParentWorkflowId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Workflows");
    }
}
