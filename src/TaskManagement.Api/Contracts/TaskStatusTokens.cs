using TaskManagement.Api.Data;

namespace TaskManagement.Api.Contracts;

internal static class TaskStatusTokens
{
    internal const string Pending = "pending";
    internal const string InProgress = "inProgress";
    internal const string Done = "done";

    internal static WorkTaskStatus? Parse(string? token) => token switch
    {
        Pending => WorkTaskStatus.Pending,
        InProgress => WorkTaskStatus.InProgress,
        Done => WorkTaskStatus.Done,
        _ => null
    };

    internal static string Format(WorkTaskStatus status) => status switch
    {
        WorkTaskStatus.Pending => Pending,
        WorkTaskStatus.InProgress => InProgress,
        WorkTaskStatus.Done => Done,
        _ => throw new ArgumentOutOfRangeException(nameof(status), "Unknown task status.")
    };
}
