using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaskManagement.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'4', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Assignee = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => x.Id);
                    table.CheckConstraint("CK_Tasks_Assignee_NotBlank", "\"Assignee\" ~ '[^[:space:]]'");
                    table.CheckConstraint("CK_Tasks_Description_NotBlank", "\"Description\" ~ '[^[:space:]]'");
                    table.CheckConstraint("CK_Tasks_Status", "\"Status\" IN (1, 2, 3)");
                    table.CheckConstraint("CK_Tasks_Title_NotBlank", "\"Title\" ~ '[^[:space:]]'");
                });

            migrationBuilder.InsertData(
                table: "Tasks",
                columns: new[] { "Id", "Assignee", "Description", "DueDate", "Status", "Title" },
                values: new object[,]
                {
                    { 1, "Alex", "Summarize the changes customers will see in the next release.", new DateOnly(2030, 1, 15), 1, "Draft the release notes" },
                    { 2, "Morgan", "Check that the setup instructions work from a fresh clone.", new DateOnly(2030, 1, 12), 2, "Review the onboarding guide" },
                    { 3, "Alex", "Document the team's checklist for approving a release.", new DateOnly(2030, 1, 10), 3, "Agree on release criteria" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tasks");
        }
    }
}
