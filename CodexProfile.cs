namespace CodexProfileLauncher;

public sealed record CodexProfile(string Key, string DisplayName, int FirstLine, bool IsDuplicate)
{
    public string ButtonText(bool isActive)
    {
        var suffix = isActive ? " [current]" : string.Empty;
        return $"Launch Codex: {DisplayName}{suffix}";
    }
}
