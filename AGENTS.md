# Who Scene It - read first

Read this file and DEVELOPMENT_HANDOFF.md before editing. Then inspect the current repository, release feed, and releases. This is an Emby Server plugin, not a Windows desktop executable, PowerShell app, hosted website, or Jellyfin plugin.

## Scope and baseline

- Repository: https://github.com/TechSavage1989/whosceneit
- The repository was public with only an initial README at commit c653762c30b37f3bb72ea3a3ac6b68aa7e8eb4b5 when work began.
- Latest maintained/published application: 1.1.0 preview, tag v1.1.0 at 1d1d221b2b5fbc6e6f4ef0cebac8880115e09ccd. Hosted Windows/Linux CI passed 29 compiled checks each; Linux passed 15 browser checks. All six public assets/feed and the old 1.0.0 updater file transaction are verified with SDK host fixtures.
- User-confirmed working baseline: 1.0.0, tag v1.0.0 at 59d6cfe49406446ee76ca52abc5dd40cc8e91d9a. The user confirmed it works on October 25, 2026. Version 1.1.0 adds the explicitly requested media deletion feature.
- Public source publication and release publication require the user's explicit approval. A repository URL alone does not override that instruction. Do not change visibility, create credentials, or embed tokens.
- Preserve the stable assembly name and installed filename WhoSceneIt.dll and plugin ID b6405a2e-52e6-4218-a014-00a46a65f1f8 across versions. Use the assembly's actual installed location for updates so custom filenames survive.
- Keep reports read-only. The user explicitly approved individual and bulk deletion of watched media and publication of the new build. Deletion must use Emby's native policy checks and library deletion API, an administrator-bound expiring preview, exact-path revalidation, explicit DELETE confirmation, and progress/error reporting. Never delete a TV show folder to implement watched-episode deletion. Never grant permissions or change watch state to make an item eligible. Every plugin endpoint must require Emby's native Admin role.
- No server setup, Codex CLI automation, unattended workers, payment setup, or API-account setup. GitHub CI and an explicitly invoked release workflow are part of the requested build/release scope.

## Build and test

Use .NET SDK 8 or newer. The plugin must target netstandard2.0. Emby supplies its own runtime and SDK assemblies; install only WhoSceneIt.dll.

    dotnet build src/WhoSceneIt/WhoSceneIt.csproj -c Release
    python scripts/test.py --real-timer
    npm ci
    npx playwright install chromium
    npm run test:ui -- --real-timers
    python scripts/package.py

The C# test runner generates an unpublished next-patch DLL to exercise upgrades from the current version (1.1.1 for the 1.1.0 source). Never publish that fixture. The real timer test takes about five minutes. Browser tests use synthetic data and never connect to a user's Emby server. After publication, the explicit --published-upgrade test mode exercises the actual published 1.0.0 updater against the current public feed using SDK host fixtures; it never starts Emby.

## Updates and releases

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
