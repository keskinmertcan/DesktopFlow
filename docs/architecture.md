# Architecture

DesktopFlow is three projects plus tests.

`DesktopFlow.Core` decides what a file is and where it would go. It does not touch the disk. Classification uses the folder map stored in settings. A custom rule with a lower priority number wins over another rule and over the built-in map. A destination must stay inside the monitored folder.

`DesktopFlow.Infrastructure` performs the disk work. A storage scan reads fixed drives, skips Windows system locations and links, and groups files that share the same content. SQLite stores settings, rules, and activity through Entity Framework Core. The database file is `%LOCALAPPDATA%\DesktopFlow\desktopflow.db`. A background service owns the file-system watchers. Watchers are not recursive, so files that have already been placed in a category folder are not watched again. A move is recorded as pending before the file is renamed. On the next start, a pending record is reconciled with what is actually on disk.

`DesktopFlow.App` is the WinUI 3 shell. View models call the services. The window can hide to the notification area after setup. A real Exit from the tray menu stops the process.

The unpackaged build is the one that sees the normal Desktop and Downloads folders. An MSIX build requests `runFullTrust` and `unvirtualizedResources` for the same reason. The app does not need a network connection except for the optional update check against the GitHub releases API.
