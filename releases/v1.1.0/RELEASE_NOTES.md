# Codex Profile Launcher v1.1.0

## Highlights

- Added UI flow to create a new `[profiles.<name>]` section directly from the launcher.
- Added UI flow to create a new `[model_providers.<name>]` section directly from the profile dialog.
- Made config actions clearer by distinguishing opening `config.toml` from opening its folder.
- Increased main window and dialog sizes and fixed layout issues introduced by the new controls.

## Included changes

- Add profile dialog with validation for key, model, optional provider, and optional reasoning effort.
- Add model provider dialog with validation for key, base URL, and wire API.
- Safe writes for newly created profile and model provider sections in `config.toml`.
- Regression coverage for creating profiles and model providers, duplicate detection, and config bootstrap scenarios.

## Artifact

- Windows x64 self-contained single-file executable.