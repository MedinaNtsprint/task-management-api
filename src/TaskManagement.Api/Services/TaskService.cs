using Microsoft.EntityFrameworkCore;
using TaskManagement.Api.Contracts;
using TaskManagement.Api.Data;

namespace TaskManagement.Api.Services;

internal sealed class TaskService(TasksDbContext database) : ITaskService
{
    public async Task<TaskResponse> CreateAsync(CreateTaskRequest input, CancellationToken cancellationToken)
    {
        var task = ToEntity(input);
        database.Tasks.Add(task);
        await database.SaveChangesAsync(cancellationToken);
        return ToResponse(task);
    }

    public async Task<Result<TaskResponse>> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var task = await database.Tasks.AsNoTracking().SingleOrDefaultAsync(task => task.Id == id, cancellationToken);
        return task is null ? Result<TaskResponse>.NotFound : Result<TaskResponse>.Success(ToResponse(task));
    }

    public async Task<Result<TaskResponse>> UpdateAsync(int id, UpdateTaskRequest input,
        CancellationToken cancellationToken)
    {
        var replacement = ToEntity(input);
        var updated = await database.Tasks.Where(task => task.Id == id).ExecuteUpdateAsync(setters => setters
            .SetProperty(task => task.Title, replacement.Title)
            .SetProperty(task => task.Description, replacement.Description)
            .SetProperty(task => task.Status, replacement.Status)
            .SetProperty(task => task.Assignee, replacement.Assignee)
            .SetProperty(task => task.DueDate, replacement.DueDate), cancellationToken);
        if (updated == 0)
            return Result<TaskResponse>.NotFound;

        replacement.Id = id;
        return Result<TaskResponse>.Success(ToResponse(replacement));
    }

    public async Task<Result<int>> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var deleted = await database.Tasks.Where(task => task.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? Result<int>.NotFound : Result<int>.Success(id);
    }

    // Input has already passed controller validation.
    private static WorkTask ToEntity(TaskInput input) => new()
    {
        Title = input.Title!.Trim(),
        Description = input.Description!.Trim(),
        Status = TaskStatusTokens.Parse(input.Status)!.Value,
        Assignee = input.Assignee!.Trim(),
        DueDate = input.DueDate!.Value
    };

    private static TaskResponse ToResponse(WorkTask task) => new(task.Id, task.Title, task.Description,
        TaskStatusTokens.Format(task.Status), task.Assignee, task.DueDate);
}
