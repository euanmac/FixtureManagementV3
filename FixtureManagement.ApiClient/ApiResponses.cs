using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FixtureManagement.Shared.Services;

namespace FixtureManagement.ApiClient;

/// <summary>
/// Turns HTTP responses back into what the service interfaces promise – the reverse of the server's ApiResults:
/// 404 → null / false / NotFound, 400 → Invalid with field errors, 409 → Conflict with the message,
/// 401/403 → ApiAuthenticationException, anything else unexpected → HttpRequestException.
/// </summary>
internal static class ApiResponses
{
    /// <summary>Same as the server: camelCase, case-insensitive, enums as numbers.</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The body as T, or null for 404.</summary>
    public static async Task<T?> ReadOrNullAsync<T>(HttpResponseMessage response, CancellationToken ct) where T : class
    {
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    /// <summary>The body as T; throws for any failure (used where the interface returns a plain value).</summary>
    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await EnsureSuccessAsync(response, ct);
        var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
        if (value is null) throw new HttpRequestException($"The server returned an empty response ({(int)response.StatusCode}).");
        return value;
    }

    /// <summary>true for success, false for 404 (the "returns false if it doesn't exist" methods).</summary>
    public static async Task<bool> SucceededOrNotFoundAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        await EnsureSuccessAsync(response, ct);
        return true;
    }

    public static async Task<ServiceResult> ToResultAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var failure = await FailureAsync(response, ct);
        if (failure is not null) return failure;
        return ServiceResult.Success();
    }

    public static async Task<ServiceResult<T>> ToResultAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var failure = await FailureAsync(response, ct);
        if (failure is not null) return ServiceResult<T>.From(failure);
        var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
        if (value is null) throw new HttpRequestException($"The server returned an empty response ({(int)response.StatusCode}).");
        return ServiceResult<T>.Success(value);
    }

    /// <summary>Throws for anything but success: auth problems, validation (as ApiValidationException), the rest.</summary>
    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        ThrowIfAuthProblem(response);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new ApiValidationException(await ReadErrorsAsync(response, ct));
        }
        response.EnsureSuccessStatusCode();
    }

    /// <summary>null on success; otherwise the matching ServiceResult (or throws for auth/unexpected errors).</summary>
    private static async Task<ServiceResult?> FailureAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return null;
        ThrowIfAuthProblem(response);
        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                return ServiceResult.NotFound();
            case HttpStatusCode.BadRequest:
                return new ServiceResult { Status = ServiceStatus.Invalid, Errors = await ReadErrorsAsync(response, ct) };
            case HttpStatusCode.Conflict:
                var problem = await ReadProblemAsync(response, ct);
                return ServiceResult.Conflict(problem?.Detail ?? problem?.Title ?? "That clashes with existing data.");
            default:
                response.EnsureSuccessStatusCode();   // throws HttpRequestException
                return null;
        }
    }

    private static void ThrowIfAuthProblem(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new ApiAuthenticationException(response.StatusCode);
        }
    }

    private static async Task<IReadOnlyDictionary<string, string[]>> ReadErrorsAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var problem = await ReadProblemAsync(response, ct);
        if (problem?.Errors is not null && problem.Errors.Count > 0) return problem.Errors;
        return new Dictionary<string, string[]> { [""] = [problem?.Detail ?? problem?.Title ?? "The request was invalid."] };
    }

    private static async Task<ProblemBody?> ReadProblemAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ProblemBody>(Json, ct);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            return null;   // not a problem+json body
        }
    }

    /// <summary>The parts of the server's ProblemDetails / ValidationProblemDetails we use.</summary>
    private sealed record ProblemBody(string? Title, string? Detail, Dictionary<string, string[]>? Errors);
}
