using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalBot.Data.Migrations;

/// <inheritdoc />
public partial class _20260619140154_RemoveRepoPath : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RepoPath",
            table: "Workflows");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RepoPath",
            table: "Workflows",
            type: "TEXT",
            nullable: true);
    }
}
