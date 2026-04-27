namespace CodexProfileLauncher;

public sealed record NewCodexProfile(
    string Key,
    string? DisplayName,
    string Model,
    string? ModelProvider,
    string? ModelReasoningEffort,
    bool SetAsActive)
{
    public string DisplayNameOrKey => string.IsNullOrWhiteSpace(DisplayName)
        ? Key
        : DisplayName.Trim();
}