# Release-candidate validation

This document is updated with actual results before release. No public v2.0.0 release has been created.

Automated tests use generated demonstration certificates and harmless content. Coverage includes altered signatures, multiple signers, correct/wrong detached bytes, expired/self-signed certificates, malformed/empty input, resource limits, traversal, collisions, byte equality and preview cleanup.

Manual matrix: attached/altered/multiple/detached signatures; PDF/text/image/unsupported previews; Open dialog; drag/drop; extraction; Save As; restart; temporary cleanup; missing WebView2; long/Unicode names; resizing and DPI; packaged icons; installer fresh install, upgrade and uninstall.

An unchecked matrix item is not a PASS. No claim of a network-isolated host test is made. A safe missing-runtime test must not uninstall WebView2 from the host. The legacy machine-wide installation must not be overwritten merely for testing.
