# Who Scene It 1.5.2 - preview validation

Date: October 25, 2026.

Version 1.5.2 adds a visible saved-rule list and verifies complete rule preservation across updates, restarts, health cleanup, and failed replacement rollback. Existing schema-1 rule fields are unchanged. Local Release builds passed with zero warnings/errors and all 86 quick compiled checks. All 42 browser checks passed, including real timers. Required exact-source hosted validation and publication are pending.

## Version 1.5.2 checks

- The frozen schema-1 fixture contains three complete rules: enabled review/automatic modes, a disabled foreign-owner rule, every eligibility/schedule/trigger setting, IDs/revisions/owners, next/last evaluation, protection, a pending review, an automation hold, and history. Full snapshots and both primary/backup files survive memory-loaded update installation, new-DLL deserialization, and dashboard-health cleanup. An injected replacement failure restores the DLL without changing rule files.
- A newly constructed coordinator reports two enabled/one disabled saved rules accurately and runs the existing due review schedule without changing either other rule or starting automatic deletion. Unsupported schema and missing-primary-with-backup fixtures fail closed without replacing available saved data.
- Browser checks cover the always-visible empty/populated lists, exact metadata for enabled/disabled/foreign-owner rules, literal HTML names, owner-only edit/evaluate routing, editor values and fresh consent, failed refresh retaining rules/input with disabled actions, and recovery with no policy save/reset. Existing reports/protection/deletion/synchronization, mobile layout, and updater tests also pass.
- Persistence schema and rule fields are unchanged. The test-only next-patch DLL is 1.5.3, never a release asset. Post-publication checks pin actual public 1.5.1 and load the full fixture with both public versions.
- Only SDK/temp-file/synthetic HTTP/browser fixtures run. Live Emby/Sonarr/Radarr deletion, native event delivery, and live loaded-DLL replacement remain unverified. No real user server or media is connected.

## Historical 1.5.1 baseline

Version 1.5.1 exposes unwatched media for individual and bulk Never delete protection without enabling unfinished deletion. All watch states are shown by default. Local Release builds passed with zero warnings/errors and all 84 quick compiled checks. All 39 browser checks passed, including real timers and timer cleanup. Exact-source hosted Windows/Linux each passed all 85 compiled checks, and Linux passed all 39 browser checks. Publication and independent asset/source/tag/feed/updater verification passed; no live user server or media is connected.

## Version 1.5.1 checks

- Native SDK inventory tests cover wholly unwatched shows and scoped episodes under All and NotStarted filters, without adding a played/resumable query restriction or changing deletion eligibility. The real Admin protection endpoint can protect an unwatched movie without changing its flag or allowing opted-in deletion.
- Browser tests show unwatched movies/shows by default, mark them individually and by page, and protect unwatched episodes in bulk while unfinished deletion remains off. Those protection flows issue no deletion request.
- Mixed selections admit every unprotected watch state for protection; default deletion sends only eligible completed selection IDs. Existing cross-page selection, context resets, exact previews/checkbox confirmation, synchronization, rules, inherited protection, errors, CSV, mobile layout, and updater behavior also pass.
- The 1.5.1 resource rejects cached older HTML missing the new help control. The historical 1.5.1 source's next-patch 1.5.2 fixture is not the actual 1.5.2 patch, and is never a release asset. The public-upgrade test pins actual public 1.5.0 for post-publication checks.
- Tests use SDK, temporary-file, synthetic HTTP, and browser fixtures. Live Emby event delivery, native deletion, Sonarr/Radarr deletion, and loaded-DLL Windows replacement remain unverified.
- Exact source/tag: 5dbdd281ec2eae24534a7254892609e097b8fd4f, v1.5.1. Hosted validation: https://github.com/TechSavage1989/whosceneit/actions/runs/40238732732. Publication: https://github.com/TechSavage1989/whosceneit/actions/runs/40239366591. Both completed successfully.
- Independently downloaded and verified all six public assets against API sizes/digests and SHA256SUMS, clean build metadata, all 61 source files/67 preview hashes, the immutable source tag, and exact canonical feed. Feed commit: 2472c50029e0d7182fe39c9bbd7e088afa101484. Public DLL: 275,968 bytes; SHA-256 2b41ba8bf41bca7b712c544fed3344839ffadbf43aa8d0fcb04f6805ec9c55a9. No existing release asset/tag was overwritten.
- The pinned actual public 1.5.0 updater, loaded from bytes, installed exact public 1.5.1 bytes using the real entry point and HTTPS feed/download. The custom filename, settings, never-delete/cleanup state, rollback, pending-health verification, and staging cleanup passed. The legacy 1.0.0 failure/manual-bootstrap control passed too. See docs/validation-published-upgrade.txt. No live Emby process was started or restarted.

