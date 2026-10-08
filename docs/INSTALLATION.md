# Installation and migration

V2.0.0 is publicly released. Installer and portable packages are built from the same self-contained Windows x64 publish directory; no separate .NET installation is needed.

The installer installs under the current user's local application data, creates a Start Menu shortcut, and optionally creates a desktop shortcut. It requires no administrator privileges. PDF preview uses the separately installed Microsoft Edge WebView2 Runtime; no runtime is downloaded or installed automatically.

## Download and first run

Download only from the [official v2.0.0 GitHub release](https://github.com/AlinTibi/p7s-universal-viewer/releases/tag/v2.0.0) or [ALMARFELD product page](https://almarfeld.com/software/p7s-universal-viewer/). Run `P7SViewer_Setup-v2.0.0.exe` for installation, or extract the portable ZIP and run `P7SUniversalViewer.exe`.

The Windows binaries are not Authenticode signed. Windows may therefore show a reputation or security warning. A signed Git tag is not a Windows executable signature. Do not disable Windows security; if unsure about a download, stop and verify its source and checksum.

## Verify SHA-256

In PowerShell, open the folder containing your download and run the relevant command:

```powershell
Get-FileHash -Algorithm SHA256 .\P7SViewer_Setup-v2.0.0.exe
Get-FileHash -Algorithm SHA256 .\P7SUniversalViewer-v2.0.0-win-x64.zip
```

Compare the `Hash` value with the matching value below (letter case does not matter):

- Installer: `c13490b2493658e378828bb9d3526adf1ae2525b058364de2822d40306e69d53`
- Portable ZIP: `78567c82f020215c623a07847098ec94f9cc418acfc64b4542fb1badffcbcac7`

The release also provides `.sha256` files. A matching hash confirms the downloaded bytes match the published artifact; it is not a malware scan or an Authenticode signature. Do not run a file whose hash differs.

## Historical v1 installations

The original v1 installer used a machine-wide Program Files directory. V2 deliberately uses per-user installation. It does not silently migrate or overwrite that machine-wide installation. Both may coexist. If you choose to remove v1, use its existing Windows uninstall entry; administrative approval may be required. Preserve your signed source documents before migration. V1's false-Valid list indicator is a known defect and must not be used as verification evidence.

## Uninstall and user data

Uninstall removes the files and shortcuts created by this installer. It does not remove documents saved or extracted to other folders. The per-user preview preference and WebView2 profile/cache are intentionally outside the installation directory and may remain after uninstall. Temporary preview sessions are normally cleaned on document changes and application exit; explicit Keep temporary previews sessions remain by user choice. Stale owned preview sessions older than one day are cleaned at startup. No unrelated folder is cleaned.

See [validation results](VALIDATION.md) for actual test results and any unverified scenario. The existence of an installer upgrade mechanism is not evidence that the original machine-wide v1 upgrade was tested.
