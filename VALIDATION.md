# Who Scene It 1.1.0 - preview validation

Date: October 25, 2026.

The user confirmed 1.0.0 works. Version 1.1.0 adds explicitly confirmed individual/bulk media deletion. Its source and package are being prepared for hosted CI and authorized publication. Automated live Emby deletion/restart validation remains incomplete.

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

The original 1.0.0 run passed all 16 compiled checks and nine browser checks. Current local outputs are recorded in docs/validation-dotnet.txt and docs/validation-browser.txt.

## Version 1.1.0 checks

- Local release build: zero compiler warnings/errors. Twenty-seven compiled checks passed without the five-minute option; hosted CI must run the 28th real-timer check on the exact release source.
- Fifteen Chromium checks passed, including the real browser polling/cleanup timers. New cases cover individual preview, cross-page bulk selection, both confirmation requirements, cancellation without deletion, a single execution request, progress/report refresh, stale-selection clearing, watched-episode expansion in By title, and disabled in-progress-only entries.
- Real temporary media files verify preview non-destructiveness, confirmed bulk deletion, duplicate/replayed execution, expiry, cancellation, settings preservation, changed-file rejection, concurrent-job exclusion, and honest partial-failure results.
- Native 4.8 SDK objects with injected host/library/user services verify watched-series expansion, administrator policy, content-deletion permission, active playback, native CanDelete, shared-folder/library-root protection, and the actual DeleteOptions passed to ILibraryManager. The SDK protocol service uses a typed test stub because its ReadOnlySpan method cannot be handled by DispatchProxy. No real Emby media/library is used.
- An active file-deletion fixture blocks actual updater Install and Restart calls until the operation finishes.
- The new explicit --published-upgrade mode will be run after publication. It loads the exact public 1.0.0 DLL with a pinned SHA-256 and exercises its updater against the public current feed and asset, then checks isolated new-assembly reload and cleanup. This is not an Emby process restart.

Historical 1.0.0 hosted GitHub Actions validation passed on the exact released source commit 59d6cfe49406446ee76ca52abc5dd40cc8e91d9a: https://github.com/TechSavage1989/whosceneit/actions/runs/40174051730. Windows and Linux each passed all 16 compiled checks, including real timers, actual filesystem rollback, and settings preservation. Linux passed all nine browser checks, including real browser polling and cleanup. Release builds reported zero compiler warnings and errors.

## Update test boundary

The regular 1.1.0 fixture upgrades a copied current DLL to an unpublished test-only next-patch 1.1.1 DLL. HTTP responses are injected into the real updater; it performs real parsing, hashing, assembly inspection, staging, replacement, backup, and cleanup. No download is silently authorized by opening a page. The separate post-publication mode uses real GitHub HTTPS transport and the published previous updater.

The actual .NET timer runs for approximately five minutes; browser timer checks run for approximately one minute. Fake-clock checks are not presented as substitutes for these runs.

## Not yet verified

- Automated comparison against a real Emby library. The user has confirmed that 1.0.0 works.
- Deleting real media through the running Emby server and verifying its actual filesystem permissions, metadata/folder behavior, and native authentication enforcement. Only synthetic temporary files were removed during testing.
- Enforcement of Emby's running authentication filter for anonymous and non-admin users.
- Native Emby restart, visible dashboard reconnection, and behavior when a loaded DLL is locked by Windows.
- Docker/NAS supervision, platform permissions, and crash recovery from a DLL that prevents the plugin from loading.
- A real published-version-to-next-release upgrade. The initial public tag, release assets, and update feed are verified; no second application version has been published for an end-to-end upgrade test.
- Actual cross-volume target filesystems. The installer explicitly copies staging into the target directory before atomic replacement; local tests use separate directories on one filesystem.

## Blocked native integration test

Automatic approval review rejected the isolated Emby startup test because Emby attempted an external public-IP lookup. The test was not bypassed or repeated indirectly. SDK-based and browser-fixture tests continued. This block must be resolved separately before claiming live Emby validation.

## Publication and artifact identity

The repository was confirmed public and contained only its initial README, with no prior releases. The user explicitly approved source and v1.0.0 preview publication on October 25, 2026. The published preview is https://github.com/TechSavage1989/whosceneit/releases/tag/v1.0.0, with immutable tag v1.0.0 at source 59d6cfe49406446ee76ca52abc5dd40cc8e91d9a. All six uploaded assets matched the successful Linux CI artifact byte for byte and were subsequently downloaded from their public versioned URLs and verified against their SHA-256 digests and sizes. The main update feed was activated only after those public checks passed.

Published plugin SHA-256: e2f2e93ee1aa4e3d656bf373cbdc8b5e5fb7bdbca9a6f30dff9f4eb6995ed024. Plugin size: 89,088 bytes. Published assets: WhoSceneIt.dll, WhoSceneIt-v1.0.0-Preview.zip, WhoSceneIt-v1.0.0-Source.zip, update-manifest.json, build-metadata.json, and SHA256SUMS.txt.

Publication recovery: workflow 40174523972 uploaded every asset but failed at the draft's by-tag lookup. The authenticated release list resolved that same draft, while the by-tag endpoint could not. The draft was published through GitHub's interface after comparison with the successful CI artifact; no uploaded asset or public tag was replaced. The release script on main now uses the stable draft ID. The historical publication workflow remains marked failed; hosted application validation and the completed public release are independently verified. Frozen release packages retain the pre-publication notes; current validation and handoff details are maintained on main.

build-metadata.json identifies the source commit and DLL digest. CONTENTS_SHA256.txt verifies the package's contents. External SHA256SUMS.txt verifies each prepared release asset. Matching hashes are integrity checks, not publisher signatures; the plugin is unsigned.
