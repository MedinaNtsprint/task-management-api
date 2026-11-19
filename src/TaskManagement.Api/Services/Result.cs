using System.Diagnostics.CodeAnalysis;

namespace TaskManagement.Api.Services;

public readonly struct Result<T> where T : notnull
{
    private Result(T value)
    {
        Value = value;
        IsSuccess = true;
    }

    public T? Value { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess { get; }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> NotFound => default;
}
