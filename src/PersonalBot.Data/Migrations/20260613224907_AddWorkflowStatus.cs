using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalBot.Data.Migrations;

/// <inheritdoc />
public partial class _20260613224907_AddWorkflowStatus : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Output",
            table: "Workflows",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "RetryCount",
            table: "Workflows",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "Status",
            table: "Workflows",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Output",
            table: "Workflows");

        migrationBuilder.DropColumn(
            name: "RetryCount",
            table: "Workflows");

        migrationBuilder.DropColumn(
            name: "Status",
            table: "Workflows");
    }
}
