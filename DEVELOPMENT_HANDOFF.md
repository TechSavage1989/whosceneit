# Who Scene It - development handoff

## Current state

- Current candidate: 1.3.0. Local validation, exact-source hosted CI, publication, and public previous-version upgrade verification must be recorded below. The latest screenshot identifies a missing legacy visibility-method signature; the fix retains native user visibility through a supported query. The user also requested Sonarr/Radarr synchronization.

- Latest maintained/published version: 1.2.2 preview; assembly/file version: 1.2.2.0.
- User-confirmed baseline: 1.0.0 reports. A subsequent deletion screenshot identifies MissingMethodException for the legacy two-argument visibility method. Version 1.2.2 exposed that exception; 1.3.0 replaces the call. Live successful deletion remains unverified.
- Repository: https://github.com/TechSavage1989/whosceneit; public. No visibility or credential changes were made.
- Exact 1.2.2 source/tag: 43494290988f57813fdfcb6ca9a42d225951f5b5, v1.2.2. Build metadata records that exact source and a clean tree. Never replace published tags/assets.
- Release: https://github.com/TechSavage1989/whosceneit/releases/tag/v1.2.2. All six public assets were independently downloaded and size/hash-checked. All 48 source files match the tagged tree; all 54 preview content hashes pass.
- Published WhoSceneIt.dll: 150,016 bytes; SHA-256 dc87b73fd77e893e22eb1ec2833f792f42f79c74962a0807ed32f0db6185bc7a.
- Live feed: https://raw.githubusercontent.com/TechSavage1989/whosceneit/main/update-manifest.json, independently verified through direct canonical HTTPS to match the public 1.2.2 manifest. Feed commit: 24c1f8aafc5b633cc9921b5baf614bfd1b5f711f.
- Hosted validation: https://github.com/TechSavage1989/whosceneit/actions/runs/40220322970, successful; Windows/Linux each passed 49 compiled checks, Linux passed all 23 browser checks including real timers. Release builds had zero warnings/errors.
- Publication run https://github.com/TechSavage1989/whosceneit/actions/runs/40220959204 succeeded, verified assets, and advanced/verified the canonical feed.
- The actual public 1.2.1 updater installed exact public 1.2.2 bytes using memory-loaded entry points in a temporary SDK fixture, preserving settings/custom filename/backup and verifying pending reload state/cleanup. The legacy 1.0.0 failure/manual-bootstrap negative control also passed. See docs/validation-published-upgrade.txt. No live Emby process was started or restarted.
- Stable assembly/filename: WhoSceneIt.dll; plugin ID: b6405a2e-52e6-4218-a014-00a46a65f1f8. Customized filenames/settings survive updates. Versions 1.0.0/1.1.0 need manual replacement once.
- Local branch: main, tracking origin/main. Main holds current verification notes; immutable release tags identify application source.
- Previous immutable sources: 1.2.1 at c68e1714c67c488adac12dda8b7c87e08b3f9126; 1.2.0 at e2be637d3e333d14fb6ae6e6a36c2b95dca59aaf; 1.1.1 at c29b1b94644168ec2ce63e359735fd244528fc3f; 1.1.0 at 1d1d221b2b5fbc6e6f4ef0cebac8880115e09ccd; 1.0.0 at 59d6cfe49406446ee76ca52abc5dd40cc8e91d9a. Original baseline: c653762c30b37f3bb72ea3a3ac6b68aa7e8eb4b5.

## Version 1.3.0 compatibility and synchronization

The new screenshot shows the native visibility method cannot be linked: FilterItemsToIdsForUser(BaseItem[], User). Current Emby reference documentation instead shows the overload with IDataContext and CancellationToken. The production adapter now uses GetItemList(new InternalItemsQuery(selectedUser)) with exact ItemIds, EnforceContentRestriction=true, and SDK collection defaults. Native fixtures reject the old method; restoring the old production call fails the check, while the repaired query still blocks inaccessible media. This is an SDK compatibility fix, not a permission bypass or proof of a live server deletion.

The user requested deleting managed entries from Sonarr/Radarr as part of media deletion. New Admin-only integration endpoints read masked settings, save changes, and run read-only connection tests. PluginConfiguration.Arr stores the two optional connections with native XML serialization. Blank keys preserve the saved value; clearing is explicit. Tests initialize the SDK's SetStartupInfo directory callback before native SaveConfiguration. Keys never appear in API views, preview/status, URLs, or logs. Default connections are disabled, preserving existing installations until configured.

