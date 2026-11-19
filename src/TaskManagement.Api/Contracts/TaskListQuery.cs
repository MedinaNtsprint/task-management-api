using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Api.Contracts;

public sealed class TaskListQuery : IValidatableObject
{
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Status { get; init; }

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [RegularExpression(@"[^\x00]*", ErrorMessage = "The {0} field must not contain a NUL character.")]
    public string? Assignee { get; init; }

    [Range(1, int.MaxValue)]
    public int? AfterId { get; init; }

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Status is not null && TaskStatusTokens.Parse(Status) is null)
            yield return new ValidationResult("Status must be pending, inProgress, or done.", [nameof(Status)]);
    }
}
