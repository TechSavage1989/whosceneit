# Who Scene It 1.2.2 - preview validation

Date: October 25, 2026.

Version 1.2.2 repairs deletion preview/error feedback and adds explicit confirmation/progress/results. Hosted exact-source CI, publication, and public upgrade verification are pending. The user's HTTP 500 preview response did not expose its underlying server exception; no claim of live deletion success is made.

## Version 1.2.2 checks

- Local Release builds have zero warnings/errors. All 48 quick compiled checks pass; hosted real-timer validation should run 49 on each platform.
- New service tests invoke actual Preview/Execute/Status/Cancel methods with native SDK IRequest and AuthorizationInfo/User objects. They verify administrator binding, rejection of API-key-only/non-admin/missing requests, read-only preview, typed confirmation, temporary-file deletion, real status polling, ownership, and replay safety.
- A held deletion worker verifies Started, Processing, Processed, CurrentItemName, queued counts, cancellation, and final counts. An inspection exception blocks that item and retains the rest of the review; preview never invokes native deletion.
- Twenty-one quick Chromium checks pass; hosted Linux must pass 23 including real timers. Native browser Fetch Response errors are decoded as safe text. Tests cover structured preview failure, no-start messaging, read-only retry with fresh confirmation, all-blocked previews, live progress, loss of the execution response and a status response, recovery without replay, and final results.
- After publication, verify all assets/tag/source/feed and run the actual public 1.2.1-to-1.2.2 memory-loaded SDK updater check. No native Emby startup or real-library deletion is attempted.

## Historical 1.2.1 validation

Version 1.2.1 repairs an unscoped report-query regression and adds useful error details. Exact-source hosted CI, all public assets/feed, and public upgrade verification passed. Publication completed before the workflow timed out on its final feed check; independent verification confirmed the canonical feed afterward. The supplied screenshot contains a generic failed-request message rather than a server exception; the exact live stack trace has not been observed.

## Version 1.2.1 checks

- Negative control: Emby's 4.8 SDK creates AncestorIds as an empty array. The 1.2.0 normal report replaces it with null and fails the new regression assertion. See docs/validation-report-query-negative.txt. This detects an invalid query-default change; it does not execute a native Emby database query or claim to reproduce the unseen live exception.
- Corrected local Release build has zero warnings/errors and passes all 43 quick compiled checks. The new case covers default ancestor arrays in Titles, Movies, Shows, and Episodes. Existing tests retain scoped series IDs, watch progress, updater, and deletion behavior. Hosted real-timer validation passed all 44 checks on Windows and Linux.
- Twenty-one browser checks pass, including real timers. The new case exercises structured native error messages and HTTP status as safe text, literal HTML that never executes, omitted stack traces, non-JSON error fallback, 403 guidance, failed-report health prevention, and successful recovery. Hosted Linux passed all 21 checks on the exact release source.
- After publication, the public 1.2.0-to-1.2.1 memory-loaded entry-point installation/health/cleanup check passed, including the legacy 1.0.0 failure/manual-bootstrap negative control. Settings, custom filename, rollback bytes, and staging cleanup were verified. This does not start or restart live Emby.

## Historical 1.2.0 validation

Version 1.2.0 adds episode watch progress and related controls. Exact-source hosted CI, publication, all public assets/feed, and the public previous-version upgrade check passed. Live Emby deletion/restart testing remains outside the completed SDK/browser checks.

## Version 1.2.0 checks

