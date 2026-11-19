using TaskManagement.Api.Contracts;

namespace TaskManagement.Api.Services;

public interface ITaskService
{
    Task<TaskResponse> CreateAsync(CreateTaskRequest input, CancellationToken cancellationToken);
    Task<Result<TaskResponse>> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<Result<TaskResponse>> UpdateAsync(int id, UpdateTaskRequest input, CancellationToken cancellationToken);
    Task<Result<int>> DeleteAsync(int id, CancellationToken cancellationToken);
}
