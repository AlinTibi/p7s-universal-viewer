# P7S Universal Viewer v2.0.0

Open. Verify. Extract. Understand P7S files.

- Redesigned three-panel Windows interface and distinct document/certificate icon.
- Corrected false Valid indicators: every view uses the same checked integrity result.
- Separate certificate chain trust, current dates and revocation status.
- Individual verification of multiple signers and explicit detached-original selection.
- Bounded CMS parsing, safe filenames, collision-safe extraction and preview cleanup.
- Reliable image preview after header inspection, covered by a real decoded-PNG regression test.
- .NET 10 self-contained portable ZIP and per-user Inno installer, with SHA-256 checksums and build provenance.
- Removes archive and Office parsing dependencies; unsupported content remains extractable.

Revocation is not checked. Signing-time assertions and timestamp authority tokens do not establish trusted signing time. Cryptographic integrity does not establish legal validity or document safety. WebView2 is a separate optional dependency for PDF preview. No automatic runtime installation occurs.

Windows EXE and installer are not Authenticode signed. Upgrading a legacy machine-wide installation may require manually uninstalling that old installation; user documents are not removed. The original v1.0.0 release remains unchanged.