- Local Release builds pass without warnings/errors. Forty-two compiled checks cover the existing reports/updater/deletion plus eight new progress/report checks and one native cross-user resume protection check. Hosted Windows and Linux each passed all 43 checks, including the real five-minute timer.
- Completion flags, partial positions, manual marks, watched-with-resume states, missing/negative durations, clamped overrun positions, all watch-state filters, full show denominators, not-started viewer inclusion, numeric episode order, and progress sorting are covered.
- Native 4.8 SDK report fixtures exercise below-threshold positions, runtime reads, the unfiltered saved-data query, series-scoped episode lists, full series totals, viewer progress, and invalid report options.
- Native deletion fixtures block saved positions for the selected or another user, including progress appearing after preview; original media remains present and native deletion is not called.
- Twenty Chromium checks pass, including real browser timers. New cases cover episode drilldown/progress, scoped partial CSV, watched-with-resume protection, all/completed/not-started filters, clearing the scope, and selecting a viewer's episode list. A mobile overflow caused by a long option label was reproduced and fixed; the responsive-width check passes. Hosted Linux passed all 20 checks on the exact released source.
- After publication, the public helper passed both the legacy empty-path failure/manual bootstrap and genuine public 1.1.1-to-1.2.0 installation using actual memory-loaded entry points. It used real HTTPS feed/downloads and verified exact bytes, the custom installed filename, preserved settings/backup, pending health verification, and staging cleanup. See docs/validation-published-upgrade.txt. This is not a live Emby restart.

## Historical 1.1.1 validation

The user confirmed 1.0.0 reports work, then reported an empty installation-path error while updating to 1.1.0. Version 1.1.1 repairs byte-loaded plugin path discovery and retains individual/bulk deletion. Exact-source hosted CI, publication, all six public assets, the matching feed, and the public memory-load/manual-bootstrap check passed. Affected older installations require one manual DLL replacement. Automated live Emby deletion/restart validation remains incomplete.

## Version 1.1.1 checks

- Release build: zero compiler warnings/errors; 33 local quick compiled checks passed. Hosted Windows and Linux each passed all 34 checks, including the real five-minute timer, on exact source c29b1b94644168ec2ce63e359735fd244528fc3f. Linux passed all 15 browser checks, including real polling and cleanup timers.
- Actual DLLs loaded using AssemblyLoadContext.LoadFromStream have an empty Assembly.Location. The previous Hash(Location) code reproduces ArgumentException with parameter path, matching the user screenshot.
- Actual memory-loaded Plugin and UpdateEntryPoint instances use native SDK SetAttributes and host plugin metadata. They resolve the registered custom filename, support attributes registered after entry-point construction, and support a bare filename under PluginsPath.
- Unique plugins-directory fallback succeeds; duplicate WhoSceneIt installations, wrong assembly identity/version, missing files, and shadow-cache locations are rejected. Negative cases preserve target bytes.
- A lazy path provider is used by real download/staging/replacement with injected HTTP. The exact custom target is replaced, the old DLL and settings are preserved, and a newly memory-loaded entry point verifies the pending digest/version before dashboard acknowledgement cleans staging.
- The revised explicit public check passed after publication. The exact public 1.0.0 entry point loaded from bytes downloaded and verified the real public 1.1.1 DLL, then reproduced the empty-path install failure without changing the target. Simulated manual replacement loaded the new public entry point from bytes, resolved the custom target, preserved settings/backup, and cleaned staging after acknowledgement. The installed SHA-256 matched the release. See docs/validation-published-upgrade.txt. The broken old updater cannot install its own fix; no live Emby process was started or restarted.
- No browser UI changed. The required hosted Linux job passed all 15 browser checks, including real timers.

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

