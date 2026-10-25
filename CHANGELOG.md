# Changes

## Documentation refresh — 2026-10-25

README reorganized for end users: preview download, requirements, numbered setup, reporting, and prominent cleanup safeguards; full operational reference retained. Documentation-only change reviewed against main `2d0e52b21dbca093f7d1e4478d08e249c585854f`, published v1.5.2 assets, and the unchanged update feed. Application version, settings/data paths, source behavior, and release assets are unchanged. Markdown anchors, relative links, and release/workflow routing were checked; no live Emby or destructive media tests were performed for this documentation edit.


## 1.5.2 - visible rules and update preservation

- Added an always-visible saved-rule list with enabled/disabled status, action, owner, watch-history user, media/completion scope, waiting period, batch limit, schedules/triggers, next/last evaluation, and owner-only edit/evaluate actions.
- Keep disabled and other-owner rules visible. Create rule opens the existing editor. Refresh is read-only and preserves unsaved input; failed refreshes retain the previous list and block stale actions.
- Fixed the startup message so existing enabled rules are reported accurately after a restart or update. Rules retain the existing schema, IDs, revisions, owners, all settings, schedules, triggers, and enabled states.
- Prevent missing primary state from silently becoming empty rules when a backup exists. Damaged/incompatible state and backups are retained for review; cleanup stops without resetting or automatically restoring old rules.
- Added a frozen 1.5.1 rule-state compatibility fixture, exact full-state checks through install/reload/health cleanup, replacement-failure rollback preservation, restored schedule execution, and schema/backup guards. Public upgrade checks now load the same complete rule state with both old and new public binaries.
- Retained prior reports, protection, deletion modes/consent, synchronization, and updater behavior. The dashboard uses a versioned 1.5.2 controller with old aliases and cached-HTML reload guidance.

## 1.5.1 - protect unwatched media

- Show all watch states by default and label the unwatched-only filter clearly, so entirely unwatched movies, shows, and episodes are easy to find.
- Allow individual and page/bulk selection for Never delete regardless of watch status, without enabling unwatched deletion. Existing protected shows continue to cover current and future episodes.
- Keep deletion eligibility separate from protection selection. Mixed selections include only eligible completed media in ordinary deletion previews; unfinished deletion still requires its explicit opt-in.
- Refresh selected row eligibility when reports reload, retain cross-page selection, and preserve native protection/deletion rechecks and existing settings.
- Add unwatched inventory/protection and mixed-selection regression coverage. Use a versioned 1.5.1 controller and reload guidance for cached older HTML.

## 1.5.0 - never delete and automatic cleanup

- Added persistent Never delete protection for movies, shows, and episodes, with show protection inherited by current and future episodes. Manual, bulk, unfinished, and automatic deletion all recheck it. Protected titles can be managed individually, by selection, or from the protected list.
- Added administrator-owned cleanup rules for completed media, completed media with resume positions, or both. Choose movies, individual episodes, or episodes of fully watched shows, a waiting period, and a bounded batch size. Saved unfinished viewing for any user remains protected.
- Added review prompts, on-demand evaluation, interval/daily/weekly UTC schedules, and optional playback-stopped, watched-state, startup, and dashboard triggers. Rules start disabled with review mode selected. Enabled unattended deletion requires fresh checkbox authorization when saving the rule.
- Reuse exact native/service previews, deletion revalidation, progress, cancellation, permissions, and Sonarr/Radarr synchronization. Editing or disabling a rule stops remaining items after the current item. Manual evaluation always queues review, including for automatic rules.
- Added atomic persistent state, a pre-mutation attempt journal, history/live results, and automation holds for interrupted or uncertain attempts. Jobs are not replayed on restart. Explicitly reviewed holds can be released for a future fresh evaluation. Storage failure blocks deletion.
- Added a separate Sonarr option to remove a show entry only when no episode files remain in Sonarr or Emby, with deleteFiles=false and read-back verification. Default behavior retains the show and unmonitors deleted episodes.
- Added SDK, persistence, scheduling, eligibility, restart, service, and browser coverage. The compiled test runner now rejects incomplete zero-exit runs. Reports, settings, previous deletion modes, stable DLL identity, and updater behavior remain available.

## 1.4.0 - optional unfinished deletion and checkbox confirmation

- Added an off-by-default option to include partially watched, unwatched, and watched-with-resume media. Selected shows include all available eligible episodes when opted in; ordinary previews retain completed-only behavior and saved-progress protection.
- Freeze the watch-state choice with the administrator-bound preview and retain it for expansion, revalidation, execution, status, and retries. Never change watch flags to make media eligible.
- Retained active/paused playback, visibility, permissions, paths/fingerprints, folder protections, and Sonarr/Radarr matching and synchronization in both modes. Previews display the selected mode and saved-progress notes.
- Replaced typed DELETE input with one unchecked permanent-deletion acknowledgement and the delete button. Execute requires an explicit Confirmed=true; a missing/false acknowledgement never starts deletion. Each new/retried preview resets confirmation.
- Clear selections when changing the option, allow all-state title previews even with no completed viewers, and keep failed-report actions disabled. The dashboard uses a 1.4.0 controller while retaining older aliases for reload guidance.
- Added frozen-mode, checkbox, default-protection, opt-in series/episode, and native-guard regression coverage. Existing report, updater, and external-service features are retained.

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
