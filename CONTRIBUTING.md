# Contributing

DesktopFlow is a Windows 11 desktop app. Changes should keep file moves predictable: no deletes, no overwrites, and no work on the real desktop during tests.

## Requirements

- Windows 11 x64
- .NET SDK 10.0.401 or a later 10.0 feature band (`global.json` allows `latestFeature`)
- A local build uses the .NET 10 runtime already installed with the SDK

## Build and test

```powershell
dotnet build DesktopFlow.slnx -c Debug
dotnet test tests/DesktopFlow.Tests/DesktopFlow.Tests.csproj -c Debug
```

Tests create temporary folders. Do not point them at a real Desktop or Downloads directory.

## Run the unpackaged app

```powershell
dotnet run --project src/DesktopFlow.App/DesktopFlow.App.csproj -c Debug
```

The first window is the setup wizard. Nothing is moved until the last step is confirmed. Closing the window during setup exits the app.

## Project layout

- `src/DesktopFlow.Core` — classification, rules, planning, and safety rules with no UI
- `src/DesktopFlow.Infrastructure` — SQLite, file moves, and the background watcher
- `src/DesktopFlow.App` — WinUI 3 views and view models
- `tests/DesktopFlow.Tests` — unit tests against temporary directories

The version in `Directory.Build.props` is the version the app and packages use.

## Pull requests

Describe the user-visible behavior, how you tested it, and whether a real folder was involved. Do not commit `.pfx` files, local databases, or logs.
