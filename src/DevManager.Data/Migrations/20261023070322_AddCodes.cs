using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "JobTitles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Categories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobTitles_Code",
                table: "JobTitles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Code",
                table: "Categories",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JobTitles_Code",
                table: "JobTitles");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Code",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "JobTitles");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Categories");
        }
    }
}
