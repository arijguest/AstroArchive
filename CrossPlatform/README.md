# AstroArchive for Linux — preview 1

This is the Linux x64 desktop release based on AstroArchive 3.1.3. It includes its
.NET 10 runtime. Supported targets are Ubuntu 22.04/24.04 x64 and Debian 12/13 x64
with a graphical desktop. Wayland desktops currently use XWayland.

Install the Debian package with `sudo apt install ./astroarchive_3.1.3-preview.1_amd64.deb`,
or extract the tarball and run `./AstroArchive`. Check `SHA256SUMS` before installing.
The tarball also needs the distribution's ICU, OpenSSL 3, fontconfig and X11 libraries.
Zstandard and compressed FITS previews use optional distribution libzstd/libcfitsio
packages. Unavailable codecs report their limitation; original files remain exportable.

## Shared drives and Windows compatibility

Open the same archive folder on either platform. Capture files, the SQLite schema,
Microsoft JSON dates, capture paths, sidecars and Edited project format stay compatible
with Windows 3.1.1–3.1.3. Linux translates Windows separators in memory and writes the
existing Windows separator convention to disk. Opening on Linux rotates the existing
archive cache identity before writes so Windows reloads the drive's latest index.
Keep the entire archive folder, including `.astroarchive`, on the drive.

Close AstroArchive before unplugging the drive, then eject/unmount it normally.
Use exFAT for a drive that both operating systems must write without extra drivers.
NTFS also works with a writable Linux NTFS mount; Windows NTFS permissions can affect
write access. FAT32 cannot hold a file larger than 4 GiB. ext4 requires a Windows driver.
Use one writer at a time. A writable local mount is required; network and cloud-backed
archives are outside this preview's tested support.

Windows deletion protection is a Windows feature. Its settings are preserved on Linux;
Linux does not enforce its NTFS rules. Protected archives permit browsing, exports,
working copies and backups; disable protection on Windows before importing or moving
captures on Linux. Complete any interrupted protection change on
Windows before opening the drive on Linux. Backups omit Windows protection settings.

## Available workflows

- Repository: browse/search, numeric FITS/XISF/SER previews where decodable, SHA-256
  verification, batch target/filter editing, original-file and stacking project exports.
- Import: scan local folders, mounted telescope cards and mounted shares; review failures;
  import selected or eligible captures; content deduplication and verified copies.
- Edited: create archive working copies, add finished images, export selected images,
  launch a configured external editor with an argument vector.
- Analytics: six integration reports and complete SVG/JSON exports.
- Settings/Guide: appearance, editor, verified folder/ZIP backups and workflow guidance.

Source originals are always retained. Rescan after cancellation to resume safely.
This preview does not include direct telescope discovery/credentials, live network
imports, online/local plate-solving UI, rotation analysis UI, native deletion protection,
or all Windows dialogs. Raster/movie formats can be archived and exported unchanged;
numeric previews and scientific conversions require an available reader.

Settings live in `$XDG_CONFIG_HOME/astroarchive/settings.json`, or
`~/.config/astroarchive/settings.json`. Linux editor paths are machine settings and do
not travel in the archive. There is no automatic Linux updater; install the next package
or replace the extracted application folder. User archives are not installed app files.

## Reproduce validation

Install .NET SDK 10.0.401 and the desktop libraries, then from the repository root:

```sh
dotnet run --project CrossPlatform/EngineTests -- /tmp/astroarchive-engine-unique
dotnet test CrossPlatform/Desktop.Tests
bash scripts/build-linux.sh
tar -xzf linux-artifacts/AstroArchive-3.1.3-preview.1-linux-x64.tar.gz -C /tmp
xvfb-run -a /tmp/AstroArchive-3.1.3-preview.1-linux-x64/AstroArchive --ui-smoke /tmp/astroarchive-ui-unique
```

`AstroArchive --self-test /tmp/unique-directory` tests the installed executable's
imports, exports, working copies, backups and reopened index without needing an SDK.
`--ui-smoke` drives all six actual pages and saves PNG screenshots; it needs a display.
Use a new empty test directory for each invocation. GitHub CI exchanges real fixtures
between Linux and the Windows Framework engine in both directions.

Required Notice: Copyright 2026 Ari J. Guest (https://arijguest.com)

AstroArchive is distributed under PolyForm Noncommercial 1.0.0. See LICENSE and
THIRD-PARTY-NOTICES.txt in the application package.
