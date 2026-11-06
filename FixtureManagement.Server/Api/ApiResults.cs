using System.ComponentModel.DataAnnotations;
using FixtureManagement.Shared.Services;

/// <summary>Helpers shared by the /api endpoint groups.</summary>
internal static class ApiResults
{
    /// <summary>
    /// Runs the DTO's DataAnnotations (and IValidatableObject) checks. Minimal APIs in .NET 9
    /// don't do this automatically. Returns a 400 ValidationProblem, or null if the DTO is valid.
    /// </summary>
    public static IResult? Validate(object model) =>
        Errors(model, prefix: "") is { Count: > 0 } errors ? Results.ValidationProblem(errors) : null;

    /// <summary>Validates every item in a list; error keys are prefixed with the index, e.g. "[2].Opponent".</summary>
    public static IResult? ValidateAll<T>(IReadOnlyList<T> models) where T : notnull
    {
        var errors = new Dictionary<string, string[]>();
        for (var i = 0; i < models.Count; i++)
            foreach (var (key, messages) in Errors(models[i], $"[{i}]."))
                errors[key] = messages;
        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    /// <summary>Turns a failed ServiceResult into 404, 400 (with field errors) or 409.</summary>
    public static IResult Failure(ServiceResult result) => result.Status switch
    {
        ServiceStatus.NotFound => Results.NotFound(),
        ServiceStatus.Invalid => Results.ValidationProblem(result.Errors.ToDictionary(e => e.Key, e => e.Value)),
        ServiceStatus.Conflict => Results.Problem(detail: result.Message, statusCode: StatusCodes.Status409Conflict),
        _ => throw new ArgumentException("Failure() was given a successful result.", nameof(result)),
    };

    private static Dictionary<string, string[]> Errors(object model, string prefix)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results
            .SelectMany(r => r.MemberNames.DefaultIfEmpty("").Select(m => (Member: prefix + m, r.ErrorMessage)))
            .GroupBy(e => e.Member)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage ?? "Invalid value.").ToArray());
    }
}
