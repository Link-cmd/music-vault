# Music Vault

A compact retro music player for 64-bit Windows. Your collection lives inside the player executable, including songs added later.

## Features

- Search, play/pause, previous/next, shuffle, repeat, seeking, and volume.
- A white daytime theme and a dark night theme, switched with the moon/sun icon.
- A 360 × 360 mini-player, opened with the outlined rectangle icon.
- An animated dotted record display that follows playback state.
- Automatic loudness balancing and peak limiting, with no additional controls.
- Pencil and × icons beside each song to rename or delete it.
- A bundled fallback decoder for audio files Windows cannot decode correctly.

This repository contains source, an original icon, build scripts, and synthetic-audio tests. It includes no music, user collections, compiled players, or releases.

## Build

Use 64-bit Windows with .NET Framework 4.8 or later, Windows media playback support, and PowerShell 5.1 or later. An additional .NET SDK is not required. The included Framework compiler builds the WPF application.

In a PowerShell terminal inside this folder:

```powershell
.\setup-dependencies.ps1
.\build.ps1
```

The setup script downloads pinned NAudio and FFmpeg packages and checks their SHA-256 hashes. Dependencies remain in the ignored `.deps` folder. The build creates `MusicVault-base.exe`, an empty standalone player. Rename a copy for your own collection and click **Add songs**.

The bundled decoder makes the empty build approximately 84 MiB. Icons can be regenerated with `make-icon.ps1`.

## Use

Double-click a song to play it. Ctrl+F focuses search; Space toggles playback when focus is outside a text field or button. The moon/sun button switches themes; the outlined rectangle opens mini mode. The library icon in mini mode returns to the full player.

Click **Add songs** to import MP3, WAV, M4A, WMA, or AAC files. The app saves a replacement executable and restarts. Identical audio files are skipped. Use the pencil beside a song to edit its display name, or × to request deletion. Deletion asks for confirmation. F2 and Delete also work while the library list has focus.

Adding, renaming, and deleting need write access to the player's folder and enough temporary free disk space for another complete copy of the executable. Original files outside the player are not changed. Keep your own collection backups. Theme, mode, and volume reset when the app starts.

## Volume balancing

Playback prepares a temporary normalized WAV before starting, so opening a track can take a few seconds and needs temporary disk space. Original embedded audio remains unchanged. The default volume is 40%.

The processor uses a gated -26 dBFS RMS target, up to +12 dB gain, moving-window control for louder passages, and a -12 dBFS sample-peak ceiling. This is not LUFS/true-peak normalization or a hearing-protection guarantee. Actual listening volume also depends on Windows and your audio equipment. Very quiet or damaged recordings can remain quiet or fail to decode.

## Developer commands

```text
MusicVault-base.exe --pack <new-output.exe> <song-folder>
MusicVault-base.exe --reskin <existing-player.exe> <new-output.exe>
existing-player.exe --verify <report.txt>
```

Output files must not already exist. `--reskin` preserves the embedded collection and verifies each track while copying it into the new application. Source edits alone do not update existing collections; rebuild, then use this command with a separate output filename.

## Tests

Python 3 is required for the synthetic-audio/storage tests. They need no music files or third-party Python packages.

```powershell
python .\test-leveling.py
python .\test-delete.py
```

The first test creates synthetic inputs and checks loud/quiet matching, bursts, peaks, silence, stereo balance, and duration. The second checks Unicode renaming, blank-name rejection, checksums, deletion, reclaimed space, removing the final track, and adding afterward. Run them in the order shown.

## Dependencies and distribution

See [THIRD-PARTY.md](THIRD-PARTY.md), [NAudio-LICENSE.txt](NAudio-LICENSE.txt), and [FFmpeg-LICENSE.txt](FFmpeg-LICENSE.txt). No CI jobs are configured. Windows downloads are available from this repository's Releases page.

## Application license

Music Vault uses the [Free Use / Reserved Sale License](LICENSE.txt). You may use it for free, including internally at work, inspect and modify the source, and share copies for free with the notices intact. Selling, renting, paid bundling, or paid redistribution of the application requires written permission from the owner, GitHub account Link-cmd. This is a source-available license, not an open-source license. Third-party components retain their own licenses and rights, as described in [THIRD-PARTY.md](THIRD-PARTY.md).
