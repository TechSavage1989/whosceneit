# Who Scene It

An administrator-only Emby Server plugin that answers two questions:

- What movies and TV shows has this user watched?
- Which users have watched this movie, TV show, or episode?

Version 1.5.2 adds a visible saved-rule list and verifies that existing rules keep their settings, schedules, triggers, and enabled states through updates and restarts. All watch-state reports, Never delete protection, synchronization, and updater behavior remain available. See VALIDATION.md for test boundaries.

## Features

- By user: select an Emby user and view movies and grouped TV shows, movies only, shows only, or individual episodes.
- By title: search for a movie, show, or episode and list its viewers. The viewer list follows the selected watch-state filter.
- Watched, partially watched, not-started, and watched-with-resume filters; saved position, percentage, duration, and remaining time when available.
- Show completion percentages and watched/partial/not-started episode counts; View episodes opens a show for the selected user. Episode order and progress sorting are available.
- Saved play counts and the last recorded playback date.
- All watch states appear by default, including entirely unwatched movies, shows, and episodes. Search/filter controls, pagination, and CSV export include the matching progress details.
- Individual and bulk watched-media deletion, with an exact-path preview, explicit confirmation, progress, cancellation, and Emby permission/playback checks.
- Optional Sonarr/Radarr connection settings, read-only connection tests, path mapping, strict matching, unmonitoring, and separate synchronization results.
- Visible saved-rule list with status, owner, watch-history user, eligibility, schedule, triggers, next/last evaluation, and owner actions.
- Explicit, administrator-controlled plugin updates with SHA-256 and assembly validation, a rollback backup, and restart guards.

## Requirements

Emby Server 4.8 or newer is the intended target. The plugin is built against the official 4.8.0.80 SDK and targets netstandard2.0. Actual compatibility with your Emby version and platform still needs an installation test. This is not a Jellyfin plugin. An Emby administrator account is required. Reports and plugin updates need no Emby API key. Optional synchronization needs each existing Sonarr/Radarr server URL and API key.

## Install

If version 1.0.0 or 1.1.0 reports an empty "path" during installation, replace the DLL manually using the steps below. The already loaded old updater cannot apply its own path fix. Install the current 1.5.2 DLL directly; intermediate versions are unnecessary. Version 1.1.1 already contains the path fix and can use the normal updater. Keep the existing filename if you customized it.

1. Stop Emby Server.
2. Find its Server Data Folder in the Emby dashboard. Open the plugins directory directly under that folder.
3. Copy WhoSceneIt.dll from this package into that plugins directory. Do not copy the Source folder or Emby's SDK/dependency DLLs.
4. Start Emby Server and sign in through its web interface as an administrator.
5. Open Dashboard, Plugins, Who Scene It, then its settings/configuration page. The server menu also provides a Who Scene It page where supported.

After updating, reload the plugin page with Ctrl+F5 to refresh cached controls. The dashboard uses a versioned controller and shows a reload notice if it receives outdated HTML. If the page does not appear, refresh the browser and check Emby's server log for WhoSceneIt. Do not install this preview over an existing plugin with a different identity or name.

Emby's official manual plugin instructions: https://emby.media/support/articles/Plugins.html

## Understand the results

Emby's saved Played flag identifies a watched title. Manual marks and imported watch state are included, so this is not a forensic playback log. A saved play count of zero remains zero, and a missing date remains "Not recorded". Repeated plays are a count, not a list of historical viewing events.

The user report includes titles currently visible to the selected user. It reads saved positions directly, including positive positions below Emby's normal resumable threshold. The title report checks saved user data for all current users, including disabled accounts, even if their present library access differs. Unsaved playback and positions Emby has cleared cannot be recovered. A series manually marked watched without episode watch data cannot be reconstructed as watched episodes.

All watch states is the default and includes unplayed media. Watched and partially watched limits the list to completion flags or positive saved positions. Fully watched uses Emby's Played flag; Partially watched selects unfinished items with a saved position. Watched with resume includes possible rewatches and manual watched marks with a saved position, without claiming a second viewing occurred. Unwatched (not started) includes items with neither a completion flag nor a positive saved position. Progress is the saved position relative to the current runtime, not a record of every scene viewed. Unknown durations stay unknown; a position at the end does not create a completion flag. Refresh report reads the latest saved data.

A show appears when its episode states match the filter. Fully watched shows require every currently available episode to be marked watched; Partially watched also includes a show with some completed episodes and others unfinished. Counts use the full inventory, independently of the chosen filter. Choose View episodes beside a show or viewer to inspect that user's episodes, then Back to TV shows to clear the scope. The viewer report counts available episode files, including specials, rather than claiming that a user finished every episode ever released. Duplicate physical versions can affect these counts. Removed files and deleted users cannot be recovered from current user data. Dates are shown in your browser's time zone; CSV dates use UTC.

