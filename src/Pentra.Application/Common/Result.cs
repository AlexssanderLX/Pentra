namespace Pentra.Application.Common;

/// <summary>
/// Minimal result type for operations that can fail with a validation/domain
/// message, avoiding exceptions for expected failures.
/// </summary>
public class Result
{
    protected Result(bool succeeded, IReadOnlyList<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors;
    }

    public bool Succeeded { get; }

    public IReadOnlyList<string> Errors { get; }

    public string? FirstError => Errors.Count > 0 ? Errors[0] : null;

    public static Result Success() => new(true, Array.Empty<string>());

    public static Result Failure(params string[] errors) => new(false, errors);

    public static Result Failure(IEnumerable<string> errors) => new(false, errors.ToArray());
}

/// <summary>Result carrying a value on success.</summary>
public sealed class Result<T> : Result
{
    private Result(bool succeeded, T? value, IReadOnlyList<string> errors)
        : base(succeeded, errors)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(true, value, Array.Empty<string>());

    public static new Result<T> Failure(params string[] errors) => new(false, default, errors);

    public static new Result<T> Failure(IEnumerable<string> errors) => new(false, default, errors.ToArray());
}
