using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class RiskWarnings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "RiskNotifiedAt",
            table: "GoalInstances",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RiskNotifiedAt",
            table: "GoalInstances");
    }
}
