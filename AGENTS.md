# Who Scene It - read first

Read this file and DEVELOPMENT_HANDOFF.md before editing. Then inspect the current repository, release feed, and releases. This is an Emby Server plugin, not a Windows desktop executable, PowerShell app, hosted website, or Jellyfin plugin.

## Scope and baseline

- Repository: https://github.com/TechSavage1989/whosceneit
- The repository was public with only an initial README at commit c653762c30b37f3bb72ea3a3ac6b68aa7e8eb4b5 when work began.
- Latest maintained/published application: 1.5.0 preview, tag v1.5.0 at 45d198dd972d8c995f4a63589c62c5ba88c3afd5. Hosted Windows/Linux each passed 84 compiled checks; Linux passed 36 browser checks. Publication 40236020877 succeeded. All six public assets, canonical feed, 61 source files/67 preview hashes, and actual public 1.4.0-to-1.5.0 memory-loaded SDK updater installation/cleanup are verified, including protected-state preservation. Live Emby/Sonarr/Radarr deletion and native event delivery remain unverified.
- User-confirmed working baseline: 1.0.0, tag v1.0.0 at 59d6cfe49406446ee76ca52abc5dd40cc8e91d9a. The user confirmed it works on October 25, 2026. Version 1.1.0 adds the explicitly requested media deletion feature.
- Public source publication and release publication require the user's explicit approval. A repository URL alone does not override that instruction. Do not change visibility, create credentials, or embed tokens.
- Preserve the stable assembly name and installed filename WhoSceneIt.dll and plugin ID b6405a2e-52e6-4218-a014-00a46a65f1f8 across versions. Use the assembly's actual installed location for updates so custom filenames survive.
- Keep reports read-only. The user explicitly approved individual and bulk deletion of watched media and publication of the new build. Deletion must use Emby's native policy checks and library deletion API, an administrator-bound expiring preview, exact-path revalidation, explicit checkbox confirmation, and progress/error reporting. Never delete a TV show folder to implement watched-episode deletion. Never grant permissions or change watch state to make an item eligible. Every plugin endpoint must require Emby's native Admin role.
- No server setup, Codex CLI automation, unattended workers, payment setup, or API-account setup. GitHub CI and an explicitly invoked release workflow are part of the requested build/release scope.

## Version 1.5.0 protection and cleanup work

- The user explicitly requested persistent Never delete marks for movies/shows/episodes, automatic cleanup of completed and completed-with-resume media through prompts/schedules/triggers, publication, and configurable removal of empty Sonarr shows. This authorizes the native Emby cleanup entry point and explicitly enabled automatic rules; the older ban on unattended work and per-operation confirmation is superseded only for this feature. No external workers, ChatGPT scheduled tasks, or live target-server setup are authorized or needed.
- Never delete applies to every plugin deletion mode, including AllowUnwatched. Protect by stable ID/exact path, inherit protected series IDs/folder paths to current/future episodes, and recheck under the protection lock before native deletion. Do not change Emby tags, watch flags, access policies, or external application settings to achieve this.
- retention.json and its atomic backup live under Emby's DataPath/WhoSceneIt. Rules are disabled by default and default to review prompts, seven days since last played, and 100 items. Enabled automatic rules require a fresh checkbox authorization when saved; bind them to the real signed-in administrator. Manual Evaluate now always queues a fresh preview/checkbox review.
- Automatic cleanup never enables AllowUnwatched. AllowWatchedResume admits saved positions only for users whose Played flag is true; another user's saved unfinished viewing blocks deletion. Recheck completion, age, owner permissions, full-show state, protection, paths, sessions, and configured service matches. Rule edits/disable stop queued items; the in-flight item may finish.
- Native schedule polling is once per minute, with interval/daily/weekly UTC rules. Playback-stopped and native watched-state changes coalesce for at least 30 seconds with a 15-minute event cooldown. Startup/dashboard triggers are optional; dashboard triggers apply only to the rule owner. Resume progress alone never proves completion. Preserve native SDK collection defaults.
- Persist exact attempted targets before any automatic or reviewed-rule mutation. Interrupted/uncertain attempts remain on durable automation hold; never replay them after restart or lost responses. A held item needs explicit administrator review/checkbox release before a future fresh automatic attempt. Fail closed if protection/journal storage cannot be read or saved. Retain live progress and separate native/service outcomes.
- Sonarr RemoveEmptySeries is false by default, included in frozen integration settings, and independently configurable. Enabled: remove only the exact original series entry after both Sonarr and native Emby contain no episode files, rechecked immediately before one DELETE with deleteFiles=false. Keep the default behavior of retained series and unmonitored removed episodes. Never replay that mutation; verify absence after a lost receipt. No cross-app atomicity or live-service success is claimed.
- Published version 1.5.0; test-only fixture 1.5.1. Local quick suite passed 83 compiled checks and 36 complete browser checks. Exact-source hosted Windows/Linux each passed 84 including timers; Linux passed 36 browser checks. Publication, six public assets/tag/source/feed, and the actual public 1.4.0-to-1.5.0 protected-state upgrade check passed. Do not publish a test fixture. The compiled runner now rejects incomplete zero-exit runs as well as test failures.