- Local release build: zero compiler warnings/errors. Twenty-eight local compiled checks passed without the five-minute option. Hosted Windows and Linux each passed all 29 checks, including the actual five-minute timer, on exact source 1d1d221b2b5fbc6e6f4ef0cebac8880115e09ccd.
- Fifteen Chromium checks passed, including the real browser polling/cleanup timers. New cases cover individual preview, cross-page bulk selection, both confirmation requirements, cancellation without deletion, a single execution request, progress/report refresh, stale-selection clearing, watched-episode expansion in By title, and disabled in-progress-only entries.
- Real temporary media files verify preview non-destructiveness, confirmed bulk deletion, duplicate/replayed execution, expiry, cancellation, settings preservation, changed-file rejection, concurrent-job exclusion, and honest partial-failure results.
- Native 4.8 SDK objects with injected host/library/user services verify watched-series expansion, administrator policy, content-deletion permission, active playback, native CanDelete, shared-folder/library-root protection, and the actual DeleteOptions passed to ILibraryManager. The SDK protocol service uses a typed test stub because its ReadOnlySpan method cannot be handled by DispatchProxy. No real Emby media/library is used.
- An active file-deletion fixture blocks actual updater Install and Restart calls until the operation finishes. Background feed checks remain allowed during deletion. A negative-control run restored the previous broad Busy guard and reproduced the failure under an actual in-flight check; the corrected guard passes the same test. Native playback matching is checked with numeric and GUID item IDs.
- Updater timing negative control: the previous documentation/release-tool commit's Windows job failed the real timer check at https://github.com/TechSavage1989/whosceneit/actions/runs/40174933095. Its five-minute timer also applied a second full five-minute wall-clock cooldown, allowing an early tick to be dropped for the next interval. Version 1.1.0 retains the server-owned five-minute timer and uses the short cross-trigger cooldown for that callback; focus gating and Retry-After/backoff are unchanged. Final hosted real-timer validation passed on both platforms for the corrected source.
- The historical --published-upgrade mode passed public 1.0.0-to-1.1.0 transport and file replacement with a target path supplied directly to UpdateManager. It bypassed the actual entry point, so it missed the empty Assembly.Location failure reported by the user. It did not verify native Emby upgrade behavior. The current helper is rewritten to reproduce this defect and check the one-time manual bootstrap after patch publication.

Historical 1.0.0 hosted GitHub Actions validation passed on the exact released source commit 59d6cfe49406446ee76ca52abc5dd40cc8e91d9a: https://github.com/TechSavage1989/whosceneit/actions/runs/40174051730. Windows and Linux each passed all 16 compiled checks, including real timers, actual filesystem rollback, and settings preservation. Linux passed all nine browser checks, including real browser polling and cleanup. Release builds reported zero compiler warnings and errors.

## Update test boundary

The regular 1.1.1 fixture upgrades a copied current DLL to an unpublished test-only next-patch 1.1.2 DLL. The old source's 1.1.1 fixture is not this patched application and must never be published. HTTP responses are injected into the real updater; it performs real parsing, hashing, assembly inspection, staging, replacement, backup, and cleanup. No download is silently authorized by opening a page. The separate post-publication mode uses real GitHub HTTPS transport and actual memory-loaded entry points, with a negative control for the previous updater and explicit manual bootstrap for the patch.

The actual .NET timer runs for approximately five minutes; browser timer checks run for approximately one minute. Fake-clock checks are not presented as substitutes for these runs.

## Not yet verified

- Automated comparison against a real Emby library. The user has confirmed that 1.0.0 works.
- Deleting real media through the running Emby server and verifying its actual filesystem permissions, metadata/folder behavior, and native authentication enforcement. Only synthetic temporary files were removed during testing.
- Enforcement of Emby's running authentication filter for anonymous and non-admin users.
- Native Emby restart, visible dashboard reconnection, and behavior when a loaded DLL is locked by Windows.
- Docker/NAS supervision, platform permissions, and crash recovery from a DLL that prevents the plugin from loading.
- A native Emby process upgrade/restart/reconnect. The historical public 1.0.0-to-1.1.0 file fixture supplied a target directly and missed the actual path failure. Old byte-loaded installations need manual replacement once.
- Actual cross-volume target filesystems. The installer explicitly copies staging into the target directory before atomic replacement; local tests use separate directories on one filesystem.

## Blocked native integration test

