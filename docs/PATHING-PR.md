# Pathing — clone, read, and PR plan (2026-09-06)

Written alongside the Hunter arc (Phases 15–17, now in `docs/COMPLETED.md`). The Pathing work lives in its
own sibling clone, `..\GW2Pathing`, not in this repo. This file stays here because it's planning. Move it
if the clone grows its own docs.

## What we learned from the source (read 2026-09-06, HEAD `2d802ad`, manifest 1.13.2)

- **Releases vs versions.** GitHub Releases stop at v0.17.5 (2022). The in-game Blish repository ships
  from `manifest.json` builds, not GitHub tags, and HEAD says 1.13.2 (2026-07-29). Dade Lamkins
  (Freesnow) is both Pathing's author (334/389 commits) and the Blish HUD lead, so he publishes straight
  to the module repo. The project is active, with a dozen substantive commits in 2026 (Lua trails, the May
  category-state rework, tree-view fixes). It's MIT-licensed, the README welcomes PRs, and six outside
  contributors have landed changes.
- **Build shape.** A single project, ~17k lines of C#, `net48`, with an old-style csproj and
  `packages.config`. All dependencies are on NuGet: TmfLib 2.2.5 (pack parsing), NeoLua 1.3.19, Gw2Sharp
  1.7.3, DLR, CSCore, Flurl, Gapotchenko.FX. The README says to clone TmfLib beside it, but the csproj
  references the NuGet package, so try a plain restore first. It should build with the same VS 2022 / SDK
  setup as this module.
- **How achievement hiding works.** `State/AchievementStates.cs` polls `/v2/account/achievements` every
  `INTERVAL_CHECKACHIEVEMENTS = 300010` ms (5 min) via `UpdateCadenceUtil`. The only early refresh is
  `Gw2ApiManager.SubtokenUpdated`. `IsAchievementHidden(id, bit)` is true when the achievement is `Done`
  or the bit is in `AchievementBits`. Markers and trails call it live on every draw
  (`Behavior/Filter/AchievementFilter.IsFiltered()`), so they vanish as soon as a poll succeeds.
  - The category tree doesn't. `UI/Controls/NodeTree/PathingCategoryNode.DetectAndBuildContexts()`
    computes `_achievementHidden` and `CheckDisabled` once, when the node is built. An open tree shows
    stale greyed-out state until it's rebuilt (map change, reopen, pack reload).
  - All of it is gated on the setting `PackAllowMarkersToAutomaticallyHide` (default true) and an API key
    with `account` and `progression`.
  - The API's own account-endpoint lag (minutes) sits on top of the poll.
- **Public surface we can already use, with no PR.** `PathingModule.PackInitiator` (public) leads to
  `PackState.CategoryStates`, which has `SetInactive(PathingCategory, bool)`, `SetInactive(string
  namespace, bool)` (stores state even if the category isn't loaded on the current map; added for Lua),
  `GetNamespaceInactive(string)` and the event `CategoryInactiveChanged`. `PackInitiator` has the events
  `LoadMapFromEachPackStarted/Finished`. The Phase 10 probe hooked the latter, and was deleted in Phase
  16. `PackInitiator.GetAllMarkersCategories()` returns the merged root category. So hunt mode (Phase 16)
  needs nothing from upstream, and the reflection-vs-reference question is ours to settle.

## PR 1 — achievement state freshness (small, do first)

Purpose: fix what users notice (stale greyed-out categories, up to 10 minutes of lag), and find out how
upstream responds to a PR before we depend on anything bigger.

Changes, all in-repo, with no new dependencies:

1. `AchievementStates`: add `public Task RefreshAsync()`, which resets `_lastAchievementCheck` so the
   next `Update` fires immediately (or calls `UpdateAchievements` directly under the existing lock). Add
   `public event EventHandler<EventArgs> AchievementStatesUpdated`, raised after a successful load.
2. `PathingCategoryNode`: subscribe to `AchievementStatesUpdated`, and unsubscribe in `DisposeControl`.
   When it fires, re-run the `_achievementHidden`/`CheckDisabled` part of `DetectAndBuildContexts()` and
   `UpdateActiveState`, so the tree greys out without a rebuild. Keep it cheap: one dictionary lookup per
   node with an achievement id.
3. Optional, in the same PR only if it's trivial: a "Refresh achievement status" item in the category
   tree's context menu or settings tab that calls `RefreshAsync()`, rate-limited to once per 30 s. The API
   caches anyway, so refreshing more often does nothing. Say so in the tooltip.

Not in PR 1: shortening the 5-minute interval (API lag makes it moot, and Dade would be right to push
back), anything Lua, or anything about our module.

Verify before submitting:

- Build, and load with `--debug`.
- Complete a small achievement in-game and wait for the poll. The tree greys the node without reopening.
- Click Refresh. The log shows one API call, and a second click inside 30 s does nothing.
- Disable and enable Pathing three times. There should be no handler leak. Dade's June commit was chasing
  Sentry-style exceptions, so this will be looked at.

PR text: two paragraphs. One describes the stale-tree behaviour with the exact code path, and the other
says what the change does. Mention the marker-pack angle: the most thoroughly tagged pack we index
carries 880+ achievement ids, so a lot of players see this. Link the Discord thread if one exists.

## PR 2 — only if PR 1 lands and we hit a wall

None of these is needed today:

- A `TryGetCategoriesForAchievement(int id, int mapId)` helper on `CategoryStates`. We build this
  ourselves from the packs in Phase 15, so it's only worth upstreaming if our index and Pathing's live
  tree ever disagree.
- An `IPathingApi` interface for other modules, if the manifest-dependency route were ever wanted. Phase
  16 chose reflection through `PathingBridge` instead, and that stands.

## Fork policy

The clone is at `..\GW2Pathing` (a sibling of this repo), with `origin` = our GitHub fork and
`upstream` = `blish-hud/Pathing`. Work on a branch per PR, and rebase on `upstream/main` before opening
it. There's no maintained fork as a product. A separately-namespaced Pathing loses auto-updates, needs
its own markers folder, and breaks every module, ours included, that finds Pathing by
`bh.community.pathing`. The fork exists to build PRs. If a PR is refused or sits for months, revisit
this. That's the only case where a real fork would be worth it.

## Working PR 1 (in the `..\GW2Pathing` clone)

The clone is a fresh copy of https://github.com/blish-hud/Pathing (upstream), with our fork as origin.

1. Read README.md, manifest.json, State/AchievementStates.cs, Behavior/Filter/AchievementFilter.cs and
   UI/Controls/NodeTree/PathingCategoryNode.cs.
2. Get a clean build of the unmodified tree first (`Pathing.sln`, x64). Note the exact command, the
   `.bhm` path, and whether the TmfLib NuGet restore worked or the README's clone step was needed.
3. Branch `achievement-refresh`. Make changes 1 and 2, and 3 only if it's a handful of lines. Match the
   file's existing style (Dade's, not ours). No behaviour change beyond what's described above.
4. Build, update the PR 1 section's status, and load-test with the verify steps above. Don't commit until
   the load-test passes.

Status (2026-09-22): not started. The clone exists at `..\GW2Pathing`, and nothing has been branched or
changed in it yet.
