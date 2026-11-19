using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Tasks_Assignee_Id",
                table: "Tasks",
                columns: new[] { "Assignee", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_Status_Id",
                table: "Tasks",
                columns: new[] { "Status", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tasks_Assignee_Id",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_Status_Id",
                table: "Tasks");
        }
    }
}