Automatic approval review rejected the isolated Emby startup test because Emby attempted an external public-IP lookup. The test was not bypassed or repeated indirectly. SDK-based and browser-fixture tests continued. This block must be resolved separately before claiming live Emby validation.

## Historical 1.0.0 publication and artifact identity

The repository was confirmed public and contained only its initial README, with no prior releases. The user explicitly approved source and v1.0.0 preview publication on October 25, 2026. The published preview is https://github.com/TechSavage1989/whosceneit/releases/tag/v1.0.0, with immutable tag v1.0.0 at source 59d6cfe49406446ee76ca52abc5dd40cc8e91d9a. All six uploaded assets matched the successful Linux CI artifact byte for byte and were subsequently downloaded from their public versioned URLs and verified against their SHA-256 digests and sizes. The main update feed was activated only after those public checks passed.

Published plugin SHA-256: e2f2e93ee1aa4e3d656bf373cbdc8b5e5fb7bdbca9a6f30dff9f4eb6995ed024. Plugin size: 89,088 bytes. Published assets: WhoSceneIt.dll, WhoSceneIt-v1.0.0-Preview.zip, WhoSceneIt-v1.0.0-Source.zip, update-manifest.json, build-metadata.json, and SHA256SUMS.txt.

Publication recovery: workflow 40174523972 uploaded every asset but failed at the draft's by-tag lookup. The authenticated release list resolved that same draft, while the by-tag endpoint could not. The draft was published through GitHub's interface after comparison with the successful CI artifact; no uploaded asset or public tag was replaced. The release script on main now uses the stable draft ID. The historical publication workflow remains marked failed; hosted application validation and the completed public release are independently verified. Frozen release packages retain the pre-publication notes; current validation and handoff details are maintained on main.

build-metadata.json identifies the source commit and DLL digest. CONTENTS_SHA256.txt verifies the package's contents. External SHA256SUMS.txt verifies each prepared release asset. Matching hashes are integrity checks, not publisher signatures; the plugin is unsigned.

## Verified 1.1.0 publication

- Source/tag: 1d1d221b2b5fbc6e6f4ef0cebac8880115e09ccd, v1.1.0.
- Hosted Windows/Linux validation: https://github.com/TechSavage1989/whosceneit/actions/runs/40178145566, successful; 29 compiled checks per platform and 15 Linux browser checks.
- Publication workflow: https://github.com/TechSavage1989/whosceneit/actions/runs/40178653703, successful.
- Six public assets independently downloaded and size/SHA-256 verified. All 41 source ZIP files match the exact tagged source; all 47 preview content hashes pass. Build metadata matches that source and records a clean tree.
- WhoSceneIt.dll: 126,976 bytes, SHA-256 2db640f20e5aca376a1d955a7ef8a8466bd60cddafeb7ca90b981f20fd9a55b6.
- The public main feed matches the 1.1.0 release manifest, URL, version, and digest. Neither published release/tag was overwritten.
- The exact public 1.0.0 updater passed real HTTPS download and file replacement into an explicitly supplied temporary target. This historical fixture bypassed UpdateEntryPoint and missed the byte-loaded empty-path defect. No real user media was deleted and no live Emby restart was performed.

## Verified 1.1.1 publication

- Source/tag: c29b1b94644168ec2ce63e359735fd244528fc3f, v1.1.1.
- Hosted Windows/Linux validation: https://github.com/TechSavage1989/whosceneit/actions/runs/40180285235, successful; 34 compiled checks per platform and 15 Linux browser checks.
- Publication workflow: https://github.com/TechSavage1989/whosceneit/actions/runs/40180822733, successful. Public release: https://github.com/TechSavage1989/whosceneit/releases/tag/v1.1.1.
- Six public assets independently downloaded and verified against API sizes/digests and SHA256SUMS.txt. All 44 source files match the exact tagged source; all 50 preview content hashes pass. Build metadata matches the tag and records a clean source tree.
- WhoSceneIt.dll: 131,584 bytes; SHA-256 9c79efe59c167cc7667cfa0fb85926bb532090b3dabd0ae7fb80e31e09949aad.
- The public main feed exactly matches the public release manifest, URL, version, digest, and size. Feed commit: 4251cc01a912521b5582d821db765371b95d6073. Existing releases/tags/assets were not overwritten.
- The real public 1.0.0 memory-loaded entry point reproduced the reported empty-path installation failure. One-time manual bootstrap into the public 1.1.1 memory-loaded entry point passed path/settings/backup/cleanup checks. This corrects the earlier explicit-target fixture's coverage gap. No real user media was deleted and no live Emby restart was performed.