## Historical 1.5.0 baseline

Version 1.5.0 adds persistent Never delete protection, cleanup rules, and optional empty Sonarr show removal. The user explicitly requested native automatic cleanup and publication. Rules remain disabled until configured; no real target server or user media is connected during development. Exact-source hosted CI, publication, all public asset/tag/source/feed checks, and the actual public 1.4.0-to-1.5.0 upgrade check passed. Exact source/tag: 45d198dd972d8c995f4a63589c62c5ba88c3afd5, v1.5.0. Validation: https://github.com/TechSavage1989/whosceneit/actions/runs/40235405080. Publication: https://github.com/TechSavage1989/whosceneit/actions/runs/40236020877.

## Version 1.5.0 checks

- Local Release builds have zero warnings/errors. The quick compiled suite passed 83 checks; exact-source hosted Windows/Linux each passed all 84, including the existing real five-minute updater timer, with zero build warnings/errors.
- Fourteen new retention checks plus native inventory and pre-start failure coverage exercise persistent/global/inherited protection; exact path boundaries; reindexed files; late protection; completed-with-resume versus another user's unfinished progress; native completion/date/full-show inventory; corrupt/unwritable state; UTC boundaries; disabled/review defaults; checkbox authorization; real request-bound Admin/API-key rejection; rule ownership; fresh preview/journaling; schedule consumption; trigger coalescing/cooldowns; rule changes/disable; fresh watch-state rechecks; unknown native outcomes; restart recovery; and explicit hold release. All files are synthetic.
- Three added Sonarr checks cover the opt-in empty-series mode, the default retained series, remote/native/incoming file preservation, deleteFiles=false, frozen setting changes, lost receipts without replay, and partial cleanup results. These use synthetic HTTP handlers, never real Sonarr/Radarr.
- The complete local browser suite and exact-source hosted Linux each passed all 36 including real timers. New checks cover global/inherited protection, unfinished media marking, bulk protection without deletion, optional Sonarr setting persistence, rule editor UTC scope/triggers/authorization reset, read-only prompt/retry/checkbox flow, disabling rules, hold release, protection lookup failure, and mobile layout.
- Persistence uses atomic retention.json replacement/backup. Pre-mutation journals retain uncertain attempts across restarts, with old in-memory operation tokens cleared. No native media or service mutation is automatically replayed after interruption. Rule changes and saved protection are revalidated before each item; a running item may finish.
- Validation uses the real Emby SDK and temporary files/HTTP/browser fixtures. Actual Emby event delivery, server-native task timing/authentication, loaded DLL replacement, and real Sonarr/Radarr deletion remain unverified. Read-back checks do not create an atomic transaction across services; files arriving after the last emptiness check cannot be locked by the plugin, and Sonarr series deletion always disables additional file deletion.
- Primary interface references checked during implementation: https://dev.emby.media/reference/pluginapi/MediaBrowser.Controller.Library.IUserDataManager.html, https://dev.emby.media/reference/pluginapi/MediaBrowser.Controller.Library.UserDataSaveEventArgs.html, https://dev.emby.media/reference/pluginapi/MediaBrowser.Controller.Session.ISessionManager.html, and Sonarr's own SeriesController DeleteSeries API. The compiler verifies the pinned official 4.8.0.80 SDK event shapes.
- A local browser test sampled a stale row during refresh; the actual bulk request protected the correct two IDs. The test now waits for the matching report, and row actions are disabled while refreshing. Incomplete local processes are not counted as successful suites; both runners require completion markers. Failed/timed-out attempts are superseded only by a complete passing rerun.
- Independently verified all six public assets against API digests and SHA256SUMS, tag/source, clean metadata, all 61 source files/67 preview hashes, and the exact canonical feed. Public DLL: 274,944 bytes; SHA-256 014f6825803704453e657edaf5521c66435894cf38274f730280f0abdf879a4d. Feed commit: 63307d04e8f3572e317fbf45fadd92a95c176f75. The historical 1.5.0 source's 1.5.1 fixture remains test-only and is not the actual 1.5.1 patch.
- The pinned actual public 1.4.0 memory-loaded updater installed exact public 1.5.0 bytes and verified custom filename, settings, never-delete/cleanup-state preservation, backup, pending-health reload, and staging cleanup. The legacy public 1.0.0 failure/manual-bootstrap control also passed. See docs/validation-published-upgrade.txt. No real Emby process or user media was used.

