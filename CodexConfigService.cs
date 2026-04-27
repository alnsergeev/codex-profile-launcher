using System.Text;
using System.Text.RegularExpressions;

namespace CodexProfileLauncher;

public sealed class CodexConfigService
{
    private static readonly Regex SectionRegex = new(@"^\s*\[\s*([^\]]+?)\s*\]\s*$", RegexOptions.Compiled);
    private static readonly Regex KeyValueRegex = new(@"^\s*([A-Za-z0-9_.-]+)\s*=\s*(.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex ProfileKeyRegex = new(@"^[A-Za-z0-9_.-]+$", RegexOptions.Compiled);

    private readonly string _configPath;

    public CodexConfigService(string configPath)
    {
        _configPath = configPath;
    }

    public string ConfigPath => _configPath;

    public static bool IsValidProfileKey(string? profileKey)
    {
        return !string.IsNullOrWhiteSpace(profileKey)
            && ProfileKeyRegex.IsMatch(profileKey.Trim());
    }

    public static bool IsValidModelProviderKey(string? providerKey)
    {
        return !string.IsNullOrWhiteSpace(providerKey)
            && ProfileKeyRegex.IsMatch(providerKey.Trim());
    }

    public CodexConfigSnapshot Load()
    {
        if (!File.Exists(_configPath))
        {
            return new CodexConfigSnapshot(
                _configPath,
                null,
                Array.Empty<CodexProfile>(),
                new[] { "Codex config.toml was not found." });
        }

        try
        {
            var text = File.ReadAllText(_configPath, Encoding.UTF8);
            return Parse(_configPath, text);
        }
        catch (Exception ex)
        {
            return new CodexConfigSnapshot(
                _configPath,
                null,
                Array.Empty<CodexProfile>(),
                new[] { $"Could not read config.toml: {ex.Message}" });
        }
    }

    public static CodexConfigSnapshot Parse(string configPath, string text)
    {
        var lines = SplitLines(text);
        var profiles = new List<CodexProfile>();
        var warnings = new List<string>();
        var profileIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        string? activeProfile = null;
        string? currentProfileKey = null;
        string? currentProfileDisplayName = null;
        int currentProfileLine = 0;
        var currentProfileDuplicate = false;
        var topLevel = true;

        void FlushProfile()
        {
            if (currentProfileKey is null)
            {
                return;
            }

            var displayName = string.IsNullOrWhiteSpace(currentProfileDisplayName)
                ? currentProfileKey
                : currentProfileDisplayName.Trim();

            profiles.Add(new CodexProfile(
                currentProfileKey,
                displayName,
                currentProfileLine,
                currentProfileDuplicate));

            currentProfileKey = null;
            currentProfileDisplayName = null;
            currentProfileLine = 0;
            currentProfileDuplicate = false;
        }

        for (var index = 0; index < lines.Count; index++)
        {
            var rawLine = lines[index];
            var lineNumber = index + 1;
            var lineWithoutComment = StripTrailingComment(rawLine).Trim();

            if (lineWithoutComment.Length == 0)
            {
                continue;
            }

            var sectionMatch = SectionRegex.Match(lineWithoutComment);
            if (sectionMatch.Success)
            {
                FlushProfile();
                topLevel = false;

                var sectionName = sectionMatch.Groups[1].Value.Trim();
                var profilePrefix = "profiles.";
                if (sectionName.StartsWith(profilePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var profileKey = sectionName[profilePrefix.Length..].Trim();
                    if (profileKey.Length == 0)
                    {
                        warnings.Add($"Line {lineNumber}: empty profile section name was ignored.");
                        continue;
                    }

                    currentProfileKey = profileKey;
                    currentProfileLine = lineNumber;

                    if (profileIndexes.ContainsKey(profileKey))
                    {
                        currentProfileDuplicate = true;
                        warnings.Add($"Line {lineNumber}: duplicate profile '{profileKey}' was ignored in the button list.");
                    }
                    else
                    {
                        profileIndexes.Add(profileKey, profiles.Count);
                    }
                }

                continue;
            }

            var keyValueMatch = KeyValueRegex.Match(lineWithoutComment);
            if (!keyValueMatch.Success)
            {
                warnings.Add($"Line {lineNumber}: unsupported config line was ignored.");
                continue;
            }

            var key = keyValueMatch.Groups[1].Value.Trim();
            var value = TryReadTomlString(keyValueMatch.Groups[2].Value.Trim());

            if (topLevel && key.Equals("profile", StringComparison.OrdinalIgnoreCase))
            {
                if (value is null)
                {
                    warnings.Add($"Line {lineNumber}: top-level profile value is not a supported string.");
                }
                else
                {
                    activeProfile = value;
                }
            }

            if (currentProfileKey is not null
                && (key.Equals("name", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("display_name", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("displayName", StringComparison.OrdinalIgnoreCase))
                && value is not null)
            {
                currentProfileDisplayName = value;
            }
        }

        FlushProfile();

        var uniqueProfiles = profiles
            .Where(profile => !profile.IsDuplicate)
            .OrderBy(profile => profile.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (activeProfile is null)
        {
            warnings.Add("No top-level profile was set. Choosing a profile will add one safely.");
        }
        else if (uniqueProfiles.All(profile => !profile.Key.Equals(activeProfile, StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add($"Active profile '{activeProfile}' is not defined under [profiles.*].");
        }

        return new CodexConfigSnapshot(configPath, activeProfile, uniqueProfiles, warnings);
    }

    public void SetActiveProfile(string profileName)
    {
        if (!File.Exists(_configPath))
        {
            throw new FileNotFoundException("Cannot update the active profile because config.toml was not found.", _configPath);
        }

        var originalText = File.ReadAllText(_configPath, Encoding.UTF8);
        var updatedText = UpdateActiveProfileText(originalText, profileName);
        SafeReplace(updatedText);
    }

    public void AddProfile(NewCodexProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var originalText = File.Exists(_configPath)
            ? File.ReadAllText(_configPath, Encoding.UTF8)
            : string.Empty;

        var updatedText = AddProfileText(originalText, profile);
        WriteConfig(updatedText);
    }

    public void AddModelProvider(NewCodexModelProvider modelProvider)
    {
        ArgumentNullException.ThrowIfNull(modelProvider);

        var originalText = File.Exists(_configPath)
            ? File.ReadAllText(_configPath, Encoding.UTF8)
            : string.Empty;

        var updatedText = AddModelProviderText(originalText, modelProvider);
        WriteConfig(updatedText);
    }

    public static string UpdateActiveProfileText(string configText, string profileName)
    {
        var lines = SplitLines(configText);
        var newline = DetectNewline(configText);
        var profileLineIndex = FindTopLevelProfileLine(lines);
        var updatedProfileLine = $"profile = \"{EscapeTomlString(profileName)}\"";

        if (profileLineIndex >= 0)
        {
            lines[profileLineIndex] = updatedProfileLine;
        }
        else
        {
            lines.Insert(0, updatedProfileLine);
            lines.Insert(1, string.Empty);
        }

        return string.Join(newline, lines);
    }

    public static string AddProfileText(string configText, NewCodexProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var normalizedProfile = NormalizeProfile(profile);
        ValidateNewProfile(normalizedProfile);

        var snapshot = Parse("config.toml", configText);
        if (snapshot.Profiles.Any(existing => existing.Key.Equals(normalizedProfile.Key, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Profile '{normalizedProfile.Key}' already exists in config.toml.");
        }

        var updatedText = normalizedProfile.SetAsActive
            ? UpdateActiveProfileText(configText, normalizedProfile.Key)
            : configText;

        var newline = DetectNewlineOrDefault(updatedText);
        var builder = new StringBuilder(updatedText.TrimEnd('\r', '\n'));

        if (builder.Length > 0)
        {
            builder.Append(newline);
            builder.Append(newline);
        }

        builder.Append(BuildProfileSection(normalizedProfile, newline));
        builder.Append(newline);
        return builder.ToString();
    }

    public static string AddModelProviderText(string configText, NewCodexModelProvider modelProvider)
    {
        ArgumentNullException.ThrowIfNull(modelProvider);

        var normalizedModelProvider = NormalizeModelProvider(modelProvider);
        ValidateNewModelProvider(normalizedModelProvider);

        if (SectionExists(configText, $"model_providers.{normalizedModelProvider.Key}"))
        {
            throw new InvalidOperationException($"Model provider '{normalizedModelProvider.Key}' already exists in config.toml.");
        }

        var newline = DetectNewlineOrDefault(configText);
        var builder = new StringBuilder(configText.TrimEnd('\r', '\n'));

        if (builder.Length > 0)
        {
            builder.Append(newline);
            builder.Append(newline);
        }

        builder.Append(BuildModelProviderSection(normalizedModelProvider, newline));
        builder.Append(newline);
        return builder.ToString();
    }

    public static string ResolveDefaultConfigPath()
    {
        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");

        if (!string.IsNullOrWhiteSpace(codexHome))
        {
            return Path.Combine(codexHome, "config.toml");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex",
            "config.toml");
    }

    private void SafeReplace(string updatedText)
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Could not determine the config directory.");
        }

        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $"config.{Guid.NewGuid():N}.tmp");
        var backupPath = Path.Combine(directory, $"config.toml.{DateTime.Now:yyyyMMddHHmmss}.bak");

        File.WriteAllText(tempPath, updatedText, Encoding.UTF8);

        try
        {
            File.Replace(tempPath, _configPath, backupPath, ignoreMetadataErrors: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    private void WriteConfig(string updatedText)
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Could not determine the config directory.");
        }

        Directory.CreateDirectory(directory);

        if (File.Exists(_configPath))
        {
            SafeReplace(updatedText);
            return;
        }

        File.WriteAllText(_configPath, updatedText, Encoding.UTF8);
    }

    private static NewCodexProfile NormalizeProfile(NewCodexProfile profile)
    {
        return profile with
        {
            Key = profile.Key.Trim(),
            DisplayName = NormalizeOptional(profile.DisplayName),
            Model = profile.Model.Trim(),
            ModelProvider = NormalizeOptional(profile.ModelProvider),
            ModelReasoningEffort = NormalizeOptional(profile.ModelReasoningEffort),
        };
    }

    private static NewCodexModelProvider NormalizeModelProvider(NewCodexModelProvider modelProvider)
    {
        return modelProvider with
        {
            Key = modelProvider.Key.Trim(),
            DisplayName = NormalizeOptional(modelProvider.DisplayName),
            BaseUrl = modelProvider.BaseUrl.Trim(),
            WireApi = modelProvider.WireApi.Trim(),
        };
    }

    private static void ValidateNewProfile(NewCodexProfile profile)
    {
        if (!IsValidProfileKey(profile.Key))
        {
            throw new ArgumentException("Profile key must use only letters, numbers, '.', '_' or '-'.", nameof(profile));
        }

        if (string.IsNullOrWhiteSpace(profile.Model))
        {
            throw new ArgumentException("Model is required.", nameof(profile));
        }
    }

    private static void ValidateNewModelProvider(NewCodexModelProvider modelProvider)
    {
        if (!IsValidModelProviderKey(modelProvider.Key))
        {
            throw new ArgumentException("Model provider key must use only letters, numbers, '.', '_' or '-'.", nameof(modelProvider));
        }

        if (!Uri.TryCreate(modelProvider.BaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Base URL must be a valid absolute http or https URL.", nameof(modelProvider));
        }

        if (string.IsNullOrWhiteSpace(modelProvider.WireApi))
        {
            throw new ArgumentException("Wire API is required.", nameof(modelProvider));
        }
    }

    private static string BuildProfileSection(NewCodexProfile profile, string newline)
    {
        var lines = new List<string>
        {
            $"[profiles.{profile.Key}]"
        };

        if (!string.IsNullOrWhiteSpace(profile.DisplayName))
        {
            lines.Add($"name = \"{EscapeTomlString(profile.DisplayName)}\"");
        }

        lines.Add($"model = \"{EscapeTomlString(profile.Model)}\"");

        if (!string.IsNullOrWhiteSpace(profile.ModelProvider))
        {
            lines.Add($"model_provider = \"{EscapeTomlString(profile.ModelProvider)}\"");
        }

        if (!string.IsNullOrWhiteSpace(profile.ModelReasoningEffort))
        {
            lines.Add($"model_reasoning_effort = \"{EscapeTomlString(profile.ModelReasoningEffort)}\"");
        }

        return string.Join(newline, lines);
    }

    private static string BuildModelProviderSection(NewCodexModelProvider modelProvider, string newline)
    {
        var lines = new List<string>
        {
            $"[model_providers.{modelProvider.Key}]",
            $"name = \"{EscapeTomlString(modelProvider.DisplayNameOrKey)}\"",
            $"base_url = \"{EscapeTomlString(modelProvider.BaseUrl)}\"",
            $"wire_api = \"{EscapeTomlString(modelProvider.WireApi)}\"",
        };

        return string.Join(newline, lines);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool SectionExists(string configText, string sectionName)
    {
        foreach (var line in SplitLines(configText))
        {
            var lineWithoutComment = StripTrailingComment(line).Trim();
            if (lineWithoutComment.Length == 0)
            {
                continue;
            }

            var sectionMatch = SectionRegex.Match(lineWithoutComment);
            if (sectionMatch.Success
                && sectionMatch.Groups[1].Value.Trim().Equals(sectionName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int FindTopLevelProfileLine(IReadOnlyList<string> lines)
    {
        var topLevel = true;

        for (var index = 0; index < lines.Count; index++)
        {
            var lineWithoutComment = StripTrailingComment(lines[index]).Trim();
            if (lineWithoutComment.Length == 0)
            {
                continue;
            }

            if (SectionRegex.IsMatch(lineWithoutComment))
            {
                topLevel = false;
                continue;
            }

            if (!topLevel)
            {
                continue;
            }

            var keyValueMatch = KeyValueRegex.Match(lineWithoutComment);
            if (keyValueMatch.Success
                && keyValueMatch.Groups[1].Value.Trim().Equals("profile", StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static string? TryReadTomlString(string value)
    {
        if (value.Length < 2)
        {
            return null;
        }

        if ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))
        {
            return value[1..^1]
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal);
        }

        return null;
    }

    private static string StripTrailingComment(string line)
    {
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var escaped = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (inDoubleQuote && character == '\\')
            {
                escaped = true;
                continue;
            }

            if (character == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
                continue;
            }

            if (character == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
                continue;
            }

            if (character == '#' && !inSingleQuote && !inDoubleQuote)
            {
                return line[..index];
            }
        }

        return line;
    }

    private static string EscapeTomlString(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static List<string> SplitLines(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .ToList();
    }

    private static string DetectNewline(string text)
    {
        return text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
    }

    private static string DetectNewlineOrDefault(string text)
    {
        return text.Length == 0 ? Environment.NewLine : DetectNewline(text);
    }
}
