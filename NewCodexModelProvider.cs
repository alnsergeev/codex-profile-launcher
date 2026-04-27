namespace CodexProfileLauncher;

public sealed record NewCodexModelProvider(
    string Key,
    string? DisplayName,
    string BaseUrl,
    string WireApi)
{
    public string DisplayNameOrKey => string.IsNullOrWhiteSpace(DisplayName)
        ? Key
        : DisplayName.Trim();
}