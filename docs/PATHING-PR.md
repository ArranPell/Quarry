# Pathing — clone, read, and PR plan (2026-09-06)

Companion to the Hunter arc in `docs/PLAN.md`. The Pathing work lives in its own clone at
a sibling clone, `..\GW2Pathing` (**not** in this repo). This file stays here because it's planning; move it
if the clone grows its own docs.

## What we learned from the source (read 2026-09-06, HEAD `2d802ad`, manifest 1.13.2)

- **Releases vs versions.** GitHub Releases stop at v0.17.5 (2022). The in-game Blish repository ships
  from `manifest.json` builds, not GitHub tags — HEAD says 1.13.2 (2026-07-29). Dade Lamkins (Freesnow)
  is both Pathing's author (334/389 commits) and the Blish HUD lead, so he publishes straight to the
  module repo. Active: a dozen substantive commits in 2026 (Lua trails, May category-state rework,
  tree-view fixes). MIT licence, PRs welcomed in the README, six outside contributors have landed.
- **Build shape.** Single project, ~17k lines C#, `net48`, old-style csproj + `packages.config`, all
  deps on NuGet: TmfLib 2.2.5 (pack parsing), NeoLua 1.3.19, Gw2Sharp 1.7.3, DLR, CSCore, Flurl,
  Gapotchenko.FX. README says clone TmfLib beside it, but the csproj actually references the NuGet
  package — try a plain restore first. Should build with the same VS 2022 / SDK setup as this module.
- **Achievement hiding, exactly.** `State/AchievementStates.cs` polls `/v2/account/achievements` every
  `INTERVAL_CHECKACHIEVEMENTS = 300010` ms (5 min) via `UpdateCadenceUtil`; the only early refresh is
  `Gw2ApiManager.SubtokenUpdated`. `IsAchievementHidden(id, bit)` = `Done` or bit in
  `AchievementBits`. Markers/trails call it live on every draw (`Behavior/Filter/AchievementFilter
  .IsFiltered()`), so they vanish as soon as a poll succeeds. The category tree does **not**:
  `UI/Controls/NodeTree/PathingCategoryNode.DetectAndBuildContexts()` computes `_achievementHidden`
  and `CheckDisabled` once at node build, so an open tree shows stale greyed-out state until it's
  rebuilt (map change, reopen, pack reload). Gate for all of it: setting
  `PackAllowMarkersToAutomaticallyHide` (default true) + API key with `account` + `progression`.
  The API's own account-endpoint lag (minutes) sits on top of the poll.
- **Public surface we can already use (no PR needed).** `PathingModule.PackInitiator` (public) →
  `PackState.CategoryStates` with `SetInactive(PathingCategory, bool)`, `SetInactive(string namespace,
  bool)` (stores state even if the category isn't loaded on the current map — added for Lua),
  `GetNamespaceInactive(string)`, event `CategoryInactiveChanged`. `PackInitiator` events
  `LoadMapFromEachPackStarted/Finished` (our probe already hooks the latter). `PackInitiator
  .GetAllMarkersCategories()` returns the merged root category. So hunt mode (Phase 16) needs nothing
  from upstream; the reflection-vs-reference question is ours to settle.

## PR 1 — achievement state freshness (small, do first)

Purpose: fix the thing users actually notice (stale greyed-out categories, up-to-10-minute lag), and
find out how upstream responds to a PR before we depend on anything bigger.

Changes, all in-repo, no new deps:

1. `AchievementStates`: add `public Task RefreshAsync()` that resets `_lastAchievementCheck` so the
   next `Update` fires immediately (or calls `UpdateAchievements` directly under the existing lock), and
   `public event EventHandler<EventArgs> AchievementStatesUpdated` raised after a successful load.
2. `PathingCategoryNode`: subscribe to `AchievementStatesUpdated` (unsubscribe in `DisposeControl`);
   on fire, re-run the `_achievementHidden`/`CheckDisabled` part of `DetectAndBuildContexts()` and
   `UpdateActiveState` so the tree greys out without a rebuild. Keep it cheap — one dictionary lookup
   per node with an achievement id.
3. Optional, same PR only if trivial: a "Refresh achievement status" item in the category tree's
   context menu / settings tab that calls `RefreshAsync()`, rate-limited to once per 30 s (the API caches
   anyway, so more often is pointless — say so in the tooltip).

Not in PR 1: shortening the 5-minute interval (API lag makes it moot and Dade will rightly push back),
anything Lua, anything about our module.

Verify before submitting: build; load with `--debug`; complete a small achievement in-game; wait for
the poll — tree greys the node without reopening; click Refresh → log shows one API call, a second
click inside 30 s does nothing; disable/enable Pathing three times — no handler leak (Sentry-style
exceptions are what Dade's June commit was chasing, so this will be looked at).

PR text: two paragraphs — the stale-tree behaviour with the exact code path, and what the change does.
Mention the marker-pack-author angle (Lady Elyssa's pack tags 880+ achievements, so this is visible to
her users). Link the Discord thread if one exists.

## PR 2 — only if PR 1 lands and we hit a wall

Candidates, none needed today: a `TryGetCategoriesForAchievement(int id, int mapId)` helper on
`CategoryStates` (we build this ourselves in Phase 15 from the packs, so only worth upstreaming if our
index and Pathing's live tree ever disagree); an `IPathingApi` interface for other modules if the
manifest-dependency route (Phase 16) turns out unsupported.

## Fork policy

Clone at `..\GW2Pathing` (a sibling of this repo), `origin` = our GitHub fork, `upstream` = `blish-hud/Pathing`,
work on a branch per PR, rebase on `upstream/main` before opening. **No maintained fork as a product**:
a separately-namespaced Pathing loses auto-updates, needs its own markers folder, and breaks every
module (ours included) that finds Pathing by `bh.community.pathing`. The fork exists to build PRs. If
a PR is refused or sits for months, revisit — that's the only case where a real fork earns its keep.

Cowork/device note: the same git-on-the-mount quirk applies (`.git/index.lock` left behind after any
read from Cowork; ArranPell deletes before Claude Code commits). No `dotnet` in the device VM; all builds in
Claude Code on Windows.

## Opening prompt for Claude Code (PR 1, run from the `..\GW2Pathing` clone)

> This folder is a fresh clone of https://github.com/blish-hud/Pathing (upstream) with my fork as
> origin. Read README.md, manifest.json, State/AchievementStates.cs, Behavior/Filter/AchievementFilter.cs
> and UI/Controls/NodeTree/PathingCategoryNode.cs, then read this repo's docs\PATHING-PR.md
> and do **PR 1 only**. First get a clean build of the unmodified tree (`Pathing.sln`, x64; report the
> exact command, the `.bhm` path and whether the TmfLib NuGet restore worked or the README's clone step
> was needed). Then branch `achievement-refresh`, make changes 1 and 2, and 3 only if it's a handful of
> lines. Match the file's existing style (Dade's, not ours). No behaviour change beyond the doc. Build,
> report, update the PR 1 section's status, and stop for my load-test (verify steps in the doc). Don't
> commit until I confirm.

**Status:** not started. Clone not yet made (folder exists, empty).
