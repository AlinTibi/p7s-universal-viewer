# Changelog

## 2.0.1 — security maintenance (pending release)

- Updates System.Security.Cryptography.Pkcs to the patched 10.0.13 version, addressing the October 2026 .NET advisories GHSA-846f-f764-76mx and GHSA-2fmv-rv2c-59x7.
- Keeps the existing P7S viewer, verification and extraction behavior; the 31-test suite and dependency audit pass on the updated dependency.
- Points the legacy GitHub Pages homepage's canonical and software identity to the official ALMARFELD product page.
- Requires a newly built and signed-tagged v2.0.1 release. Existing v2.0.0 installers are not modified by this source change.

## 2.0.0 — released 2026-10-08

- Separates integrity, certificate trust, dates and revocation with one authoritative status model.
- Checks every signer and supports detached signatures with explicit original-file selection.
- Adds bounded CMS inspection, collision-safe extraction and owned temporary-preview cleanup.
- Decodes image previews from a fresh stream after header inspection.
- Introduces a three-panel WPF interface and a distinct document/certificate icon.
- Uses .NET 10 and current cryptography/WebView2 packages; removes SharpCompress and Office parsing dependencies.
- Prepares self-contained portable ZIP, Inno installer, checksums and provenance workflow.

Revocation remains not checked; timestamp authority tokens are not verified. Cryptographic success does not imply legal validity. Windows packages are not Authenticode signed.

## 1.0.0

Original public installer and GitHub Pages content. Historical downloads and tag are retained unchanged.
