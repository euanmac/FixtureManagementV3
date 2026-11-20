namespace FixtureManagement.UI.Components;

/// <summary>One button in a FilterRow: its text, how many items it would show, whether it's in force, and its link.</summary>
public sealed record FilterOption(string Label, int Count, bool Active, string Url);
