# Who Scene It - development handoff

## Current state

- Latest maintained/published version: 1.1.0 preview; assembly/file version: 1.1.0.0.
- User-confirmed baseline: 1.0.0. The user reported "it works great" on October 25, 2026, then requested individual and bulk watched-media deletion and publication of the next build. The new deletion feature is not yet user-confirmed.
- Repository: https://github.com/TechSavage1989/whosceneit; public. No visibility or credential changes were made.
- Exact 1.1.0 source/tag: 1d1d221b2b5fbc6e6f4ef0cebac8880115e09ccd, v1.1.0. The release build metadata records that source and a clean working tree.
- Release: https://github.com/TechSavage1989/whosceneit/releases/tag/v1.1.0. Six public assets were downloaded and verified, including the source and complete preview ZIPs. Never replace this tag or its assets.
- Published WhoSceneIt.dll: 126,976 bytes; SHA-256 2db640f20e5aca376a1d955a7ef8a8466bd60cddafeb7ca90b981f20fd9a55b6.
- Live feed: https://raw.githubusercontent.com/TechSavage1989/whosceneit/main/update-manifest.json, verified to match the public 1.1.0 manifest/version/digest.
- Stable assembly/filename: WhoSceneIt.dll; plugin ID: b6405a2e-52e6-4218-a014-00a46a65f1f8. Use the assembly's actual installed location so customized filenames survive updates.
- Local branch: codex/whosceneit-publication, tracking origin/main. Main additionally contains the verified publication handoff; the immutable release tag identifies the application source.
- Prior 1.0.0 source: 59d6cfe49406446ee76ca52abc5dd40cc8e91d9a. Original repository baseline was only a README at c653762c30b37f3bb72ea3a3ac6b68aa7e8eb4b5.

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

Hosted validation for exact release source 1d1d221b2b5fbc6e6f4ef0cebac8880115e09ccd passed at https://github.com/TechSavage1989/whosceneit/actions/runs/40178145566. Windows and Linux each passed all 29 compiled checks, including actual five-minute polling, two-minute focus gating, cooldown expiry, ETag/304, temporary-file deletion/rollback, cancellation, stale-target/native permission/playback/folder guards, partial errors, and update/deletion concurrency. Linux passed all 15 browser checks, including real polling and timer/listener cleanup. Release builds reported zero compiler warnings/errors.

Publication workflow https://github.com/TechSavage1989/whosceneit/actions/runs/40178653703 completed successfully, verified draft/public assets, and advanced/verified the feed. All six public downloads were independently size/hash-checked. All 41 source files match the tagged source; all 47 preview content hashes pass.

The actual public 1.0.0 updater passed a 1.0.0-to-1.1.0 test with real GitHub HTTPS feed/download, hash verification, staging, custom target filename, atomic replacement, one backup, configuration preservation, isolated new-assembly loading, and cleanup. Installed digest matched the published 1.1.0 DLL. This uses SDK host fixtures and does not start or restart Emby. See docs/validation-published-upgrade.txt.

The regular test runner creates an unpublished next-patch 1.1.1 fixture for the 1.1.0 source. Never publish that fixture. Browser fixtures contain synthetic data only. Native SDK fixture protocol/service stubs are test infrastructure, not a running Emby server.

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
- The actual public update transport/file transaction is verified with SDK fixtures. Native Emby process restart and deletion on a real library are not verified. Actual cross-volume target filesystems are not separately characterized.
