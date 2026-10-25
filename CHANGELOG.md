# Changes

## 1.1.0 - media deletion preview

- Added individual and bulk deletion of watched movies and TV episodes, from both report views.
- Added page selection, selection across pages, exact-path previews, typed confirmation, progress, and cancellation of remaining items.
- TV shows expand to watched episodes only. Deletion respects Emby permissions and playback state, rechecks paths/watch state, and protects library roots and folders containing other library media.
- Blocked plugin installation/restart during media deletion and retained watch-report/CSV behavior.
- Corrected draft release verification to resolve the authenticated draft by its stable release ID. Published 1.0.0 application assets remain unchanged.
- Added deletion tests and an explicit post-publication check using the real published 1.0.0 updater and public release feed.

## 1.0.0 - published preview

- Added watched-title reports by user with grouped TV shows and episode detail.
- Added movie/show/episode lookup and viewer reports across current Emby users.
- Added filtering, pagination, optional in-progress entries, and complete CSV exports.
- Added administrator-only update controls, a background check timer, verified DLL downloads, atomic installation, and one rollback backup.
- Added tests, packaging, GitHub CI/release definitions, and development handoff instructions.

This is the first application baseline, published as a preview on October 25, 2026. Hosted Windows/Linux CI, all six public asset downloads, and their hashes are verified. Live Emby/platform verification remains pending.