## Historical 1.4.0 validation

Version 1.4.0 adds explicit unfinished/unwatched deletion scope and checkbox confirmation. The user directly requested these changes; default watch/resume protection remains. Exact-source hosted CI, publication, all public asset/source/feed checks, and the actual public 1.3.0-to-1.4.0 updater check passed. Exact source/tag: 33020fd765433302b45f5b487ed473e9bab41ac7, v1.4.0. Validation run: https://github.com/TechSavage1989/whosceneit/actions/runs/40230705080. Publication: https://github.com/TechSavage1989/whosceneit/actions/runs/40231340731.

## Version 1.4.0 checks

- Local quick compiled validation passed 64 checks with zero warnings/errors. Hosted Windows and Linux each passed all 65 checks including real timers, with zero build warnings/errors.
- New tests verify the option is false by default and frozen independently of mutable response DTOs, then passed to expansion, inspections, and deletion. Missing/false checkbox acknowledgement cannot start a job. The service passes the explicit mode and confirmation to native code.
- Native SDK fixtures allow unwatched/saved-position media only when opted in, preserve watch data, include all available show episodes, and retain playback, deletion permission/support, visibility, and root-folder guards. Original default saved-progress and revalidation checks remain.
- Local Chromium and exact-source hosted Linux each passed all 29 checks including real timers. New cases cover partially watched and not-started selection only when opted in, healthy empty-viewer show previews, clear/reset behavior, frozen-scope display, unchecked acknowledgement, removed typed input, boolean execution payloads, and versioned/stale controls.
- Sonarr/Radarr integration, native SDK compatibility, report/updater checks, and exact-path previews remain covered. All tests use synthetic data/temp files or explicitly pinned public release transport. Live Emby/Sonarr/Radarr deletion remains unverified.
- Independently verified all six public 1.4.0 assets against API digests and SHA256SUMS, tag/source, clean metadata, all 54 source files/60 preview hashes, and the exact canonical feed. Public DLL: 199,680 bytes; SHA-256 00591d2c6d3b5dda621e0e2049d3405b6465c8316a58fb6d652c93db079ca2a1. Feed commit: 537ff44b3ae718d85f4f8a8a2e1ccc5c0d82c525. The next-patch 1.4.1 fixture is test-only.
- The pinned actual public 1.3.0 memory-loaded updater installed exact public 1.4.0 bytes and verified custom filename, settings, backup, pending-health reload, and cleanup. The legacy public 1.0.0 failure/manual-bootstrap control also passed. See docs/validation-published-upgrade.txt. No Emby process or real user media was used.

## Historical 1.3.0 validation

Version 1.3.0 replaces the legacy Emby visibility method and adds optional Sonarr/Radarr synchronization. Hosted exact-source validation, publication, asset/feed verification, and the actual public 1.2.2-to-1.3.0 updater check passed. Source deedd7b16d85e3644db9aaedb771b9d0f530e736; validation run 40224437428; publication run 40225059244. The new screenshot identifies the SDK mismatch; no live Emby or Sonarr/Radarr deletion success is claimed.

## Version 1.3.0 checks