## Never delete

Use Never delete beside a report row, Never delete this title in By title, or Never delete selected for selected rows. Unwatched media is included by default; choose Unwatched (not started) to see only titles with no completion flag or saved progress. Select movies, shows, or scoped episodes individually or by page. Protection never requires the unfinished-deletion option. A protected show also protects its current and future episode files. Protection applies to every deletion mode in this plugin, including the unfinished/unwatched option, bulk deletion, and cleanup rules. It does not depend on who watched the media.

Open Never delete and automatic cleanup to manage protected titles. An episode protected by its show cannot remove that inherited protection; remove the show's protection explicitly from the protected list first. Exact paths also preserve protection when a file is reindexed under another library ID. These marks are plugin state; Emby's own controls and other applications retain their own deletion behavior. Back up Emby's DataPath/WhoSceneIt/retention.json and retention.json.bak with the server configuration. Unreadable or unsavable protection state blocks plugin deletion.

## Saved cleanup rules

The Saved cleanup rules section is visible above the collapsed protection/editor panel. It lists every saved rule, including disabled rules and rules owned by other administrators. Each row shows its status and action, owner, watch-history user, media scope, completion criteria, waiting period, batch limit, schedule, event triggers, and next/last evaluation. Schedule times are UTC; evaluation dates use the browser time zone.

Create rule opens the editor. Edit rule loads that exact saved rule; Evaluate now creates a fresh review prompt. Only the owner can edit/evaluate a rule. Refresh rules reads the current server state and does not save, remove, enable, or reset rules or overwrite unsaved editor input. If refresh fails, the last displayed rules stay visible with an error and actions remain blocked until a successful refresh.

Updates replace the plugin DLL and leave retention.json and its atomic backup intact. Existing IDs, revisions, owners, criteria, schedules, triggers, and enabled/disabled states are retained; updates do not require rules to be recreated or reauthorized. Explicit edits to enabled automatic rules still require fresh checkbox authorization. A restart status now reflects saved rules instead of claiming cleanup is disabled. If state is damaged or incompatible, or the primary is missing while its backup exists, cleanup stops and retains the available files for review instead of creating empty defaults or activating an obsolete backup.

## Cleanup rules

Open Never delete and automatic cleanup. Select a watch-history user, media scope, and completed state: fully watched without resume, fully watched with resume only, or both. Movies and individual episodes can be cleaned separately, or choose episodes of fully watched shows. Full-show scope requires every currently available episode to have that user's saved Played flag. Newly available unwatched episodes block that show's cleanup. Rules never clear progress or infer completion from a position/count/date.

The waiting period applies per item and defaults to seven days since its saved last-played date. Unknown dates are excluded when the waiting period is positive. A zero-day rule can include manually marked watched items without a known date. Batch limits range from 1 to 500, defaulting to 100. Completed-with-resume mode still preserves saved positions belonging to any user who has not completed that item. The broader unfinished/unwatched manual option is never enabled by automatic cleanup.

Rules are disabled by default and default to Prompt for review and confirmation. Enable a rule and choose an interval of 1 to 168 hours, a daily UTC time, or a weekly UTC day/time. Optional triggers run after playback stops, watched state changes, Emby starts, or the rule owner opens the plugin. Emby checks schedules once per minute. Item events settle for at least 30 seconds, coalesce up to 500 IDs per rule, and observe a 15-minute cooldown. Completed-show events can include the other completed episodes of that show. Schedules run while the dashboard is closed. Review prompts appear on this plugin dashboard.

Evaluate now always creates a review prompt, including for an automatic rule. Review media prepares a new exact-path/service preview; check the permanent-deletion box to execute it. Scheduled/triggered review rules only queue prompts. To enable unattended cleanup, choose Delete automatically, enable the rule, check its authorization box, and save. Changes require fresh authorization. Rules are owned by the signed-in administrator, whose native permissions are rechecked at execution. Save an edited/disabled rule or remove it to stop remaining queued items; an already executing item can finish.

The same playback, visibility, permissions, path/fingerprint, root/shared-folder, and Sonarr/Radarr checks apply. Never delete always takes priority. Cleanup history shows results, and View progress / stop opens the active operation. Completed detail tokens remain available for ten minutes; durable summaries remain after that. Attempt records are persisted before mutations. Interrupted or uncertain attempts are not replayed after a restart and remain on automation hold. Inspect the item and configured service, then use a fresh manual preview or explicitly release the reviewed hold with its checkbox before allowing future automatic attempts. A plugin rollback does not recover deleted media.