Each preview freezes settings and exact remote IDs/paths/sizes. Caches hold catalogs only during preview and are cleared before retaining the plan. A failed read stops that service for the current batch. Matching uses path boundaries/case rules and size, never display titles. Sonarr requires all file-linked episode IDs to fit the selected Emby episode/range. Duplicate remote targets block all affected entries. Server-side settings saves are excluded for the full running operation, and changes invalidate old previews.

After confirmation, the session revalidates remote targets, unmonitors them using Sonarr episode/monitor or Radarr movie/editor, and verifies monitoring by read-back. Emby's adapter then rechecks watch state, permissions, sessions, paths, and fingerprints and performs native deletion. Only after native success does cleanup remove a Sonarr episode-file record or a Radarr movie record. Radarr uses deleteFiles=false; its optional import-list exclusion is requested. Sonarr retains the series/episode listings. A replacement file at Emby's original media path stops Sonarr cleanup. Missing records and lost mutation receipts are checked through reads; mutations are never replayed automatically.

Results keep EmbyState, ExternalService/State/Message/Path, Synchronized, and SyncFailed separate from native failure counts. A remote failure after native success still counts the media as deleted and flags synchronization. A native failure can remain Unknown and never launches external cleanup. Cancellation/skipped items report service actions as Not run. There is no atomic cross-application transaction or media rollback; failures may need review in the remote service. Sonarr's own root-folder protections remain in effect. URLs support native base paths; redirects are rejected, response sizes/timeouts are bounded, and existing TLS validation remains enabled. The connection test detects Sonarr v3/v5 API routes and Radarr v3, but no live instance is used.

The HTML uses whosceneit-1.3.0.js, with a matching page resource and the old alias retained. New JavaScript gives outdated HTML a reload notice. After installation, use Ctrl+F5 to discard cached controls. New browser fixtures cover masked keys, draft connection testing, save/reload, exact preview actions, independent partial results, safe text rendering, mobile settings, and stale HTML.

Local checks: 62 quick compiled checks and 26 browser checks including real timers have passed during development. The final exact source must pass hosted 63 compiled checks on Windows/Linux and 26 browser checks on Linux before publication. The compatibility negative control is in docs/validation-deletion-compatibility-negative.txt. After publication, run the pinned public 1.2.2 memory-loaded updater against 1.3.0 and verify all assets/tag/source/feed. No Emby process, real Sonarr/Radarr endpoint, or user media is used in these checks.

## Version 1.2.2 deletion feedback

The user reports that Delete selected media opens a prompt without confirmation/progress. The screenshot shows the preview request failed with HTTP 500. In the existing controller, Execute is sent only after the separate acknowledgement and typed DELETE step, so this prompt does not establish that deletion started. The exact live server exception is absent.

The browser now decodes native Fetch Response error bodies, including Emby's JSON error message, instead of expecting only XHR responseJSON/responseText. The official archived MediaBrowser/Emby.ApiClient.Javascript source returns rejected Fetch responses for HTTP errors. The UI distinguishes preview failure (no deletion started), read-only retry, eligible review/confirmation, submission, active counts/current item/progress, unknown status, and final outcomes. Unknown status never clears the token or replays Execute. The progress dialog can close/reopen the current operation without cancelling it; status is not polled while the Execute request is still awaiting a response. A retry requires fresh confirmation. All-blocked previews offer no destructive confirmation.

DeletionService returns a structured PreviewFailed status for preview errors while retaining native Admin route attributes and request-bound actor policy checks. DeletionManager converts individual inspection exceptions to blocked entries, and tracks Started, Processing, Processed, and CurrentItemName. Actual native deletion is still performed only through the guarded Emby backend. Native service tests cover IRequest-bound administrator identity, no-user/API-key-only/non-admin rejection, full preview/execute/status/replay flow with synthetic files, current-item/cancellation counts, and blocked inspection failures.

Local validation passed 48 quick compiled checks and 23 browser checks including real timers. An incomplete local browser process produced only four PASS lines with a zero exit status; that run is not accepted. A full rerun passed all 23, and the runner now rejects incomplete early exits. Exact-source hosted Windows/Linux each passed all 49 compiled checks, including real timers; Linux passed all 23 browser checks. Publication, all six public assets, source/preview archives, tag, and canonical feed passed verification. The pinned public 1.2.1 memory-loaded updater installed exact public 1.2.2 bytes and passed settings/custom-target/backup/pending-health/cleanup checks after publication. No live Emby process or real user media is used. The underlying HTTP 500 remains unobserved; new error handling must report its cause if an environmental or native issue remains.

