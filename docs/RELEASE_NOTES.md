# P7S Universal Viewer v2.1.0

- Verify up to 100 CMS/PKCS#7 containers in one batch. Select a result to supply the exact original for a detached signature or open its detailed view.
- When a same-folder original filename matches the signature name, the file picker suggests it for confirmation. Verification checks the selected bytes.
- Save a JSON report for one file or a batch with document SHA-256, UTC check time and per-signer results. Reports include filenames and certificate details, without document bytes or absolute input paths.
- View the certificate chain Windows built locally, including chain and certificate status. On-screen guidance distinguishes signature integrity from certificate trust and dates.
- Includes the System.Security.Cryptography.Pkcs security update prepared in v2.0.1.

Revocation and timestamp authority tokens are not checked. The report records the application's findings and does not establish legal validity. WebView2 is optional for PDF preview and is installed separately. Windows executables and the installer are unsigned.
