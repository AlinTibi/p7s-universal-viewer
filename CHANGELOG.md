# Changelog

## 2.0.0 — unreleased

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
