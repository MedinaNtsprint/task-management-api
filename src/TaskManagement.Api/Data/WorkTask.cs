namespace TaskManagement.Api.Data;

public sealed class WorkTask
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required WorkTaskStatus Status { get; set; }
    public required string Assignee { get; set; }
    public required DateOnly DueDate { get; set; }
}
