using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskManagement.Api.Data;

public sealed class TasksDbContextFactory : IDesignTimeDbContextFactory<TasksDbContext>
{
    public TasksDbContext CreateDbContext(string[] args)
    {
        // Migration generation only needs the provider; runtime supplies the connection.
        var options = new DbContextOptionsBuilder<TasksDbContext>()
            .UseNpgsql()
            .Options;

        return new TasksDbContext(options);
    }
}
