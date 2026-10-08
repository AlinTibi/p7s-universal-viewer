# V2.0.0 validation

The checks below were performed on Windows on 2026-10-08 before publication. V2.0.0 is now publicly released; these results describe that validation session. Synthetic signed fixtures were generated locally; no real customer documents or private signing keys are included in the repository.

Automated tests use generated demonstration certificates and harmless content. Coverage includes altered signatures, multiple signers, correct/wrong detached bytes, expired/self-signed certificates, malformed/empty input, resource limits, traversal, collisions, byte equality and preview cleanup.

## Automated checks

- 31 tests pass, zero failures and zero skipped tests.
- Altered payloads, independent multiple signers, exact/wrong detached bytes, expired/self-signed certificates, malformed/empty input, parsing limits, safe paths, collisions, byte equality and preview cleanup are covered.
- A real PNG preview regression test verifies decoded pixel dimensions and pixel ownership after streams are disposed. Manual validation found a consumed-stream defect; decoding now uses a fresh stream after header inspection.
- Release build succeeds; dependency audit reports no vulnerable packages including transitives.

## Manual application checks

| Scenario | Result |
| --- | --- |
| Attached valid signature and text preview | PASS; valid integrity remains separate from untrusted certificate and unchecked revocation |
| Altered signature | PASS; header, signer list and details consistently show invalid |
| Three signers | PASS; individual signer details and integrity results are available |
| Detached signature without original | PASS; not verified, original required, extraction disabled |
| Wrong / exact detached original | PASS; wrong bytes invalid, exact bytes valid |
| Local PDF preview | PASS with separately installed WebView2; synthetic PDF rendered |
| PNG image preview | PASS after the stream fix |
| Unsupported binary content | PASS; no invented preview, original bytes remain extractable |
| Open dialog, Save As, folder extraction | PASS; saved/extracted 526 bytes match the original SHA-256 |
| Save cancellation | PASS; no extra files or error and existing state remains intact |
| Unicode filename | PASS; Romanian filename and text render correctly |
| Long filename/path | PASS; 229-character filename and 284-character absolute path open, filename wraps in a scrolling pane |
| Restart and temporary cleanup | PASS; Start Menu restart works; PDF preview file/session removed on exit |
| Window icon | PASS; distinct document/shield icon |
| Taskbar / Alt+Tab icons | PASS; confirmed visually by the user |

Windows scaling 125%, 150% and 200% was tested at the native 3840×2160 display resolution. Resolution checks at 100% scaling passed at 1280×720, 1366×768, 1920×1080 and 2560×1440. This is not a claim that every scaling/resolution combination was tested. Original display settings were restored to 3840×2160 / 150%.

## Installer and packages

- Fresh per-user installation: PASS, exit 0; actual installed app opened and verified a valid container from a different working directory.
- Extraction and Save As from the installed app: PASS, exact-byte equality.
- Start Menu shortcut restart: PASS; shortcut targets the installed executable.
- Same-RC reinstall: PASS, exit 0. This does not establish historical v1-to-v2 upgrade behavior.
- Uninstall: PASS, exit 0; installation directory, user Start Menu shortcut and per-user uninstall entry removed.
- Saved/extracted documents and a separate disposable sentinel remain intact after uninstall.
- Historical machine-wide v1 executable remains byte-for-byte unchanged. Its upgrade was not reproduced on the host: v2 intentionally uses a separate per-user scope. See INSTALLATION.md.
- Preview preferences and the WebView2 user-data/cache directory intentionally remain outside installation ownership. Normally cleaned previews are not retained by uninstall; explicit Keep sessions remain by user choice.
- Installer and portable ZIP use the same self-contained publish output. Package checksums are calculated independently before handoff.

## Limits of this validation

The host's WebView2 was not uninstalled. A fully clean missing-runtime host and a fully network-isolated environment were not tested. No security, firewall or proxy settings were weakened. Drag-and-drop is implemented but has not been manually validated in this pass; the real Open dialog and command-line opening were validated.

An unchecked matrix item is not a PASS. No claim of a network-isolated host test is made. A safe missing-runtime test must not uninstall WebView2 from the host. The legacy machine-wide installation must not be overwritten merely for testing.
