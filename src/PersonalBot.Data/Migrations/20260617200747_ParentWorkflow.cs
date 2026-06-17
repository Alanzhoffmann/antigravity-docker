using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalBot.Data.Migrations;

/// <inheritdoc />
public partial class _20260617200747_ParentWorkflow : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ParentWorkflowId",
            table: "Workflows",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Workflows_ParentWorkflowId",
            table: "Workflows",
            column: "ParentWorkflowId");

        migrationBuilder.AddForeignKey(
            name: "FK_Workflows_Workflows_ParentWorkflowId",
            table: "Workflows",
            column: "ParentWorkflowId",
            principalTable: "Workflows",
            principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Workflows_Workflows_ParentWorkflowId",
            table: "Workflows");

        migrationBuilder.DropIndex(
            name: "IX_Workflows_ParentWorkflowId",
            table: "Workflows");

        migrationBuilder.DropColumn(
            name: "ParentWorkflowId",
            table: "Workflows");
    }
}
