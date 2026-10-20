# P7S Universal Viewer v2.0.1

Security maintenance release for the existing P7S Universal Viewer v2 series.

- Updates Microsoft's `System.Security.Cryptography.Pkcs` from 10.0.12 to 10.0.13, patched for the October 2026 advisories (GHSA-846f-f764-76mx and GHSA-2fmv-rv2c-59x7).
- Keeps the redesigned three-panel Windows interface, detached-content support, per-signer checks, original-byte extraction and protective handling introduced in v2.0.0.
- Aligns the legacy GitHub Pages homepage with the canonical ALMARFELD P7S product page.
- Includes a self-contained Windows portable ZIP and per-user installer, with SHA-256 checksum files and workflow provenance; these artifacts must be rebuilt from the v2.0.1 signed tag.

This patch does not add revocation checking or timestamp authority verification. Cryptographic integrity is not proof of legal validity or content safety. WebView2 is separate and optional for PDF preview. Windows installer and EXE are not Authenticode signed. The original historical releases remain available and unchanged; use v2.0.1 instead of v2.0.0 once it is published.