## Delete watched media

In By user, choose a user and use a title's Delete button, or select rows and choose Delete selected media. The header checkbox selects all unprotected titles on the current page for protection, regardless of watch status. Delete selected media reviews only selected rows that contain eligible completed media unless the unfinished-deletion option is enabled. Selections carry across pages and clear when the user, report view, or filter changes. Each batch supports up to 500 media items after TV shows expand to their watched episodes. Partially watched and watched-with-resume items can be selected for protection. Their deletion remains disabled by default, and saved progress for any current user blocks ordinary deletion, including progress appearing after preview. To include them, enable Allow deletion of partially watched and unwatched media. This is off on a freshly loaded page and applies only to previews prepared with that option. Enabling it changes Watched and partially watched to All watch states when that filter is selected; other chosen filters stay selected. Use All watch states or Unwatched (not started) to find unwatched media. Changing this option clears the selection.

In By title, select a movie, show, or episode and choose Delete watched media. This view includes media marked watched by at least one current user; filtering the viewer list does not narrow the deletion preview. By default, TV shows expand into watched episodes. With the unfinished/unwatched option enabled, they expand into all currently available episodes within the selected user scope. The plugin never uses a show-folder deletion to implement this action.

Delete selected media first prepares a read-only preview. If preparation fails, no deletion has started; the dialog shows the reason and Retry preview. Retry only prepares another review and never confirms deletion. If every item is blocked, the dialog lists the reasons without offering confirmation.

Review every listed path and any Sonarr/Radarr action. Deletion removes media from the library and its storage for every user. Emby's normal deletion behavior can also remove listed folders, metadata, subtitles, and other folder contents. The plugin blocks filesystem roots, configured library roots, and directory deletions containing other movie/episode library items. Merged versions or unusual shared-folder layouts may therefore require Emby's own media controls.

Review the watch-state scope, paths, and service actions, check the permanent-deletion confirmation box, and choose the delete button. No typing is required. Every new or retried preview starts with confirmation unchecked. Only the frozen preview can run; newly watched episodes are not added after confirmation. Previews expire after ten minutes. The server freezes the watch-state option with the preview and uses it during every recheck. Ordinary previews continue to protect unfinished media and saved progress. Opted-in previews allow those watch states without changing them. Emby's administrator/content-deletion permissions, selected-user visibility, active/paused sessions, exact paths, and file fingerprints still apply to both modes. Local movies and episodes must qualify; channels, virtual media, and non-file sources are excluded. The selected administrator must already have Emby's content-deletion permission for the item.

After confirmation, a progress bar shows processed/total counts, the current item, and deleted, queued, blocked/skipped, and failed results. A lost response is treated as an unknown status; the plugin checks the existing operation and does not resubmit deletion. Close progress view only closes the dialog; View deletion progress reopens the current operation while you remain on this page. Stop after current item preserves the remaining items. Confirmed operations continue inside Emby if you leave the page, and are not replayed automatically after a server restart. Partial deletion can occur if Emby or the filesystem fails; refresh the library and review the result before retrying. Plugin rollback restores only the plugin DLL and cannot recover deleted media. Plugin installation/restart is blocked while deletion is running.

## Sonarr and Radarr synchronization

Synchronization is disabled by default until configured. Expand Sonarr and Radarr synchronization on the plugin page.

1. Enter each service's base URL as reachable from Emby Server, including any URL base such as /sonarr. Enter its existing API key in the password field.
2. Enable the matching connection: Sonarr for episodes, Radarr for movies.
3. If the services see different filesystem paths, provide both path prefixes. Otherwise leave both blank. For example, map Emby's /media/tv to Sonarr's /tv, or D:\Media\Movies to Radarr's /movies. Prefix matching respects path boundaries, Windows/UNC case handling, and POSIX case. One connection and one prefix mapping per service are supported.
4. Use Test connection. This reads the supplied connection without saving changes or modifying media. Sonarr v3 and v5 API routes are detected; Radarr uses its v3 API.
5. Save settings, then prepare a new deletion preview. Blank API-key fields preserve stored keys; Clear the stored API key removes one explicitly. Reload saved settings discards unsaved form edits. Settings cannot be saved during a running deletion.

Keys are saved in Emby's native plugin configuration and are never returned by the integration settings endpoint, reports, previews, or CSV. Protect and back up that configuration as you would other service credentials. Requests use the configured service only; HTTP redirects are rejected, and TLS certificate checks are retained. No credentials or library data are sent to GitHub.

