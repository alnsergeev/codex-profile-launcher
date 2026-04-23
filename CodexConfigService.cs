using System.Text;
using System.Text.RegularExpressions;

namespace CodexProfileLauncher;

public sealed class CodexConfigService
{
    private static readonly Regex SectionRegex = new(@"^\s*\[\s*([^\]]+?)\s*\]\s*$", RegexOptions.Compiled);
    private static readonly Regex KeyValueRegex = new(@"^\s*([A-Za-z0-9_.-]+)\s*=\s*(.+?)\s*$", RegexOptions.Compiled);

    private readonly string _configPath;

    public CodexConfigService(string configPath)
    {
        _configPath = configPath;
    }

    public string ConfigPath => _configPath;

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
}
