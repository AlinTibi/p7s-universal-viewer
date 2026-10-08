# Installation and migration

V2.0.0 remains a release candidate. Installer and portable packages are built from the same self-contained Windows x64 publish directory; no separate .NET installation is needed.

The installer installs under the current user's local application data, creates a Start Menu shortcut, and optionally creates a desktop shortcut. It requires no administrator privileges. PDF preview uses the separately installed Microsoft Edge WebView2 Runtime; no runtime is downloaded or installed automatically.

## Historical v1 installations

The original v1 installer used a machine-wide Program Files directory. V2 deliberately uses per-user installation. It does not silently migrate or overwrite that machine-wide installation. Both may coexist. If you choose to remove v1, use its existing Windows uninstall entry; administrative approval may be required. Preserve your signed source documents before migration. V1's false-Valid list indicator is a known defect and must not be used as verification evidence.

## Uninstall and user data

Uninstall removes the files and shortcuts created by this installer. It does not remove documents saved or extracted to other folders. The per-user preview preference and WebView2 profile/cache are intentionally outside the installation directory and may remain after uninstall. Temporary preview sessions are normally cleaned on document changes and application exit; explicit Keep temporary previews sessions remain by user choice. Stale owned preview sessions older than one day are cleaned at startup. No unrelated folder is cleaned.

See [validation results](VALIDATION.md) for actual test results and any unverified scenario. The existence of an installer upgrade mechanism is not evidence that the original machine-wide v1 upgrade was tested.
