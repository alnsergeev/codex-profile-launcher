using CodexProfileLauncher;

var tests = new (string Name, Action Test)[]
{
    ("Parses active profile and profile sections", ParsesActiveProfileAndProfiles),
    ("Handles comments and friendly names", HandlesCommentsAndFriendlyNames),
    ("Warns when profile is missing", WarnsWhenProfileIsMissing),
    ("Ignores duplicate profiles in button list", IgnoresDuplicateProfiles),
    ("Updates active profile without dropping comments", UpdatesActiveProfileText),
    ("Safely updates file and creates backup", SafelyUpdatesFileAndCreatesBackup),
};

var failed = 0;

foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}");
        Console.WriteLine(ex.Message);
    }
}

if (failed > 0)
{
    Environment.Exit(1);
}

static void ParsesActiveProfileAndProfiles()
{
    var snapshot = CodexConfigService.Parse(
        "config.toml",
        """
        profile = "openai"

        [profiles.ollama]
        model = "qwen3"

        [profiles.openai]
        model = "gpt-5.4"
        """);

    Equal("openai", snapshot.ActiveProfile);
    Equal(2, snapshot.Profiles.Count);
    True(snapshot.Profiles.Any(profile => profile.Key == "openai"));
    True(snapshot.Profiles.Any(profile => profile.Key == "ollama"));
}

static void HandlesCommentsAndFriendlyNames()
{
    var snapshot = CodexConfigService.Parse(
        "config.toml",
        """
        # The active profile can have a trailing comment.
        profile = "remote" # active

        [profiles.remote] # remote model
        name = "Remote Ollama"
        model = "qwen3"
        """);

    Equal("remote", snapshot.ActiveProfile);
    Equal("Remote Ollama", snapshot.Profiles.Single().DisplayName);
}

static void WarnsWhenProfileIsMissing()
{
    var snapshot = CodexConfigService.Parse(
        "config.toml",
        """
        [profiles.openai]
        model = "gpt-5.4"
        """);

    True(snapshot.ActiveProfile is null);
    True(snapshot.Warnings.Any(warning => warning.Contains("No top-level profile", StringComparison.OrdinalIgnoreCase)));
}

static void IgnoresDuplicateProfiles()
{
    var snapshot = CodexConfigService.Parse(
        "config.toml",
        """
        profile = "openai"

        [profiles.openai]
        model = "gpt-5.4"

        [profiles.openai]
        model = "other"
        """);

    Equal(1, snapshot.Profiles.Count);
    True(snapshot.Warnings.Any(warning => warning.Contains("duplicate profile", StringComparison.OrdinalIgnoreCase)));
}

static void UpdatesActiveProfileText()
{
    var updated = CodexConfigService.UpdateActiveProfileText(
        """
        # keep this comment
        profile = "openai" # old

        [profiles.openai]
        model = "gpt-5.4"
        """,
        "ollama");

    Contains("# keep this comment", updated);
    Contains("profile = \"ollama\"", updated);
    Contains("[profiles.openai]", updated);
}

static void SafelyUpdatesFileAndCreatesBackup()
{
    var directory = Path.Combine(Path.GetTempPath(), $"codex-profile-launcher-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);

    try
    {
        var configPath = Path.Combine(directory, "config.toml");
        File.WriteAllText(
            configPath,
            """
            profile = "openai"

            [profiles.openai]
            model = "gpt-5.4"

            [profiles.ollama]
            model = "qwen3"
            """);

        var service = new CodexConfigService(configPath);
        service.SetActiveProfile("ollama");

        Contains("profile = \"ollama\"", File.ReadAllText(configPath));
        True(Directory.GetFiles(directory, "config.toml.*.bak").Length == 1);
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}

static void True(bool condition)
{
    if (!condition)
    {
        throw new InvalidOperationException("Expected condition to be true.");
    }
}

static void Contains(string expected, string actual)
{
    if (!actual.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected text to contain '{expected}'.");
    }
}
