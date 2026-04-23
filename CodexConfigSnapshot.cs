namespace CodexProfileLauncher;

public sealed record CodexConfigSnapshot(
    string ConfigPath,
    string? ActiveProfile,
    IReadOnlyList<CodexProfile> Profiles,
    IReadOnlyList<string> Warnings)
{
    public bool Exists => File.Exists(ConfigPath);
}
