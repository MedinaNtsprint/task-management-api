using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Api.Contracts;

public abstract class TaskInput
{
    private const string NoNul = @"[^\x00]*";
    private const string NoNulMessage = "The {0} field must not contain a NUL character.";

    [Required, StringLength(120), RegularExpression(NoNul, ErrorMessage = NoNulMessage)]
    public string? Title { get; init; }

    [Required, StringLength(2000), RegularExpression(NoNul, ErrorMessage = NoNulMessage)]
    public string? Description { get; init; }

    [Required, AllowedValues(TaskStatusTokens.Pending, TaskStatusTokens.InProgress, TaskStatusTokens.Done,
        ErrorMessage = "Status must be pending, inProgress, or done.")]
    public string? Status { get; init; }

    [Required, StringLength(100), RegularExpression(NoNul, ErrorMessage = NoNulMessage)]
    public string? Assignee { get; init; }

    [Required]
    public DateOnly? DueDate { get; init; }
}

public sealed class CreateTaskRequest : TaskInput { }

public sealed class UpdateTaskRequest : TaskInput { }
