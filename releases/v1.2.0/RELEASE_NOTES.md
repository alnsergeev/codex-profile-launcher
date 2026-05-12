# Codex Profile Launcher v1.2.0

## Highlights

- Retargeted the WinForms app and regression test runner to .NET 10.
- Added `global.json` so local builds prefer the stable .NET 10 SDK line instead of a preview SDK when both are installed.
- Refreshed README requirements, build/test guidance, and project structure notes for the .NET 10 release.

## Included changes

- Main app target framework updated from `net8.0-windows` to `net10.0-windows`.
- Test runner target framework updated from `net8.0-windows` to `net10.0-windows`.
- Application package, assembly, file, and informational versions updated to `1.2.0`.
- Verified release build, regression tests, and Windows x64 self-contained publish on .NET 10.

## Artifact

- Windows x64 self-contained single-file executable.
