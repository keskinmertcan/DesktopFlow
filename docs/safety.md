# Safety rules

These rules are enforced by the mover and the planner, not only by the settings screen.

- Folders on the desktop are left in place. Only files in the top level of a monitored folder are candidates.
- `desktop.ini`, hidden files, and system files are skipped.
- Shortcuts (`.lnk`) stay put unless that protection is turned off.
- Partial downloads (`.crdownload`, `.partial`, `.part`, and similar names) are not moved. The watcher waits until the file size stops changing and the file can be opened.
- If the destination folder cannot be created or written, the source file stays where it was.
- An existing destination file is never replaced. The new file is saved with a numbered name such as `report (1).pdf`.
- A move on the same volume is a rename. A move across volumes copies to a temporary name, checks the size, and deletes the source only after that copy exists.
- Undo uses the stored original path. If that name is taken, the app asks before restoring with a numbered name.
- Symlink and junction targets that would leave the monitored folder are rejected.