## Current deletion-option work

- Published: 1.4.0. The user explicitly requested optional deletion of partially watched/unwatched movies and shows and removal of typed DELETE confirmation. This supersedes the earlier mandatory typed-confirmation and unconditional saved-progress restrictions. Require a checkbox acknowledgement (Confirmed=true) for the frozen preview; never infer consent from the broader watch-state option.
- AllowUnwatched defaults false and is frozen in each administrator-bound preview. With it enabled, selected shows expand into all available scoped episodes; watch-state/resume checks alone are bypassed. Keep administrator/content-deletion policy, user visibility, playing/paused sessions, filesystem/library-root protections, exact fingerprints, and external-service target checks in both modes. Never alter Emby watch state.
- Default previews must continue to preserve unfinished media and every user's saved progress. Each new/retried preview resets acknowledgement. Mode changes clear selections; the UI must explain that saved progress for any user can be included when enabled. Keep real SDK and browser tests for default, opt-in, scope freezing, and missing confirmation.

## Current compatibility and synchronization work

- Published repair: 1.3.0. The new screenshot identifies MissingMethodException for ILibraryManager.FilterItemsToIdsForUser(BaseItem[], User). Current Emby documents a different overload. Use GetItemList with InternalItemsQuery(selectedUser), an exact ItemIds filter, EnforceContentRestriction, and preserved SDK collection defaults. Never bypass visibility to work around SDK differences.
- The user requested Sonarr/Radarr deletion synchronization and added the SDK error as another issue to fix. Keep optional server-configured integrations disabled until URLs/API keys are supplied. Enabled connections require one exact path/size match; Sonarr must cover every episode in a shared file within the selected Emby numbered range. Never match by title alone or delete a Sonarr series for an episode selection.
- Unmonitor/verify first, then revalidate and delete through native Emby, then verify remote cleanup. Radarr DELETE must keep deleteFiles=false. Report Emby deletion separately from external outcomes; there is no atomic transaction across apps and no media rollback. Never automatically replay mutations after a lost response. Cache a failed read for the batch and retain only frozen targets after preview.
- API keys are stored through native plugin configuration, never returned by the integration API or placed in URLs/logs/artifacts. All integration endpoints require native Admin; settings save is blocked during deletion. Only synthetic HTTP/settings/media fixtures may run during development. Do not connect to or delete from real Sonarr/Radarr instances without the user's specific target-server instruction.
- Keep the versioned dashboard resource in Plugin.GetPages and HTML aligned with the application version. Preserve the old script alias and show a reload notice for outdated HTML.

## Deletion feedback repair

- Published repair: 1.2.2. The user's deletion dialog shows an HTTP 500 while loading the read-only preview, before confirmation. Do not claim files were deleted or that the unseen server exception has been reproduced. Decode Fetch Response error bodies and XHR errors, show precise no-start/unknown/active/final states, and never replay a destructive request on transport failure.
- Keep real service-method coverage with the native IRequest/IAuthorizationContext shape; the signed-in actor remains separate from the selected report user. A failed preview has no executable token. Per-item inspection failures stay blocked and visible. Progress only counts completed, blocked/skipped, or failed items as processed; identify any in-flight item separately.

## Report-query repair

- Published repair: 1.2.1. The user's screenshot confirms 1.2.0 is loaded but its report request fails. The 1.2.0 unscoped report sets AncestorIds to null instead of retaining the SDK's default empty array. Preserve native collection defaults; set AncestorIds only for a validated show scope. The SDK regression test catches the old code. The screenshot does not show a native stack trace, so do not claim another server cause is excluded.
- Display structured Emby errors and HTTP status safely as plain text, retain 401/403 guidance, and never acknowledge dashboard health from an unsuccessful or stale report. Keep test coverage for normal and show-scoped queries.

