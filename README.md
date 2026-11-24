# DesktopFlow

A cleaner desktop. Automatically.

DesktopFlow is a free, open-source Windows 11 app that sorts files on your desktop and in Downloads. It runs entirely on your PC. It does not delete files, and it does not overwrite a file that is already there.

The first launch is a setup wizard. It shows what would move and waits for you to confirm. After that, you can organize by hand or let the app watch for new files.

## Features

- Separate organization for the desktop and for Downloads, with independent on/off switches.
- Folder maps you can rename, including documents, images, video, audio, archives, installers, and source files.
- Custom rules with priority, enable/disable, and a list of overlapping rules.
- A preview before a manual organize, then a real move on the Windows file system.
- Background watching that waits until a download has finished writing.
- Activity history in a local SQLite database, with search, filters, and undo.
- Notification-area icon, optional start with Windows, and English and Turkish interface text.

Folders, shortcuts, `desktop.ini`, hidden files, and system files are left alone. Details are in [docs/safety.md](docs/safety.md).

## Screenshots

No screenshots are checked in yet. Add PNG captures to the `assets` folder when you have them. The app window title is DesktopFlow, and the first screen is the setup wizard.

## Requirements

- Windows 11 x64 (version 22000 or later)
- For a local build: .NET SDK 10.0.401 or a later 10.0 SDK (`global.json`)
- For a framework-dependent build: the .NET 10 runtime
- A self-contained publish does not need a separate .NET install

The app does not need an internet connection. The update check is optional and only reads the public GitHub releases API for this repository.

## Install

The practical install is the unpackaged, self-contained build.

```powershell
dotnet publish src/DesktopFlow.App/DesktopFlow.App.csproj -c Release -r win-x64 --self-contained true -p:WindowsPackageType=None
```

Run `DesktopFlow.exe` from the `publish` directory. GitHub Actions on a published release also uploads that folder as a zip, and attempts an MSIX signed with a certificate created only for that job. That MSIX is not a Microsoft Store package. Trust the certificate only if you built it yourself.

An MSIX build requests full trust and unvirtualized file-system access so it can see the real desktop. The unpackaged app is the default and the one used for day-to-day development.

## Use

1. Open DesktopFlow. The setup wizard asks which folders to watch and whether new files should be organized automatically.
2. Read the preview. Finish setup without organizing if you want to leave the current files where they are.
3. Use **Organize Desktop** or **Organize Downloads** when you want a pass over the files that are already there. You get a confirmation when preview or bulk confirmation is on.
4. Open **Activity History** to undo a move. Undo still works after you quit and open the app again.
5. Closing the window after setup keeps DesktopFlow in the notification area. **Exit** in that menu closes it completely.

Automatic organization stays off until setup is finished, the matching folder is being watched, and automation is not paused.

## Develop

```powershell
dotnet build DesktopFlow.slnx -c Debug
dotnet test tests/DesktopFlow.Tests/DesktopFlow.Tests.csproj -c Debug
dotnet run --project src/DesktopFlow.App/DesktopFlow.App.csproj -c Debug
```

See [CONTRIBUTING.md](CONTRIBUTING.md) and [docs/architecture.md](docs/architecture.md).

The solution is `DesktopFlow.slnx`. Version `1.0.0` is set in `Directory.Build.props`.

## License

[MIT](LICENSE)
