using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalBot.Data.Migrations;

/// <inheritdoc />
public partial class _20260617203427_ChatStartedWorkflow : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AgentName",
            table: "Workflows",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "AgentPhase",
            table: "Workflows",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ArtifactOutput",
            table: "Workflows",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ChatOutput",
            table: "Workflows",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Prompt",
            table: "Workflows",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Session",
            table: "Workflows",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AgentName",
            table: "Workflows");

        migrationBuilder.DropColumn(
            name: "AgentPhase",
            table: "Workflows");

        migrationBuilder.DropColumn(
            name: "ArtifactOutput",
            table: "Workflows");

        migrationBuilder.DropColumn(
            name: "ChatOutput",
            table: "Workflows");

        migrationBuilder.DropColumn(
            name: "Prompt",
            table: "Workflows");

        migrationBuilder.DropColumn(
            name: "Session",
            table: "Workflows");
    }
}
