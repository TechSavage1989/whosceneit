# Who Scene It - development handoff

## Current state

- Current candidate: 1.2.0; exact-source hosted CI, publication, and public upgrade verification are pending. The latest published release below remains the baseline until those steps succeed.

- Latest maintained/published version: 1.1.1 preview; assembly/file version: 1.1.1.0.
- User-confirmed baseline: 1.0.0 reports. The user requested individual/bulk deletion, then supplied the empty-path installation error while updating to 1.1.0. Real-library deletion is not yet user-confirmed. Affected older installations need one manual DLL replacement to receive 1.1.1.
- Repository: https://github.com/TechSavage1989/whosceneit; public. No visibility or credential changes were made.
- Exact 1.1.1 source/tag: c29b1b94644168ec2ce63e359735fd244528fc3f, v1.1.1. Build metadata records this exact source and a clean tree. Never replace the published tag/assets.
- Release: https://github.com/TechSavage1989/whosceneit/releases/tag/v1.1.1. All six public assets were independently downloaded and size/hash-checked. All 44 source files match the tagged tree; all 50 preview content hashes pass.
- Published WhoSceneIt.dll: 131,584 bytes; SHA-256 9c79efe59c167cc7667cfa0fb85926bb532090b3dabd0ae7fb80e31e09949aad.
- Live feed: https://raw.githubusercontent.com/TechSavage1989/whosceneit/main/update-manifest.json, verified to match the public 1.1.1 manifest. Feed commit: 4251cc01a912521b5582d821db765371b95d6073, created only after public asset verification.
- Stable assembly/filename: WhoSceneIt.dll; plugin ID: b6405a2e-52e6-4218-a014-00a46a65f1f8. Resolve the native installed path lazily because byte-loaded Assembly.Location can be empty; preserve customized filenames.
- Local branch: codex/whosceneit-publication, tracking origin/main. Main holds current verification notes; immutable release tags identify application source.
- Previous immutable sources: 1.1.0 at 1d1d221b2b5fbc6e6f4ef0cebac8880115e09ccd; 1.0.0 at 59d6cfe49406446ee76ca52abc5dd40cc8e91d9a. The original repository baseline was only a README at c653762c30b37f3bb72ea3a3ac6b68aa7e8eb4b5.

## Version 1.2.0 watch progress

The user requested full/partial episode detection, related features, and publication. WatchProgress separates Started, Watched, Partial, Resuming, NotStarted, and All. Played remains the saved completion flag; positive PlaybackPositionTicks is saved progress. A watched item with a position is labelled Watched with resume position, including possible rewatches, without claiming a second viewing occurred. Positions at or past runtime remain partial unless Played is true. Missing/invalid durations never produce fabricated percentages or remaining time.

WatchInspectorService reads the full accessible media inventory instead of relying on IsResumable, so below-threshold saved positions and not-started episodes can appear. Series totals count current available episode records, including specials, and retain the full denominator when filtering. UserReport accepts WatchState and an optional validated SeriesId for Episodes. Omitting WatchState preserves the legacy IncludeInProgress selection behavior. Both existing report routes still require native Admin authentication.

The dashboard defaults to Started, adds watch-state/progress/episode-order controls, progress bars and remaining time, and View episodes in both report views. Episode drilldown keeps the selected user and show together. CSV includes the same state/scope and progress fields. Reports do not modify watch flags or resume data. The native deletion backend now preserves media with any current user's saved position, even if another user completed it, and rechecks this before deletion. Local/native/browser fixtures use synthetic data only.

Local validation passed 42 compiled checks and 20 browser checks including real browser timers. Hosted real-timer validation is expected to run 43 compiled checks per platform and 20 Linux browser checks. A long status option initially caused mobile overflow; constrained form fields and a shorter label pass the mobile check. The public check now additionally uses the pinned public 1.1.1 DLL, creates its actual entry point from bytes, installs the public new release, verifies custom target/settings/backup, and loads the new entry point to confirm pending health and cleanup. Run it only after publication/feed verification.

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

Current 1.1.1 validation: https://github.com/TechSavage1989/whosceneit/actions/runs/40180285235, successful at exact source c29b1b94644168ec2ce63e359735fd244528fc3f. Windows/Linux each passed 34 compiled checks; Linux passed 15 browser checks. Publication workflow https://github.com/TechSavage1989/whosceneit/actions/runs/40180822733 completed successfully, verified assets, and advanced the feed. Independent public verification and the revised memory-load/manual-bootstrap check passed. No live Emby process was started or restarted.