## Version 1.2.1 report-query repair

The user's 1.2.0 screenshot shows the new UI and installed version, but UserReport fails with the old generic message. A verified regression was found in the new query: the SDK initializes AncestorIds to an empty long array, but the unscoped 1.2.0 query overwrites it with null. The new test fails against that source and verifies default preservation for Titles, Movies, Shows, and Episodes after correction. Scoped episodes still pass the selected series ID. The previous fixture did not inspect the unscoped array.

The patch creates the query with native defaults and only assigns AncestorIds for a validated show scope. Browser errors now show ResponseStatus.Message and HTTP status through textContent, omit stack traces and returned HTML, and retain authorization guidance. Loading/failed reports reset reportHealthy, and failure clears stale selections. A browser fixture covers JSON errors, non-JSON gateway errors, authorization failures, false health acknowledgement prevention, and successful retry. No watch data or settings are changed.

Local validation passed 43 compiled checks and 21 browser checks including real timers. Exact-source hosted Windows and Linux each passed 44 compiled checks; Linux passed all 21 browser checks. The post-publication helper now uses the pinned public 1.2.0 updater to install 1.2.1 in a temporary plugins directory; it passed after publication, verifying exact public bytes, the custom target, settings/rollback preservation, pending version/digest verification, and simulated dashboard acknowledgement/cleanup. The older 1.0.0 failure/manual bootstrap also passed. See docs/validation-published-upgrade.txt. The screenshot does not expose the server exception, and no live Emby process is used in these checks. The SDK-default repair is verified; confirmation against the user's report remains pending.

## Version 1.2.0 watch progress

The user requested full/partial episode detection, related features, and publication. WatchProgress separates Started, Watched, Partial, Resuming, NotStarted, and All. Played remains the saved completion flag; positive PlaybackPositionTicks is saved progress. A watched item with a position is labelled Watched with resume position, including possible rewatches, without claiming a second viewing occurred. Positions at or past runtime remain partial unless Played is true. Missing/invalid durations never produce fabricated percentages or remaining time.

WatchInspectorService reads the full accessible media inventory instead of relying on IsResumable, so below-threshold saved positions and not-started episodes can appear. Series totals count current available episode records, including specials, and retain the full denominator when filtering. UserReport accepts WatchState and an optional validated SeriesId for Episodes. Omitting WatchState preserves the legacy IncludeInProgress selection behavior. Both existing report routes still require native Admin authentication.

The dashboard defaults to Started, adds watch-state/progress/episode-order controls, progress bars and remaining time, and View episodes in both report views. Episode drilldown keeps the selected user and show together. CSV includes the same state/scope and progress fields. Reports do not modify watch flags or resume data. The native deletion backend now preserves media with any current user's saved position, even if another user completed it, and rechecks this before deletion. Local/native/browser fixtures use synthetic data only.

Local validation passed 42 compiled checks and 20 browser checks including real browser timers. Exact-source hosted Windows and Linux each passed 43 compiled checks, including real timers; Linux passed 20 browser checks. A long status option initially caused mobile overflow; constrained form fields and a shorter label pass the mobile check. The public check now additionally uses the pinned public 1.1.1 DLL, creates its actual entry point from bytes, installs the public new release, verifies custom target/settings/backup, and loads the new entry point to confirm pending health and cleanup. It passed after publication/feed verification: the public 1.1.1 updater installed the exact public 1.2.0 bytes, preserved settings/custom filename/backup, and the new memory-loaded entry point verified pending health and cleaned staging. The legacy 1.0.0 failure/manual-bootstrap negative control also passed. See docs/validation-published-upgrade.txt. No live Emby process was started or restarted.

## Version 1.1.1 installation-path repair

The user supplied an installed 1.0.0 dashboard screenshot with available 1.1.0 and the error "The value cannot be an empty string. (Parameter 'path')". UpdateEntryPoint passed Assembly.Location to the installer. Byte-loaded assemblies have an empty location. The earlier public upgrade fixture supplied a valid target directly, so it verified transport and file replacement but missed the actual entry-point failure.

PluginPathResolver now prefers matching IApplicationHost.Plugins / IPlugin.AssemblyFilePath metadata, then a unique matching DLL in PluginsPath. It validates the installed assembly identity/version, preserves custom filenames, and rejects missing, duplicate, wrong-version, and shadow-cache targets. The entry point supplies a lazy provider so later Emby attribute registration is supported. The original UpdateManager constructor is preserved. Reports and deletion behavior are unchanged.

