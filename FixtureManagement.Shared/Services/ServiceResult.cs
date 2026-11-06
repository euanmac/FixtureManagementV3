namespace FixtureManagement.Shared.Services;

public enum ServiceStatus
{
    Success,
    /// <summary>The item being changed doesn't exist (HTTP 404).</summary>
    NotFound,
    /// <summary>The request refers to something that doesn't exist or breaks a rule (HTTP 400).</summary>
    Invalid,
    /// <summary>The change clashes with existing data, e.g. a duplicate or an item still in use (HTTP 409).</summary>
    Conflict
}

/// <summary>
/// Outcome of a service write. Lets the website, the API and the desktop client
/// handle "not found", "invalid" and "conflict" the same way without exceptions.
/// </summary>
public record ServiceResult
{
    public ServiceStatus Status { get; init; }

    /// <summary>Field name → messages, for Invalid results (shown like form validation errors).</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();

    /// <summary>Human-readable reason, for Conflict results.</summary>
    public string? Message { get; init; }

    public bool Succeeded => Status == ServiceStatus.Success;

    public static ServiceResult Success() => new() { Status = ServiceStatus.Success };
    public static ServiceResult NotFound() => new() { Status = ServiceStatus.NotFound };
    public static ServiceResult Invalid(string field, string message) =>
        new() { Status = ServiceStatus.Invalid, Errors = new Dictionary<string, string[]> { [field] = [message] } };
    public static ServiceResult Conflict(string message) => new() { Status = ServiceStatus.Conflict, Message = message };
}

/// <summary>A ServiceResult that carries the created or updated item on success.</summary>
public record ServiceResult<T> : ServiceResult
{
    public T? Value { get; init; }

    public static ServiceResult<T> Success(T value) => new() { Status = ServiceStatus.Success, Value = value };

    /// <summary>Copies a non-success result, e.g. <c>ServiceResult&lt;FixtureDto&gt;.From(ServiceResult.NotFound())</c>.</summary>
    public static ServiceResult<T> From(ServiceResult failure) =>
        new() { Status = failure.Status, Errors = failure.Errors, Message = failure.Message };
}
