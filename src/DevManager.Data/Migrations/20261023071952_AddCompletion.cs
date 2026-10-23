using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Requirements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletedModules",
                table: "Requirements",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Requirements");

            migrationBuilder.DropColumn(
                name: "CompletedModules",
                table: "Requirements");
        }
    }
}