Affected 1.0.0 and 1.1.0 installations need one manual replacement: stop Emby, replace the existing plugin DLL with the official 1.1.1 DLL, preserve any customized filename, and start Emby. Do not add a second DLL beside the old one. Settings are retained by preserving the plugin identity/configuration. Advancing the feed cannot repair code already loaded by the old updater.

Local Release builds have zero warnings/errors. All 33 quick compiled checks pass, including five new memory-loaded entry-point cases and the existing deletion/update checks. Exact-source hosted Windows and Linux each passed all 34 checks, including the real timer; Linux passed all 15 browser checks. New cases reproduce the old empty-path failure, test native registered/custom filenames, fallback ambiguity, wrong targets, late registration, actual installation/backup/settings preservation, new memory loading, and health cleanup. The rewritten public check passed after publication: it reproduced the exact public 1.0.0 memory-loaded entry-point failure after a verified HTTPS patch download, then simulated the documented manual bootstrap into the public 1.1.1 DLL. The patched entry point resolved the custom target; settings/backup were preserved and owned staging was cleaned. See docs/validation-published-upgrade.txt. No Emby process or real user library is used.

## Approved project scope

Maintain an Emby Server plugin with watched titles by user, viewers by movie/show/episode, CSV export, reliable explicit updates, tests, packaging, and a future-chat handoff. This is not a desktop EXE, PowerShell app, Jellyfin plugin, or hosted website.

The user explicitly authorized public source/release publication, confirmed 1.0.0 works, and requested individual and bulk deletion of watched media plus publication of the new build. This expands the original report-only scope to administrator-confirmed permanent media deletion through Emby's native API. Preserve the current feature set. Do not change permissions, metadata, or watch flags to make an item eligible. Do not delete actual user media during development/testing. Do not change visibility, create credentials, or add server setup, Codex CLI automation, unattended workers, payments, or API-account setup.

## Implementation

- ReportBuilder implements watch-state grouping, counts, filtering, dates, and sorting. WatchInspectorService uses IUserManager, ILibraryManager, and IUserDataManager. All report/update/deletion routes require native Authenticated(Roles = "Admin"). Reports remain read-only.
- Embedded dashboard resources are Web/whosceneit.html and .js. No external CDN scripts, API credentials, or media files are required.
- DeletionService adds POST Delete/Preview, POST Delete/Execute, GET Delete/Status, and POST Delete/Cancel under WhoSceneIt. IAuthorizationContext/IRequiresRequest resolve the actual signed-in administrator; an API-key-only context without a user is rejected.
- DeletionManager keeps bounded, administrator-bound, ten-minute previews in memory, with at most 500 expanded media items. It snapshots exact targets, rejects duplicate physical paths, requires exact DELETE confirmation, prevents concurrent jobs/replayed execution, and reports per-item outcomes. Jobs begin only after confirmation, continue inside Emby if the page closes, support stopping remaining items, and are never persisted for automatic replay.
- EmbyDeletionBackend expands series into watched episodes, rechecks visibility/watch state, native CanDelete permissions, active/paused playback (numeric and GUID IDs), and deletion paths. It rejects folders/non-file/virtual items, filesystem/library roots, and directory removal containing other library movies/episodes. Fingerprints cover type, paths, size, and timestamps. Inspection repeats before ILibraryManager.DeleteItem with DeleteFileLocation=true, DeleteFromExternalProvider=false, collection folders, and parent notification. Media removal never uses custom filesystem deletion.
- By user supports individual buttons, page selection, selections across pages, and bulk preview. Scope/filter changes clear selections. By title includes media watched by any current user, independently of viewer filtering. Both views show exact paths and typed confirmation. In-progress-only entries cannot be selected.
- UpdateEntryPoint owns a server Timer. Startup checks do not await network I/O. UpdateManager supplies single-flight, cache/ETag/backoff, explicit download/install, verified replacement, and one rollback DLL. The timer supplies the five-minute cadence; its callback uses the short cross-trigger cooldown to avoid applying that interval twice. Focus uses two minutes; manual checks use 30 seconds.
- Background update checks/downloads can coexist with deletion. IsInstalling or a pending restart excludes deletion. A full-operation ReportActivity lease blocks plugin installation/restart throughout a deletion job. Existing playback, scan, scheduled-task, startup/shutdown, and report guards remain.
- UpdateIntegrity validates the exact HTTPS feed/repository and redirect hosts, sizes, hashes, PE DLL header, assembly identity, and version. The installer stages on the target filesystem, verifies atomic replacement, restores on replacement failure, and retains one backup. Hash matching is not publisher signing; the DLL is unsigned.
- Reload health checks verify the pending version/digest. Cleanup waits for a visible administrator dashboard with a successful report response. Do not equate an isolated assembly reload or file replacement with a verified native Emby restart.

