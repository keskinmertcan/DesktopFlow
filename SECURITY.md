# Security

DesktopFlow moves files on the computer where it is installed. It does not upload those files.

## Reporting a problem

Open a private security advisory on the GitHub repository, or open a public issue if the report does not include a way to damage someone's files. Include the Windows version, whether the app is unpackaged or MSIX, and the relevant lines from `%LOCALAPPDATA%\DesktopFlow\logs`. Do not attach the files that were being organized.

## What this app refuses to do

- It does not delete files as part of organizing.
- It does not overwrite an existing file. A name collision gets a numbered name.
- It does not follow a destination path that leaves the monitored folder.
- It does not add itself to Windows startup unless you turn that option on.

## Local data

Settings, rules, and activity history are stored in `%LOCALAPPDATA%\DesktopFlow\desktopflow.db`. Logs are in `%LOCALAPPDATA%\DesktopFlow\logs`. Uninstalling an unpackaged copy does not remove that folder. Delete it yourself if you want the history gone.