Historical 1.1.0 hosted validation for exact release source 1d1d221b2b5fbc6e6f4ef0cebac8880115e09ccd passed at https://github.com/TechSavage1989/whosceneit/actions/runs/40178145566. Windows and Linux each passed all 29 compiled checks, including actual five-minute polling, two-minute focus gating, cooldown expiry, ETag/304, temporary-file deletion/rollback, cancellation, stale-target/native permission/playback/folder guards, partial errors, and update/deletion concurrency. Linux passed all 15 browser checks, including real polling and timer/listener cleanup. Release builds reported zero compiler warnings/errors.

Publication workflow https://github.com/TechSavage1989/whosceneit/actions/runs/40178653703 completed successfully, verified draft/public assets, and advanced/verified the feed. All six public downloads were independently size/hash-checked. All 41 source files match the tagged source; all 47 preview content hashes pass.

The historical public 1.0.0-to-1.1.0 test passed real GitHub HTTPS transport and file replacement with an explicitly supplied target. It bypassed UpdateEntryPoint and did not establish that Emby supplied a usable installation path. The user screenshot disproved that assumption. See the 1.1.1 repair and revised post-publication check above; retain the old output only as historical file-transaction evidence.

The current test runner creates an unpublished next-patch 1.2.1 fixture for the 1.2.0 source. The old 1.1.0 source generated a test-only 1.1.1 fixture; it is not the patched release and must never be published. Never publish that fixture. Browser fixtures contain synthetic data only. Native SDK fixture protocol/service stubs are test infrastructure, not a running Emby server.

Negative controls: historical Windows run 40174933095 missed its timer request under the redundant five-minute wall-clock gate. The corrected real timer passes on both platforms. A local run restoring the broad update Busy guard reproduced interruption of deletion by a background check; the corrected IsInstalling guard passes that same test.

## Storage

- Configuration: Emby's normal plugin configuration directory, preserving the established identity.
- Update data: IApplicationPaths.DataPath/WhoSceneIt, normally Server Data Folder/data/WhoSceneIt.
- Rollback: rollback/WhoSceneIt.dll under that data directory, exactly one previous binary.
- Cache: update-cache.json; staging: download.partial and downloaded.dll; transaction: pending-install.json.
- Temporary atomic copy: .whosceneit-install-*.tmp in the installed DLL directory, removed after installation.
- Deletion previews/progress: bounded memory, scoped to the initiating administrator. No watch history or media is transmitted to GitHub.
- CSV exports: browser downloads. Logs: normal Emby logs. Media deletion is permanent; DLL rollback cannot restore media.

## Future release procedure after explicit approval

1. Read AGENTS.md and this handoff. Inspect latest source, visibility, releases, and feed. Preserve the latest maintained feature set and the user-confirmed baseline.
2. Use a new matching project/assembly/file version. Verify all required hosted CI jobs for the exact release source.
3. Invoke the manual release workflow at that source ref. Existing tags/releases must not be overwritten. Draft verification resolves the authenticated release list and reads the stable release ID.
4. Verify public assets, source tag/ref, hashes, metadata, and matching feed. The feed must advance only after public asset validation.
5. Use the explicit published-upgrade check after publication, and record its boundaries. Native Emby update/restart and real-library deletion require separate target-platform validation.
6. Update this handoff and VALIDATION.md with actual results. Frozen release packages retain pre-publication notes; main holds current verification details.

## Known limitations

- Automated isolated Emby startup was rejected by automatic approval review because Emby attempted an external public-IP lookup. Do not bypass or silently repeat it. The user subsequently confirmed 1.0.0 works; automated native authentication, real-media deletion, server restart, and dashboard reconnection remain unverified.
- Windows replacement of a DLL loaded by live Emby, Docker/NAS supervision, and recovery from a DLL that prevents plugin loading remain unverified. SDK/file tests and isolated assembly reload do not establish those results.
- A bad plugin that cannot load cannot execute its own rollback. Stop Emby and restore the single rollback DLL manually when needed; no external watchdog exists.
- Real Emby/filesystem errors can leave media partially deleted. The UI states this explicitly. Folder layouts containing other library media or merged versions can require Emby's controls. Confirmed jobs are not automatically replayed after an Emby restart.
- Saved watch flags/counts cannot reconstruct deleted history or reliably distinguish manual/imported marks from playback. Current user access, resumable thresholds, current episode files/specials/versions, and deleted users limit report completeness. Series-only manual marks do not fabricate episode data.
- Historical public transport/file replacement used an explicit target; affected old byte-loaded updaters cannot install this patch automatically. Native Emby process restart and deletion on a real library are not verified. Actual cross-volume target filesystems are not separately characterized.