## Build and checks

Use .NET SDK 8 or newer. Production targets netstandard2.0 against official MediaBrowser.Server.Core 4.8.0.80. System.Memory is supplied at runtime by Emby. Install only WhoSceneIt.dll.

    dotnet build src/WhoSceneIt/WhoSceneIt.csproj -c Release
    python scripts/test.py --real-timer
    npm ci
    npx playwright install chromium
    npm run test:ui -- --real-timers
    python scripts/package.py

After publication and feed verification, explicitly run the public previous-version upgrade check:

    dotnet run --project tests/WhoSceneIt.Tests/WhoSceneIt.Tests.csproj -c Release -- --published-upgrade

Current 1.2.2 validation: https://github.com/TechSavage1989/whosceneit/actions/runs/40220322970, successful at source 43494290988f57813fdfcb6ca9a42d225951f5b5. Windows/Linux each passed 49 compiled checks; Linux passed all 23 browser checks. Publication workflow https://github.com/TechSavage1989/whosceneit/actions/runs/40220959204 completed successfully. All six public assets/tag/feed, 48 source files/54 preview hashes, and the actual public 1.2.1-to-1.2.2 updater file transaction/health/cleanup passed independent verification.

Historical 1.2.1 validation: https://github.com/TechSavage1989/whosceneit/actions/runs/40216607262, successful at source c68e1714c67c488adac12dda8b7c87e08b3f9126. Windows/Linux each passed 44 compiled checks; Linux passed 21 browser checks. Public assets/tag/feed and a real public 1.2.0-to-1.2.1 updater file transaction passed independent verification. Publication workflow 40217267071 timed out only after publishing and advancing the feed; its failure status remains historical. No asset/tag was replaced.

Historical 1.2.0 validation: https://github.com/TechSavage1989/whosceneit/actions/runs/40213700764, successful at exact source e2be637d3e333d14fb6ae6e6a36c2b95dca59aaf. Windows/Linux each passed 43 compiled checks; Linux passed 20 browser checks. Publication workflow https://github.com/TechSavage1989/whosceneit/actions/runs/40214340337 completed successfully. All six assets, source/preview archives, metadata, tag, and feed passed independent public verification. Actual public memory-loaded 1.1.1-to-1.2.0 updater installation and new-entry-point health/cleanup passed in SDK fixtures.

Historical 1.1.1 validation: https://github.com/TechSavage1989/whosceneit/actions/runs/40180285235, successful at exact source c29b1b94644168ec2ce63e359735fd244528fc3f. Windows/Linux each passed 34 compiled checks; Linux passed 15 browser checks. Publication workflow https://github.com/TechSavage1989/whosceneit/actions/runs/40180822733 completed successfully, verified assets, and advanced the feed. Independent public verification and the revised memory-load/manual-bootstrap check passed. No live Emby process was started or restarted.

Historical 1.1.0 hosted validation for exact release source 1d1d221b2b5fbc6e6f4ef0cebac8880115e09ccd passed at https://github.com/TechSavage1989/whosceneit/actions/runs/40178145566. Windows and Linux each passed all 29 compiled checks, including actual five-minute polling, two-minute focus gating, cooldown expiry, ETag/304, temporary-file deletion/rollback, cancellation, stale-target/native permission/playback/folder guards, partial errors, and update/deletion concurrency. Linux passed all 15 browser checks, including real polling and timer/listener cleanup. Release builds reported zero compiler warnings/errors.

Publication workflow https://github.com/TechSavage1989/whosceneit/actions/runs/40178653703 completed successfully, verified draft/public assets, and advanced/verified the feed. All six public downloads were independently size/hash-checked. All 41 source files match the tagged source; all 47 preview content hashes pass.

The historical public 1.0.0-to-1.1.0 test passed real GitHub HTTPS transport and file replacement with an explicitly supplied target. It bypassed UpdateEntryPoint and did not establish that Emby supplied a usable installation path. The user screenshot disproved that assumption. See the 1.1.1 repair and revised post-publication check above; retain the old output only as historical file-transaction evidence.

