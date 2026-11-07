namespace FixtureManagement.ApiClient;

/// <summary>Builds "?a=1&amp;b=2" from optional values, leaving out the empty ones.</summary>
internal static class QueryString
{
    public static string From(params (string Name, string? Value)[] parameters)
    {
        var parts = parameters
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .Select(p => $"{Uri.EscapeDataString(p.Name)}={Uri.EscapeDataString(p.Value!)}")
            .ToList();
        return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
    }

    /// <summary>ISO yyyy-MM-dd, which the server's DateOnly query binding reads.</summary>
    public static string? Date(DateOnly? date) => date?.ToString("yyyy-MM-dd");
}
