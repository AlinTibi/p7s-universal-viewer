# Security

Report vulnerabilities privately to security@almarfeld.com. Include version, reproduction steps and a synthetic sample where possible. Do not submit private signed documents, keys or certificates in public issues.

The 2.0.0 preparation separates signature integrity, certificate trust, current dates and revocation. Revocation and timestamp authority validation are not performed. Integrity is not a legal signature opinion or a malware scan. A valid signature may sign dangerous content.

The legacy v1.0.0 signer list can incorrectly show Valid for altered content. Its historical installer is preserved, but that indicator must not be used as verification evidence.

Download from this repository, verify checksums, and inspect the source. Checksums alone do not establish publisher identity. Windows binaries are currently not Authenticode signed.
