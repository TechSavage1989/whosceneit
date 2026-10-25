# Who Scene It - development handoff

## Current state

- Latest prepared version: 1.0.0; assembly/file version: 1.0.0.0.
- Status: preview publication approved on October 25, 2026; hosted CI and publication verification are in progress. This is not a user-confirmed Emby baseline.
- Repository: https://github.com/TechSavage1989/whosceneit; confirmed public.
- Original main ref: c653762c30b37f3bb72ea3a3ac6b68aa7e8eb4b5, containing only a README. No prior releases, AGENTS.md, or DEVELOPMENT_HANDOFF.md existed.
- Prepared local source: d812efd4c77a42af2f290996eb267b5dde3b95a0 on codex/whosceneit-initial. Publication documentation records approval without changing application behavior. The release package build-metadata.json records the exact hosted source commit and whether its working tree was clean.
- Intended first tag: v1.0.0. No published tag or release exists yet.
- Update feed: https://raw.githubusercontent.com/TechSavage1989/whosceneit/main/update-manifest.json. Do not claim this feed works until an approved release and feed are hosted and verified.
- Stable assembly/filename: WhoSceneIt.dll; plugin ID: b6405a2e-52e6-4218-a014-00a46a65f1f8.

## Approved project scope

Build a new Emby plugin with watched titles by user and viewers by movie/show/episode, reliable update checks, explicit download/install controls, safe replacement, build assets, tests, and a clear future-chat handoff. Adapt desktop-specific requirements to Emby Server. Do not create a separate desktop EXE or add server setup, account setup, payments, unattended workers, or Codex CLI automation.

The user explicitly approved publishing the prepared source and v1.0.0 preview with "publish it" on October 25, 2026. Sync this approved source to the existing public repository, run hosted CI, and publish the preview after its required checks pass. No further publication confirmation is required within this scope. Do not change repository visibility or create credentials.

## Implementation

- ReportBuilder contains watch-status, grouping, count, date, filtering, and sorting logic.
- WatchInspectorService uses IUserManager, ILibraryManager, and IUserDataManager. Its routes are under WhoSceneIt and all require Authenticated(Roles = "Admin").
- Web/whosceneit.html and .js are embedded Emby dashboard resources. No external CDN resources, API credentials, or media files are needed.
- UpdateEntryPoint owns a System.Threading.Timer; checks start without awaiting network I/O. UpdateManager owns network single-flight, persisted cache/ETag/backoff, progress, explicit download/install, and post-restart assembly verification. Cleanup waits for a visible administrator dashboard with a successful report response.
- UpdateIntegrity allows only the exact source feed and repository release URLs, with restricted GitHub asset redirect hosts. Updates accept one DLL and validate size, SHA-256, PE DLL characteristics, assembly name, and version.
- UpdateInstaller copies to the target filesystem, flushes writes, uses atomic replacement, verifies the result, restores on replacement failure, and retains one previous DLL. It updates the assembly's actual location, including a customized filename.
- Restart guards cover active/paused playback, library scans, scheduled tasks, startup/shutdown, and active plugin report queries. A user-initiated restart is rechecked immediately before the host restart call.

## Build and checks

    dotnet build src/WhoSceneIt/WhoSceneIt.csproj -c Release
    python scripts/test.py --real-timer
    npm ci
    npx playwright install chromium
    npm run test:ui -- --real-timers
    python scripts/package.py

Target netstandard2.0 using official MediaBrowser.Server.Core 4.8.0.80. System.Memory is a build-time reference supplied at runtime by Emby; ship only the plugin DLL. The test runner targets net8.0. Browser tests use synthetic data, not a real Emby instance. Sixteen compiled checks and nine browser checks passed, including real timing, file rollback, and verified downloads with injected HTTP responses. See VALIDATION.md for exact test boundaries and blocked checks.

## Storage

- Configuration: Emby's plugin configuration directory, preserving its normal plugin identity.
- Update data: IApplicationPaths.DataPath/WhoSceneIt; normally Server Data Folder/data/WhoSceneIt.
- Rollback: that directory's rollback/WhoSceneIt.dll, exactly one previous binary.
- Cache: update-cache.json; staging: download.partial and downloaded.dll; transaction: pending-install.json.
- Temporary installation copy: a uniquely named .whosceneit-install-*.tmp in the installed DLL's directory, removed when the install operation ends.
- Report exports: user-controlled browser CSV downloads.
- Logs: normal Emby server logs. No private user data is exported or transmitted by update checks.

## Release procedure after explicit approval

1. Review the local source, completed tests, artifact hashes, and this handoff. Sync the approved source to the existing repository without changing its visibility.
2. Verify the exact pushed source commit and successful hosted CI on Windows and Linux, including the five-minute real timer check.
3. Invoke the manual release workflow with version 1.0.0 at that source ref. It refuses an existing tag/release, tags the exact source commit, creates a draft, uploads all artifacts, verifies them, publishes, then advances the main feed.
4. Verify public release URLs, uploaded assets, source tag/ref, published DLL SHA-256, and the public feed's matching version/digest.
5. Install the initial DLL with Emby stopped, then verify admin/non-admin access, both report views against known server data, and real server/browser lifecycle behavior.
6. After this first baseline is confirmed, publish a small 1.0.1 change and test a real 1.0.0-to-1.0.1 upgrade from the hosted feed through download, digest, staging, installation, Emby restart, browser reconnection, settings, cleanup, and rollback.
7. Update this handoff and VALIDATION.md with actual outcomes and approved source/ref. Never overwrite a published version.

## Known limitations

- An isolated Emby startup test was rejected by automatic approval review because Emby attempted to contact an external public-IP lookup service. Do not bypass or retry this rejected execution indirectly. No live Emby load, native authentication filter, restart, or reconnect result is claimed.
- Windows loaded-DLL replacement, Docker/NAS supervision, server crash recovery, hosted CI, public assets/feed, and a real published-version upgrade are unverified.
- A plugin cannot run its recovery code if a bad replacement prevents that plugin from loading. The single backup supports a manual restore with Emby stopped; the current implementation does not include an external watchdog.
- Saved watch flags, dates, and counts cannot reconstruct deleted history or distinguish all manual/imported marks from actual playback.
- The user report respects current selected-user access; the title report reads saved data for all current users. Short playback outside Emby's resumable query can be absent from the user report. Counts refer to current episode files, including specials and possible duplicate versions.
- A series-level manual mark without individual episode data is not treated as evidence for reconstructed episode counts.
- Filesystem and network tests use real .NET timers/files and injected HTTP responses. They do not substitute for an actual hosted release and native Emby install test.