For an enabled service, the preview requires exactly one matching file path and matching file size. Sonarr also verifies episode numbering and all episodes associated with a shared file. A shared file containing episodes outside the selected Emby episode or numbered range is blocked. Ambiguous records, duplicate remote targets, missing matches, unavailable services, changed settings, and changed targets preserve media. Names alone are never used to choose a deletion target. Disabled connections are explicitly labelled Emby-only in the preview.

After confirmation, the plugin verifies the remote target and unmonitors it, reads the monitoring state back, and rechecks Emby's native deletion guards. Emby performs the actual media deletion. The plugin then removes the matched Sonarr episode-file record or Radarr movie entry and verifies the record is absent. Sonarr retains its series and episode listings with the affected episodes unmonitored by default. The separate Remove the Sonarr show entry option removes a matched series entry only after both Sonarr and native Emby have no episode files. It checks again immediately before one series DELETE with deleteFiles=false and verifies absence afterward. The option defaults off and appears in the frozen preview; changing it invalidates older previews. If files remain or native emptiness cannot be established, the show entry stays. Radarr is told not to delete additional files. Its optional import-list exclusion is requested when removing the movie; that setting does not prevent a later manual re-add.

The UI shows Emby and external service results separately. If external cleanup fails after media deletion, the media remains deleted and the result reports a synchronization issue. If preparation/unmonitoring fails first, media deletion does not start for that item, although monitoring may already have changed. There is no cross-application transaction or media rollback. Read-back checks can verify a lost mutation receipt; mutations are not automatically replayed. Review partial failures in the matching service rather than repeating media deletion. Deleting the last series folder can also trigger Sonarr's own root-folder safeguards; those protections are not bypassed.

## Updates

The trusted repository is https://github.com/TechSavage1989/whosceneit. The update feed is the repository's main/update-manifest.json file. Release assets are published and verified before this feed advances to the new version.

Update installation resolves Emby's registered plugin AssemblyFilePath when the assembly itself has no file location. If host metadata is not yet available, it finds one matching DLL in Emby's plugins directory. Custom filenames are preserved; duplicate/mismatched targets stop installation instead of choosing a file arbitrarily. Paths are resolved when needed, including after restart, so late startup registration does not leave an empty cached target.

The plugin checks in the background at server startup and every five minutes. Returning to the plugin page/window triggers a check if at least two minutes have passed. Manual checks have a 30-second cooldown. Conditional requests and backoff reduce repeated traffic. A failed/offline check reports failure and preserves any cached result without claiming to be current.

Choose Download, then Install and restart. The warning refers to Emby Server, so all of its clients briefly disconnect. Active/paused playback, scans, scheduled tasks, and plugin report queries block the restart. On hosts that cannot restart themselves, the action installs the DLL and asks you to restart through Emby's normal controls. If the operating system locks the loaded DLL, the update stops and reports an error; install the verified release manually with Emby stopped.

Downloads are a single DLL from the named repository. The plugin verifies allowed HTTPS hosts, size, SHA-256, the PE DLL header, assembly identity, and version. SHA-256 matching is integrity checking, not publisher signing. The binaries are unsigned.

Emby must load the new assembly on restart. When the plugin loads, it verifies the pending installation's version and digest. Cleanup waits until an administrator opens a visible dashboard page and the report API responds. The browser polls for reconnection and displays the loaded version. If the new plugin cannot load at all, its own code cannot automatically recover; use the rollback procedure below. Native restart/reconnection and recovery from an unloadable DLL remain unverified in this preview.

## Data and rollback

Plugin configuration stays in Emby's own plugin configuration directory. Update cache, staging, the pending-install record, and the single rollback DLL are under the directory WhoSceneIt inside Emby's DataPath. This is normally Server Data Folder/data/WhoSceneIt. The backup is rollback/WhoSceneIt.dll below that directory. Emby logs contain ordinary server/plugin errors; CSV exports go to the location chosen by your browser.

To roll back, stop Emby Server, copy the rollback DLL over the installed plugin DLL, remove the plugin-owned pending-install.json record, and start Emby again. Keep the installed DLL's actual filename if you customized it. Do not remove Emby configuration, library files, or user data. The updater preserves configuration and never marks a title watched or unwatched.

## Development

Read AGENTS.md and DEVELOPMENT_HANDOFF.md first. Source is in src/WhoSceneIt; report/update tests and browser tests are in tests; reproducible build, test, packaging, and release scripts are in scripts. No Emby server, credentials, or media files are included.
