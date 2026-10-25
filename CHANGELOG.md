# Changes

## 1.3.0 - Emby deletion compatibility and Sonarr/Radarr synchronization

- Replaced the removed two-argument FilterItemsToIdsForUser call with an exact-item, user-scoped native query. Selected-user visibility and content restrictions remain enforced. A regression fixture rejects the legacy method and catches the old implementation.
- Added administrator-only Sonarr/Radarr settings, masked stored-key indicators, explicit key preservation/clearing, native XML persistence, read-only connection tests, and optional path-prefix mapping. Integrations remain disabled until configured.
- Added strict path/size matching, Sonarr shared-episode range checks, duplicate-target protection, cached read failures, and revalidation of settings, remote identities, and native media guards. Preview is read-only; unavailable/unmatched enabled services block media deletion.
- Unmonitor and verify matched entries before Emby deletes media. Afterward, remove and verify Sonarr's file record or Radarr's movie entry. Sonarr series/episode listings remain; Radarr is told not to delete additional files and can request an import-list exclusion.
- Report native media deletion and external synchronization independently, including partial success, cancelled/not-run actions, and lost receipts verified by reads without mutation replay. A replacement file appearing at the Emby path stops Sonarr cleanup.
- Added a versioned dashboard controller and stale-HTML reload notice. Retained existing reports, saved-progress protections, deletion confirmation/cancellation, settings, and updater behavior.
- Added native settings-persistence, mock HTTP contract, partial-failure, path-mapping, and browser tests. No live Emby, Sonarr, Radarr, or user media is used by the automated tests.

## 1.2.2 - deletion confirmation and progress

- Separated read-only preview failure, review/confirmation, deletion submission, active progress, and final result states. A failed preview explicitly states that no deletion was started.
- Read native Fetch error response bodies as well as XHR-style errors, so Emby's returned error reason and HTTP status are visible as safe text.
- Returned structured preview failures for authentication/availability/selection errors and kept per-item inspection failures visible as blocked items without discarding the whole review.
- Added a read-only Retry preview action. Every successful retry requires fresh acknowledgement and typed DELETE confirmation.
- Added processing/completed counts, a current-item label, a progress bar, clear blocked/skipped/failed results, and closing/reopening the current progress view without stopping the operation. Unknown submission/status responses are polled without replaying deletion.
- Added real service-method tests with native SDK request authorization, temporary media deletion, cancellation, progress, ownership, and replay checks. Added browser tests using actual Fetch Response errors and lost responses.
- The supplied HTTP 500 did not include its server exception. This release repairs missing error-body handling and deletion feedback; it preserves native permissions, playback/watch checks, and all existing deletion guards.

## 1.2.1 - report query repair

- Preserved Emby's native empty ancestor-ID array for reports without a selected show. Version 1.2.0 incorrectly set it to null; the new SDK-default regression test fails on that code and passes after the repair.
- Show-scoped episode reports still use their selected series ID. Added coverage for unscoped Titles, Movies, Shows, and Episodes queries.
- Report failures now show the server's message and HTTP status as plain text, without rendering returned HTML or stack traces. Administrator access guidance is preserved.
- Failed or loading reports no longer acknowledge dashboard health using an earlier successful report, and failed requests clear stale media selections.
- Retained watch progress, filtering, CSV export, media-deletion protections, settings, and updater behavior.
- The screenshot did not expose the server exception. This patch fixes the verified query-default regression and exposes the underlying message if another server error remains.

## 1.2.0 - episode watch progress

- Added watched, partial, not-started, all-state, and watched-with-resume filters. Started items appear together by default; existing API callers retain IncludeInProgress behavior when WatchState is omitted.
- Read saved positions directly, including positions below Emby's resumable threshold. Emby's Played flag remains authoritative for completion; positions, dates, or play counts never fabricate it.
- Added saved position, runtime, percentage, and remaining time, with explicit unknown durations and separate watched-with-resume labels for possible rewatches.
- Added complete show inventories, completion percentages, not-started counts, numeric episode sorting, progress sorting, and View episodes from either report view.
- Added progress fields to CSV exports while preserving filtering, paging, and formula neutralization.
- Preserved media with saved partial playback or resume positions for any user, including when progress changes after deletion preview.
- Preserved reports, administrator-only routes, settings, updater path discovery, and existing deletion/update guards.
- Added native SDK report checks, progress edge cases, cross-user deletion checks, and browser tests for the new controls. Published-upgrade validation now also exercises the real public 1.1.1 entry point installing the new release.

## 1.1.1 - updater installation path fix

- Fixed the empty-path installation error when Emby loads the plugin DLL from bytes.
- Uses Emby's registered plugin path, with a unique matching-DLL fallback, while preserving custom filenames and rejecting ambiguous/mismatched targets.
- Resolves the path when installing and checking a restarted plugin, rather than caching an unavailable assembly location at startup.
- Added actual memory-loaded entry-point tests, the old empty-path negative control, and install/reload/backup/settings/cleanup checks.
- Memory-loaded versions 1.0.0 and 1.1.0 require one manual DLL replacement to receive this fix. The prior explicit-path updater fixture did not test the failing entry point.
- Preserved watched reports, individual/bulk media deletion, update checks, and safety controls.

## 1.1.0 - media deletion preview

- Added individual and bulk deletion of watched movies and TV episodes, from both report views.
- Added page selection, selection across pages, exact-path previews, typed confirmation, progress, and cancellation of remaining items.
- TV shows expand to watched episodes only. Deletion respects Emby permissions and playback state, rechecks paths/watch state, and protects library roots and folders containing other library media.
- Blocked plugin installation/restart during media deletion and retained watch-report/CSV behavior.
- Allowed background update checks/downloads to coexist with deletion, while keeping installation and pending restart excluded. Playback guards recognize both numeric and GUID item IDs.
- Fixed a five-minute update callback being skipped by a redundant wall-clock interval check, observed in Windows CI.
- Corrected draft release verification to resolve the authenticated draft by its stable release ID. Published 1.0.0 application assets remain unchanged.
- Added deletion tests and an explicit post-publication check using the real published 1.0.0 updater and public release feed.

## 1.0.0 - published preview

- Added watched-title reports by user with grouped TV shows and episode detail.
- Added movie/show/episode lookup and viewer reports across current Emby users.
- Added filtering, pagination, optional in-progress entries, and complete CSV exports.
- Added administrator-only update controls, a background check timer, verified DLL downloads, atomic installation, and one rollback backup.
- Added tests, packaging, GitHub CI/release definitions, and development handoff instructions.

This is the first application baseline, published as a preview on October 25, 2026. Hosted Windows/Linux CI, all six public asset downloads, and their hashes are verified. Live Emby/platform verification remains pending.