## Watch-progress behavior

- Published version: 1.2.1. The user explicitly requested episode full/partial detection, related features, and publication. Read saved Played, PlaybackPositionTicks, and RunTimeTicks without changing them. A saved position alone never implies completion or a proven rewatch. Keep unknown durations unknown, complete show denominators, scoped episode drilldowns, and watch-state filtering before paging/export.
- Partial playback or saved resume positions for any current user must block ordinary media deletion, including after preview, unless that frozen preview explicitly opted into AllowUnwatched. Never automatically clear progress or change a watch flag to make media deletable.

## Build and test

Use .NET SDK 8 or newer. The plugin must target netstandard2.0. Emby supplies its own runtime and SDK assemblies; install only WhoSceneIt.dll.

    dotnet build src/WhoSceneIt/WhoSceneIt.csproj -c Release
    python scripts/test.py --real-timer
    npm ci
    npx playwright install chromium
    npm run test:ui -- --real-timers
    python scripts/package.py

The C# test runner generates an unpublished next-patch DLL to exercise upgrades from the current version (1.5.1 for the 1.5.0 source). Never publish a test-artifacts fixture. Version 1.5.0 must be built from this source; never publish an upgrade-test fixture. The real timer test takes about five minutes. Browser tests use synthetic data and never connect to a user's Emby server. After publication, --published-upgrade reproduces the public 1.0.0 memory-loaded entry-point failure and one-time manual bootstrap, then exercises the actual public 1.4.0 memory-loaded updater installing the current release. It never starts Emby.

## Updates and releases

- User-reported 1.0.0-to-1.1.0 installation failed because the entry point used an empty Assembly.Location. Emby may load DLL bytes into memory. The previous explicit-target fixture missed this. Keep the patched lazy target provider and PluginPathResolver: prefer matching native IPlugin.AssemblyFilePath, then a unique matching DLL in PluginsPath, and reject wrong/duplicate/shadow-cache targets. Affected old installations need a manual DLL replacement once; do not claim a feed change repairs already loaded old code.
- Feed: https://raw.githubusercontent.com/TechSavage1989/whosceneit/main/update-manifest.json
- Use three-part release versions, four-part matching assembly/file versions, and immutable vMAJOR.MINOR.PATCH tags. Never replace assets under a published version.
- The server-owned timer checks on startup and every five minutes. Browser focus checks require two minutes since the last attempt; manual checks have a 30-second cooldown. Preserve single-flight, ETags, cached results, backoff, and Retry-After behavior.
- No automatic downloads or unsolicited restarts. Installation is an explicit administrator action. Block installation/restart during playback, library scans, running scheduled tasks, and active plugin reports.
- Validate the exact repository, HTTPS URLs and redirect hosts, size, SHA-256, PE DLL header, assembly name, and version. A digest is not publisher signing. Updates use a single DLL, never an archive.
- Stage on the destination filesystem, verify before and after atomic replacement, restore the old DLL on replacement failure, and retain one rollback DLL in Emby's data directory. The updater must never remove settings or media data. The separately confirmed media-deletion feature has no media rollback.
- Do not claim an Emby update is healthy merely because a file was replaced. Disclose the limits of server load checks, browser checks, and platform testing.
- The release workflow is manually invoked after approval. It creates a draft, uploads and verifies all assets, publishes, verifies the public DLL hash, then advances the feed. No feed may point at a missing asset.

## Handoff and validation

Update DEVELOPMENT_HANDOFF.md, CHANGELOG.md, and VALIDATION.md after meaningful changes. Record source/ref, version, feed, settings/data paths, checks actually performed, and limitations. Keep credentials, runtime downloads, test servers, local absolute paths, and test data out of release artifacts.

Automated Emby startup validation was blocked by automatic approval review because Emby attempted an external public-IP lookup. Do not bypass that rejection or silently repeat the blocked test. The user subsequently confirmed 1.0.0 works. Automated live Emby deletion/authentication/restart, loaded-DLL behavior on Windows, and Docker/NAS remain unverified. SDK and temporary-file deletion checks do not constitute a real-library deletion test. See DEVELOPMENT_HANDOFF.md for release state and exact checks.
