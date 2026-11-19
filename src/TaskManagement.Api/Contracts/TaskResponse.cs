namespace TaskManagement.Api.Contracts;

public sealed record TaskResponse(int Id, string Title, string Description, string Status, string Assignee,
    DateOnly DueDate);
