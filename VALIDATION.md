# Who Scene It 1.0.0 - preview validation

Date: October 25, 2026.

This is a locally built preview for review and an initial Emby installation test. It is not a verified public release. No user-confirmed working Emby baseline exists yet.

## Completed checks

- Release build against the official Emby 4.8.0.80 SDK, targeting netstandard2.0, with zero compiler warnings or errors.
- Watch flags, manual/imported marks without dates or play counts, incomplete playback, grouped shows, duplicate records, same-name shows, episode ranges, UTC dates, filtering, and sorting.
- Native Admin authentication attributes on every report and update endpoint; update mutations accept POST. This checks the actual compiled route declarations, not Emby's running authentication pipeline.
- Strict manifest identity/version/repository checks and allowed download/redirect URL rules. Negative controls reject other applications, untrusted URLs, invalid hashes, oversized assets, wrong assembly versions, executable payloads, and bad downloaded bytes.
- Real DLL download/staging/install file operations with injected HTTP responses, including a custom target filename, one backup, settings preservation, playback/scan/scheduled-task blocking, and restart-required state.
- Failure injection after the original DLL is removed, followed by actual filesystem restoration and original-byte verification.
- Pending-install version/digest verification using a reload fixture; staging survives until dashboard health acknowledgement, then app-owned files are cleaned without changing settings. This does not constitute a native Emby restart.
- Single-flight requests, offline errors, Retry-After/backoff, and a manual-check cooldown.
- A real five-minute server timer, conditional requests with ETag, and HTTP 304 reuse passed after correcting a startup ordering issue. The negative control scheduled the timer before the startup check and missed its first conditional poll.
- Chromium browser checks with synthetic Emby data: both report views, paging, all 205 exported rows, CSV formula neutralization, safe text rendering, quick user changes, explicit update actions, restart warning, focus subscriptions, mobile layout, and no browser exceptions.
- Real browser status polling and timer/listener cleanup when leaving the plugin page. Nine browser checks passed.
- Release script syntax and workflow YAML syntax. Publication is gated on successful hosted Windows and Linux CI for the exact source commit.

The extended run passed all 16 compiled checks, including the two-minute focus gate and real manual-cooldown expiry. Nine browser checks passed. The complete outputs are recorded in docs/validation-dotnet.txt and docs/validation-browser.txt.

## Update test boundary

The update fixture upgrades a copied 1.0.0 DLL to an unpublished test-only 1.0.1 DLL. HTTP responses are injected into the real updater; it performs real parsing, hashing, assembly inspection, staging, replacement, backup, and cleanup. No download is silently authorized by opening a page.

The actual .NET timer runs for approximately five minutes; browser timer checks run for approximately one minute. Fake-clock checks are not presented as substitutes for these runs.

## Not yet verified

- Loading the plugin into Emby and comparing reports against a real library.
- Enforcement of Emby's running authentication filter for anonymous and non-admin users.
- Native Emby restart, visible dashboard reconnection, and behavior when a loaded DLL is locked by Windows.
- Docker/NAS supervision, platform permissions, and crash recovery from a DLL that prevents the plugin from loading.
- Hosted GitHub Actions, public tags/releases/assets, the live feed, and a real published-version-to-next-release upgrade.
- Actual cross-volume target filesystems. The installer explicitly copies staging into the target directory before atomic replacement; local tests use separate directories on one filesystem.

## Blocked native integration test

Automatic approval review rejected the isolated Emby startup test because Emby attempted an external public-IP lookup. The test was not bypassed or repeated indirectly. SDK-based and browser-fixture tests continued. This block must be resolved separately before claiming live Emby validation.

## Publication and artifact identity

The repository was confirmed public and contained only its initial README, with no prior releases. The user explicitly approved source and v1.0.0 preview publication on October 25, 2026. Hosted CI and publication verification are in progress. The package includes the DLL, source, tests, scripts, CI definitions, installation instructions, handoff notes, a proposed release manifest, content hashes, and build metadata. The proposed manifest points at the intended release and must not be treated as a currently hosted working feed until publication is verified.

build-metadata.json identifies the source commit and DLL digest. CONTENTS_SHA256.txt verifies the package's contents. External SHA256SUMS.txt verifies each prepared release asset. Matching hashes are integrity checks, not publisher signatures; the plugin is unsigned.