- Local quick compiled validation passes 62 checks with zero warnings/errors. Hosted Windows/Linux each passed all 63 checks with real timers.
- Negative control: restoring the removed two-argument visibility call causes MissingMethodException in the native SDK fixture. The repaired exact-item user query passes and still rejects inaccessible items. The fixture simulates the unavailable legacy method; it does not start a newer Emby process. See docs/validation-deletion-compatibility-negative.txt. Current reference: https://dev.emby.media/reference/pluginapi/MediaBrowser.Controller.Library.ILibraryManager.html.
- Thirteen integration checks cover disabled/read-only preview; exact file path and size; unmonitor-before-native-delete ordering; Sonarr shared-file ranges and retained series; native/remote/settings revalidation; lost receipts without replay; partial results; replacement-file preservation; API versions/URL bases/redirects; key masking; Windows/UNC/POSIX mapping; and actual native XML save/reload with blank-key preservation, explicit clearing, and running-operation exclusion.
- Local Chromium passed 26 checks including real timers during development. New browser cases exercise connection settings, masked keys, unsaved read-only tests, persistence/reload, exact preview service actions, independent media/synchronization outcomes, literal HTML error text, mobile settings, and stale cached HTML. Hosted Linux passed all 26 on the exact release source.
- All new routes require native Admin; only POST can save/test integration settings. Preview never changes monitoring or media. Configured missing/ambiguous/unreachable targets block the item; disabled connections explicitly remain Emby-only. API keys stay in native configuration and are absent from response views and artifacts.
- Sonarr and Radarr tests use synthetic HTTP handlers; native deletion tests use temporary files and SDK interfaces. There is no claim of a live authentication pipeline, actual external app behavior, or real-library deletion. Cross-app partial failures cannot restore media. Sonarr root-folder safeguards are not bypassed.
- All six public assets, 54 source files/60 preview hashes, tag, metadata, and canonical feed were independently verified. The 196,608-byte public DLL hash is 2383314165e2ae609d9ff3a2ffe48482141bc16edf1e88246de015fbf387aaa0. Actual public 1.2.2-to-1.3.0 memory-loaded installation/settings/backup/health/cleanup passed. The next-patch 1.3.1 DLL remains test-only.

## Historical 1.2.2 validation

Version 1.2.2 repairs deletion preview/error feedback and adds explicit confirmation/progress/results. Hosted exact-source CI, publication, public asset/feed verification, and the public previous-version upgrade check passed. Exact source: 43494290988f57813fdfcb6ca9a42d225951f5b5; tag v1.2.2. The user's HTTP 500 preview response did not expose its underlying server exception; no claim of live deletion success is made.

## Version 1.2.2 checks

- Local Release builds have zero warnings/errors. All 48 quick compiled checks pass. Exact-source hosted Windows and Linux each passed all 49 checks, including real timers, with zero warnings/errors: https://github.com/TechSavage1989/whosceneit/actions/runs/40220322970.
- New service tests invoke actual Preview/Execute/Status/Cancel methods with native SDK IRequest and AuthorizationInfo/User objects. They verify administrator binding, rejection of API-key-only/non-admin/missing requests, read-only preview, typed confirmation, temporary-file deletion, real status polling, ownership, and replay safety.
- A held deletion worker verifies Started, Processing, Processed, CurrentItemName, queued counts, cancellation, and final counts. An inspection exception blocks that item and retains the rest of the review; preview never invokes native deletion.
- Twenty-three Chromium checks pass locally and on hosted Linux, including real timers. Native browser Fetch Response errors are decoded as safe text. Tests cover structured preview failure, no-start messaging, read-only retry with fresh confirmation, all-blocked previews, live progress, loss of the execution response and a status response, recovery without replay, closing/reopening progress, and final results. The runner rejects an incomplete early exit instead of treating a partial log as success.
- Publication workflow https://github.com/TechSavage1989/whosceneit/actions/runs/40220959204 succeeded. All six public downloads passed independent size/hash verification; all 48 source files and 54 preview hashes matched, the immutable tag matched the approved source, and the canonical feed exactly matched the public manifest. The 150,016-byte DLL SHA-256 is dc87b73fd77e893e22eb1ec2833f792f42f79c74962a0807ed32f0db6185bc7a.
- The actual public 1.2.1-to-1.2.2 memory-loaded SDK updater check passed after publication, including custom-target installation, settings/rollback preservation, pending-health verification, staging cleanup, and the legacy failure/manual-bootstrap control. See docs/validation-published-upgrade.txt. No native Emby startup or real-library deletion is attempted.

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
