using Microsoft.EntityFrameworkCore;

namespace TaskManagement.Api.Data;

public sealed class TasksDbContext(DbContextOptions<TasksDbContext> options) : DbContext(options)
{
    public DbSet<WorkTask> Tasks => Set<WorkTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var task = modelBuilder.Entity<WorkTask>();
        task.ToTable("Tasks", table =>
        {
            table.HasCheckConstraint("CK_Tasks_Status",
                $"\"Status\" IN ({(int)WorkTaskStatus.Pending}, {(int)WorkTaskStatus.InProgress}, {(int)WorkTaskStatus.Done})");
            table.HasCheckConstraint("CK_Tasks_Title_NotBlank", "\"Title\" ~ '[^[:space:]]'");
            table.HasCheckConstraint("CK_Tasks_Description_NotBlank", "\"Description\" ~ '[^[:space:]]'");
            table.HasCheckConstraint("CK_Tasks_Assignee_NotBlank", "\"Assignee\" ~ '[^[:space:]]'");
        });

        // Keep generated keys above the IDs used by the demo data below.
        task.Property(item => item.Id).HasIdentityOptions(startValue: 4);
        task.Property(item => item.Title).IsRequired().HasMaxLength(120);
        task.Property(item => item.Description).IsRequired().HasMaxLength(2000);
        task.Property(item => item.Assignee).IsRequired().HasMaxLength(100);
        task.HasIndex(item => new { item.Status, item.Id });
        task.HasIndex(item => new { item.Assignee, item.Id });

        task.HasData(
            new WorkTask
            {
                Id = 1,
                Title = "Draft the release notes",
                Description = "Summarize the changes customers will see in the next release.",
                Status = WorkTaskStatus.Pending,
                Assignee = "Alex",
                DueDate = new DateOnly(2030, 1, 15)
            },
            new WorkTask
            {
                Id = 2,
                Title = "Review the onboarding guide",
                Description = "Check that the setup instructions work from a fresh clone.",
                Status = WorkTaskStatus.InProgress,
                Assignee = "Morgan",
                DueDate = new DateOnly(2030, 1, 12)
            },
            new WorkTask
            {
                Id = 3,
                Title = "Agree on release criteria",
                Description = "Document the team's checklist for approving a release.",
                Status = WorkTaskStatus.Done,
                Assignee = "Alex",
                DueDate = new DateOnly(2030, 1, 10)
            });
    }
}