## Verified 1.2.0 publication

- Source/tag: e2be637d3e333d14fb6ae6e6a36c2b95dca59aaf, v1.2.0.
- Hosted Windows/Linux validation: https://github.com/TechSavage1989/whosceneit/actions/runs/40213700764, successful; 43 compiled checks per platform and 20 Linux browser checks.
- Publication workflow: https://github.com/TechSavage1989/whosceneit/actions/runs/40214340337, successful. Public release: https://github.com/TechSavage1989/whosceneit/releases/tag/v1.2.0.
- Six public assets independently downloaded and verified against API sizes/digests and SHA256SUMS.txt. All 46 source files match the exact tagged source; all 52 preview content hashes pass. Build metadata matches the tag and records a clean source tree.
- WhoSceneIt.dll: 141,824 bytes; SHA-256 ab828ce02eff35e632ca6605c0b583eede2c0021fb74374a7740f243812a7661.
- The public feed exactly matches the public manifest, version, URL, digest, and size. Feed commit: 50b6837bebafb76991a9c60e258fbbd3eba41cfb. Previous releases/tags/assets remain unchanged.
- The pinned public 1.1.1 DLL, loaded from bytes through its real entry point, installed the public 1.2.0 DLL. The new memory-loaded entry point verified pending health and cleaned owned staging after acknowledgement; settings/custom filename/rollback were preserved. The older 1.0.0 path failure/manual bootstrap also passed as a negative control. No real user media was deleted and no live Emby process was started or restarted.

## Verified 1.2.1 publication

- Source/tag: c68e1714c67c488adac12dda8b7c87e08b3f9126, v1.2.1.
- Hosted validation: https://github.com/TechSavage1989/whosceneit/actions/runs/40216607262, successful; 44 compiled checks per platform and 21 Linux browser checks.
- Public release: https://github.com/TechSavage1989/whosceneit/releases/tag/v1.2.1. Six assets independently downloaded and checked against API sizes/digests and SHA256SUMS.txt; all 47 source files match the exact tagged tree and all 53 preview content hashes pass. Metadata records that source and a clean tree.
- WhoSceneIt.dll: 142,336 bytes; SHA-256 16d75ae5b603e61545d9555412a86093fe03ecf51a3c9cce23e326d5b93bc8f2.
- Feed commit: 32d8d1b82d20982a4943d3f8d081a10a37042644. A direct canonical raw.githubusercontent.com HTTPS request independently matched the public manifest, URL, version, digest, and size.
- Publication workflow https://github.com/TechSavage1989/whosceneit/actions/runs/40217267071 remains marked failed because its one-minute final feed-verification window expired after assets were published/verified and the feed was advanced. Subsequent canonical-feed verification and the public updater test passed. No tag or asset was overwritten, and publication was not rerun. Main's future publication check now permits up to six minutes of retries; this does not change the frozen 1.2.1 package.
- The pinned actual public 1.2.0 updater installed the public 1.2.1 binary through its real memory-loaded entry point in SDK fixtures. New-entry-point pending version/digest verification and simulated dashboard acknowledgement/cleanup passed with settings/custom filename/rollback preserved. See docs/validation-published-upgrade.txt. No live Emby process was started/restarted and no real user media was deleted.
