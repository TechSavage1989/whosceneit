# Who Scene It

An administrator-only Emby Server plugin that answers two questions:

- What movies and TV shows has this user watched?
- Which users have watched this movie, TV show, or episode?

Version 1.1.0 adds individual and bulk deletion of watched media. The user confirmed version 1.0.0 works; the new deletion feature still needs confirmation in a real Emby library. See VALIDATION.md before installation.

## Features

- By user: select an Emby user and view movies and grouped TV shows, movies only, shows only, or individual episodes.
- By title: search for a movie, show, or episode and list its viewers. A TV show qualifies when the user has at least one watched episode.
- Watch status, watched episode counts, saved play counts, and the last recorded playback date.
- Optional in-progress entries, search/filter controls, pagination, and CSV export of all matching rows.
- Individual and bulk watched-media deletion, with an exact-path preview, explicit confirmation, progress, cancellation, and Emby permission/playback checks.
- Explicit, administrator-controlled plugin updates with SHA-256 and assembly validation, a rollback backup, and restart guards.

## Requirements

Emby Server 4.8 or newer is the intended target. The plugin is built against the official 4.8.0.80 SDK and targets netstandard2.0. Actual compatibility with your Emby version and platform still needs an installation test. This is not a Jellyfin plugin. An Emby administrator account is required; no new API account or API key is needed.

## Install

1. Stop Emby Server.
2. Find its Server Data Folder in the Emby dashboard. Open the plugins directory directly under that folder.
3. Copy WhoSceneIt.dll from this package into that plugins directory. Do not copy the Source folder or Emby's SDK/dependency DLLs.
4. Start Emby Server and sign in through its web interface as an administrator.
5. Open Dashboard, Plugins, Who Scene It, then its settings/configuration page. The server menu also provides a Who Scene It page where supported.

If the page does not appear, refresh the browser and check Emby's server log for WhoSceneIt. Do not install this preview over an existing plugin with a different identity or name.

Emby's official manual plugin instructions: https://emby.media/support/articles/Plugins.html

## Understand the results

Emby's saved Played flag identifies a watched title. Manual marks and imported watch state are included, so this is not a forensic playback log. A saved play count of zero remains zero, and a missing date remains "Not recorded". Repeated plays are a count, not a list of historical viewing events.

The user report includes titles currently visible to the selected user. Its in-progress option uses Emby's resumable entries. The title report checks saved user data for all current users, including disabled accounts, even if their present library access differs. Very short playback below Emby's resume threshold may not appear in the user report. A series manually marked watched without episode watch data cannot be reconstructed as watched episodes.

A show appears when at least one individual episode qualifies. The viewer report counts available episode files, including specials, rather than claiming that a user finished every episode ever released. Duplicate physical versions can affect these counts. Removed files and deleted users cannot be recovered from current user data. Dates are shown in your browser's time zone; CSV dates use UTC.

## Delete watched media

In By user, choose a user and use a title's Delete button, or select rows and choose Delete selected media. The header checkbox selects watched titles on the current page. Selections carry across pages and clear when the user, report view, or filter changes. Each batch supports up to 500 media items after TV shows expand to their watched episodes. In-progress-only items cannot be selected for deletion.

In By title, select a movie, show, or episode and choose Delete watched media. This view includes media marked watched by at least one current user; filtering the viewer list does not narrow the deletion preview. TV shows expand into watched episodes rather than deleting the show folder or unwatched episodes.

Review every listed path. Deletion removes media from the library and its storage for every user. Emby's normal deletion behavior can also remove listed folders, metadata, subtitles, and other folder contents. The plugin blocks filesystem roots, configured library roots, and directory deletions containing other movie/episode library items. Merged versions or unusual shared-folder layouts may therefore require Emby's own media controls.

Check the acknowledgement, type DELETE, and choose Delete confirmed media. Only the frozen preview can run; newly watched episodes are not added after confirmation. Previews expire after ten minutes. Emby's deletion permissions, current watch status, playback sessions, and exact deletion paths are rechecked before each item. Local movies and episodes must qualify; channels, virtual media, and non-file sources are excluded. The selected administrator must already have Emby's content-deletion permission for the item.

The progress view lists deleted, blocked/skipped, and failed items. Stop after current item preserves the remaining items. Confirmed operations continue inside Emby if you leave the page, and are not replayed automatically after a server restart. Partial deletion can occur if Emby or the filesystem fails; refresh the library and review the result before retrying. Plugin rollback restores only the plugin DLL and cannot recover deleted media. Plugin installation/restart is blocked while deletion is running.

## Updates

The trusted repository is https://github.com/TechSavage1989/whosceneit. The update feed is the repository's main/update-manifest.json file. Release assets are published and verified before this feed advances to the new version.

The plugin checks in the background at server startup and every five minutes. Returning to the plugin page/window triggers a check if at least two minutes have passed. Manual checks have a 30-second cooldown. Conditional requests and backoff reduce repeated traffic. A failed/offline check reports failure and preserves any cached result without claiming to be current.

Choose Download, then Install and restart. The warning refers to Emby Server, so all of its clients briefly disconnect. Active/paused playback, scans, scheduled tasks, and plugin report queries block the restart. On hosts that cannot restart themselves, the action installs the DLL and asks you to restart through Emby's normal controls. If the operating system locks the loaded DLL, the update stops and reports an error; install the verified release manually with Emby stopped.

Downloads are a single DLL from the named repository. The plugin verifies allowed HTTPS hosts, size, SHA-256, the PE DLL header, assembly identity, and version. SHA-256 matching is integrity checking, not publisher signing. The binaries are unsigned.

Emby must load the new assembly on restart. When the plugin loads, it verifies the pending installation's version and digest. Cleanup waits until an administrator opens a visible dashboard page and the report API responds. The browser polls for reconnection and displays the loaded version. If the new plugin cannot load at all, its own code cannot automatically recover; use the rollback procedure below. Native restart/reconnection and recovery from an unloadable DLL remain unverified in this preview.

## Data and rollback

Plugin configuration stays in Emby's own plugin configuration directory. Update cache, staging, the pending-install record, and the single rollback DLL are under the directory WhoSceneIt inside Emby's DataPath. This is normally Server Data Folder/data/WhoSceneIt. The backup is rollback/WhoSceneIt.dll below that directory. Emby logs contain ordinary server/plugin errors; CSV exports go to the location chosen by your browser.

To roll back, stop Emby Server, copy the rollback DLL over the installed plugin DLL, remove the plugin-owned pending-install.json record, and start Emby again. Keep the installed DLL's actual filename if you customized it. Do not remove Emby configuration, library files, or user data. The updater preserves configuration and never marks a title watched or unwatched.

## Development

Read AGENTS.md and DEVELOPMENT_HANDOFF.md first. Source is in src/WhoSceneIt; report/update tests and browser tests are in tests; reproducible build, test, packaging, and release scripts are in scripts. No Emby server, credentials, or media files are included.
