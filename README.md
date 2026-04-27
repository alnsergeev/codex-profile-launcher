# Codex Profile Launcher

**Codex Profile Launcher** is a small Windows-only utility for using **OpenAI Codex Desktop** with multiple model backends, including **local or self-hosted LLMs** exposed through Codex profiles.

Its main value is simple: it makes the **Codex Desktop GUI** practical to use with a **local Ollama setup** or another self-hosted OpenAI-compatible endpoint, without manually editing `config.toml` every time you switch.

It reads profile definitions from Codex `config.toml`, shows one launch button per profile, saves the selected profile as the active default, and launches or restarts Codex Desktop.

The goal is intentionally narrow: a fast, local, practical Windows UI utility for switching Codex profiles so that Codex Desktop can be used comfortably as a GUI for both OpenAI-hosted and local/self-hosted models.

## The problem

A common use case is running **Codex Desktop as a GUI** while switching between:

- an OpenAI-hosted model
- a local or self-hosted LLM such as **Ollama** behind an OpenAI-compatible endpoint

Codex Desktop supports this through profile-driven configuration in `config.toml`, but switching that setup is not currently exposed as a first-class desktop UI flow.

If you use Codex Desktop with both cloud and local model backends, editing `config.toml` by hand gets old quickly. This launcher removes that friction without becoming a full configuration manager.

## What it does

- Reads the active profile from top-level `profile = "..."`.
- Reads available profiles from `[profiles.<name>]`.
- Shows one button per profile.
- Can create a basic `[profiles.<name>]` section from the UI.
- Can create a basic `[model_providers.<name>]` section from the UI.
- Highlights the current profile.
- Safely updates the active `profile`.
- Creates a timestamped backup before modifying `config.toml`.
- Opens Codex Desktop with the selected profile.
- Offers to restart Codex if it is already running.
- Provides quick actions to open the config file, open the config folder, and copy the config path.

## What it does not do

- It does not edit profile contents.
- It does not edit model provider contents after creation.
- It does not create advanced nested provider config for you.
- It does not add telemetry, networking, or cloud services.
- It is not cross-platform.
- It is not intended to replace Codex Desktop settings.

## Example config

Example: using Codex Desktop GUI with the default OpenAI profile and an Ollama-backed local or self-hosted profile.

```toml
profile = "openai"

[profiles.openai]
model = "gpt-5.4"
model_reasoning_effort = "high"

[profiles.ollama]
name = "Remote Ollama"
model = "qwen3.6:35b"
model_provider = "remote_ollama"
model_reasoning_effort = "medium"

[model_providers.remote_ollama]
name = "Remote Ollama"
base_url = "http://your-ollama-host:11434/v1"
wire_api = "responses"

[windows]
sandbox = "elevated"
```

Each `[profiles.<name>]` section becomes a launch button.

If a profile contains `name`, `display_name`, or `displayName`, that value is used as a friendly label in the UI.

## Config location

The launcher looks for:

- `CODEX_HOME\\config.toml` if `CODEX_HOME` is set
- otherwise `%USERPROFILE%\\.codex\\config.toml`

## Safety

When switching profiles, the launcher:

1. Reads the current `config.toml`.
2. Updates only the top-level `profile = "..."` setting.
3. Writes the new content to a temporary file in the same folder.
4. Replaces the original file using `File.Replace`.
5. Creates a timestamped backup such as `config.toml.20260423120000.bak`.

This preserves the rest of your config, including comments and provider sections.

## Requirements

- Windows
- OpenAI Codex Desktop installed from the Microsoft Store
- .NET 8 SDK for local builds

## Build

```powershell
dotnet build .\\CodexProfileLauncher.csproj -c Release
```

## Run from source

```powershell
dotnet run --project .\\CodexProfileLauncher.csproj
```

## Publish as a single EXE

```powershell
dotnet publish .\\CodexProfileLauncher.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o .\\publish `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:DebugType=None
```

## Known limitations

- Profile section names are expected to be simple keys such as `[profiles.openai]` or `[profiles.ollama]`.
- The parser is intentionally scoped to the subset of TOML this launcher needs: top-level `profile` and `[profiles.*]` sections.
- Codex Desktop is a single-instance app, so applying a changed profile usually requires a restart.
- Restarting Codex first attempts a graceful close, then falls back to killing Codex Desktop processes if needed.

## Support

If this project is useful to you and you want to support it, donations are welcome.

See [SUPPORT.md](SUPPORT.md).

## Project structure

- `MainForm.cs` - WinForms UI
- `CodexConfigService.cs` - profile discovery and safe config writes
- `CodexLauncherService.cs` - Codex process detection, launch, and restart
- `CodexProfile.cs` - profile model
- `CodexConfigSnapshot.cs` - parsed config state

## License

MIT
