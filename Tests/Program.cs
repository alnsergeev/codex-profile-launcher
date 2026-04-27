using CodexProfileLauncher;

var tests = new (string Name, Action Test)[]
{
    ("Parses active profile and profile sections", ParsesActiveProfileAndProfiles),
    ("Handles comments and friendly names", HandlesCommentsAndFriendlyNames),
    ("Warns when profile is missing", WarnsWhenProfileIsMissing),
    ("Ignores duplicate profiles in button list", IgnoresDuplicateProfiles),
    ("Updates active profile without dropping comments", UpdatesActiveProfileText),
    ("Adds a new profile section without dropping config", AddsProfileText),
    ("Rejects duplicate profile keys when adding", RejectsDuplicateProfileKeys),
    ("Adds a new model provider section without dropping config", AddsModelProviderText),
    ("Rejects duplicate model provider keys when adding", RejectsDuplicateModelProviderKeys),
    ("Safely updates file and creates backup", SafelyUpdatesFileAndCreatesBackup),
    ("Creates config when adding a first profile", CreatesConfigWhenAddingFirstProfile),
    ("Creates config when adding a first model provider", CreatesConfigWhenAddingFirstModelProvider),
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

static void AddsProfileText()
{
    var updated = CodexConfigService.AddProfileText(
        """
        profile = "openai"

        [profiles.openai]
        model = "gpt-5.4"

        [model_providers.remote_ollama]
        base_url = "http://localhost:11434/v1"
        """,
        new NewCodexProfile(
            "ollama",
            "Remote Ollama",
            "qwen3:latest",
            "remote_ollama",
            "medium",
            SetAsActive: true));

    Contains("profile = \"ollama\"", updated);
    Contains("[profiles.ollama]", updated);
    Contains("name = \"Remote Ollama\"", updated);
    Contains("model = \"qwen3:latest\"", updated);
    Contains("model_provider = \"remote_ollama\"", updated);
    Contains("model_reasoning_effort = \"medium\"", updated);
    Contains("[model_providers.remote_ollama]", updated);
}

static void RejectsDuplicateProfileKeys()
{
    try
    {
        CodexConfigService.AddProfileText(
            """
            [profiles.openai]
            model = "gpt-5.4"
            """,
            new NewCodexProfile(
                "openai",
                null,
                "gpt-5.4",
                null,
                null,
                SetAsActive: false));
    }
    catch (InvalidOperationException)
    {
        return;
    }

    throw new InvalidOperationException("Expected duplicate profile keys to be rejected.");
}

static void AddsModelProviderText()
{
    var updated = CodexConfigService.AddModelProviderText(
        """
        profile = "openai"

        [profiles.openai]
        model = "gpt-5.4"
        """,
        new NewCodexModelProvider(
            "remote_ollama",
            "Remote Ollama",
            "http://localhost:11434/v1",
            "responses"));

    Contains("[profiles.openai]", updated);
    Contains("[model_providers.remote_ollama]", updated);
    Contains("name = \"Remote Ollama\"", updated);
    Contains("base_url = \"http://localhost:11434/v1\"", updated);
    Contains("wire_api = \"responses\"", updated);
}

static void RejectsDuplicateModelProviderKeys()
{
    try
    {
        CodexConfigService.AddModelProviderText(
            """
            [model_providers.remote_ollama]
            base_url = "http://localhost:11434/v1"
            wire_api = "responses"
            """,
            new NewCodexModelProvider(
                "remote_ollama",
                null,
                "http://localhost:8080/v1",
                "responses"));
    }
    catch (InvalidOperationException)
    {
        return;
    }

    throw new InvalidOperationException("Expected duplicate model provider keys to be rejected.");
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

static void CreatesConfigWhenAddingFirstProfile()
{
    var directory = Path.Combine(Path.GetTempPath(), $"codex-profile-launcher-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);

    try
    {
        var configPath = Path.Combine(directory, "config.toml");
        var service = new CodexConfigService(configPath);

        service.AddProfile(new NewCodexProfile(
            "ollama",
            "Remote Ollama",
            "qwen3:latest",
            "remote_ollama",
            null,
            SetAsActive: true));

        var text = File.ReadAllText(configPath);
        Contains("profile = \"ollama\"", text);
        Contains("[profiles.ollama]", text);
        Contains("model_provider = \"remote_ollama\"", text);
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void CreatesConfigWhenAddingFirstModelProvider()
{
    var directory = Path.Combine(Path.GetTempPath(), $"codex-profile-launcher-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);

    try
    {
        var configPath = Path.Combine(directory, "config.toml");
        var service = new CodexConfigService(configPath);

        service.AddModelProvider(new NewCodexModelProvider(
            "remote_ollama",
            "Remote Ollama",
            "http://localhost:11434/v1",
            "responses"));

        var text = File.ReadAllText(configPath);
        Contains("[model_providers.remote_ollama]", text);
        Contains("name = \"Remote Ollama\"", text);
        Contains("base_url = \"http://localhost:11434/v1\"", text);
        Contains("wire_api = \"responses\"", text);
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

