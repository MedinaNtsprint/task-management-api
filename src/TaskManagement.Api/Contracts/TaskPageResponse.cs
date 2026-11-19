namespace TaskManagement.Api.Contracts;

public sealed record TaskPageResponse(IReadOnlyList<TaskResponse> Items, int? NextAfterId);