The current test runner creates an unpublished next-patch 1.3.1 fixture for the 1.3.0 source. The old 1.2.2 source's 1.2.3 fixture is unrelated to this release. The old 1.2.1 source's 1.2.2 test fixture is not this release and must never be published. The old 1.2.0 source's same-numbered 1.2.1 fixture is not this patch and must never be published. The old 1.1.0 source generated a test-only 1.1.1 fixture; it is not the patched release and must never be published. Never publish that fixture. Browser fixtures contain synthetic data only. Native SDK fixture protocol/service stubs are test infrastructure, not a running Emby server.

Negative controls: historical Windows run 40174933095 missed its timer request under the redundant five-minute wall-clock gate. The corrected real timer passes on both platforms. A local run restoring the broad update Busy guard reproduced interruption of deletion by a background check; the corrected IsInstalling guard passes that same test.

## Storage

- Configuration: Emby's normal plugin configuration directory, preserving the established identity. PluginConfiguration.Arr contains optional URLs, server-held API keys, path prefixes, enabled flags, and Radarr exclusion preference; protect it as credential-bearing configuration.
- Update data: IApplicationPaths.DataPath/WhoSceneIt, normally Server Data Folder/data/WhoSceneIt.
- Rollback: rollback/WhoSceneIt.dll under that data directory, exactly one previous binary.
- Cache: update-cache.json; staging: download.partial and downloaded.dll; transaction: pending-install.json.
- Temporary atomic copy: .whosceneit-install-*.tmp in the installed DLL directory, removed after installation.
- Deletion previews/progress: bounded memory, scoped to the initiating administrator. No watch history or media is transmitted to GitHub.
- CSV exports: browser downloads. Logs: normal Emby logs. Media deletion is permanent; DLL rollback cannot restore media.

## Future release procedure after explicit approval

1. Read AGENTS.md and this handoff. Inspect latest source, visibility, releases, and feed. Preserve the latest maintained feature set and the user-confirmed baseline.
2. Use a new matching project/assembly/file version. Verify all required hosted CI jobs for the exact release source.
3. Invoke the manual release workflow at that source ref. Existing tags/releases must not be overwritten. Draft verification resolves the authenticated release list and reads the stable release ID. Main now allows up to six minutes for canonical feed verification after the observed one-minute timeout in the 1.2.1 publication. If a release is already public, independently verify it rather than rerunning publication for the same tag.
4. Verify public assets, source tag/ref, hashes, metadata, and matching feed. The feed must advance only after public asset validation.
5. Use the explicit published-upgrade check after publication, and record its boundaries. Native Emby update/restart and real-library deletion require separate target-platform validation.
6. Update this handoff and VALIDATION.md with actual results. Frozen release packages retain pre-publication notes; main holds current verification details.

## Known limitations

- Sonarr/Radarr API behavior is tested with synthetic HTTP fixtures, including lost responses and native XML persistence. Real installed versions, authentication proxies, path aliases, and media deletion across the user's services remain unverified. One instance/mapping per service is supported. Partial cross-app failures need review; a successful Emby deletion cannot be rolled back by a failed remote update.

- Automated isolated Emby startup was rejected by automatic approval review because Emby attempted an external public-IP lookup. Do not bypass or silently repeat it. The user subsequently confirmed 1.0.0 works; automated native authentication, real-media deletion, server restart, and dashboard reconnection remain unverified.
- Windows replacement of a DLL loaded by live Emby, Docker/NAS supervision, and recovery from a DLL that prevents plugin loading remain unverified. SDK/file tests and isolated assembly reload do not establish those results.
- A bad plugin that cannot load cannot execute its own rollback. Stop Emby and restore the single rollback DLL manually when needed; no external watchdog exists.
- Real Emby/filesystem errors can leave media partially deleted. The UI states this explicitly. Folder layouts containing other library media or merged versions can require Emby's controls. Confirmed jobs are not automatically replayed after an Emby restart.
- Saved watch flags/counts cannot reconstruct deleted history or reliably distinguish manual/imported marks from playback. Current user access, resumable thresholds, current episode files/specials/versions, and deleted users limit report completeness. Series-only manual marks do not fabricate episode data.
- Historical public transport/file replacement used an explicit target; affected old byte-loaded updaters cannot install this patch automatically. Native Emby process restart and deletion on a real library are not verified. Actual cross-volume target filesystems are not separately characterized.
