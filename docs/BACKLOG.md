# Backlog

> **Reading this in the public repo?** `docs/DECISIONS.md` (the decision log) and `docs/COMPLETED.md`
> (the completed-phase archive) are cited throughout. They live in my private working repo, along with
> the other archive-only docs. The citations stay as they are: they point at a record that exists, just
> not here.

An idea dump. Anything goes in here, and nothing in here is scheduled or decided. When an item is picked
up, it becomes a phase in `docs/PLAN.md` and is deleted from this file; don't keep both. When an item is
rejected, it moves to Closed with a one-line reason so we don't argue it again. Keep entries short: what
and why, 1–3 lines, and the date it was added.

The product rule applies to everything here: **bounded sessions, not completeness**.

## 2.0.x — first maintenance release (collecting)

*Opened 2026-09-20, right after 2.0.2 shipped and the in-game listing PR went in. Unlike the rest of this
file, this section is a staging area for one release. The items are intended, just not yet scheduled into
`docs/PLAN.md`. None is urgent enough to justify a release on its own. The first real bug report from the
in-game audience triggers the release, and these ride along. When that happens, items move to PLAN.md
under the same rule as the rest of the file. 2.0.3 (2026-09-21) was an unplanned manifest fix and shipped
without any of these. They're all still open, and the line numbers below were re-checked against the
source that day.*

### Observability: the Sentry pass

Blish HUD reports to Sentry automatically. There's no per-module configuration and nothing to add to the
manifest. Sentry receives unhandled exceptions plus every `Logger.Error` / `Logger.Fatal` call we make, so
our own log levels are the only control we have. Blish's own guidance
(docs/modules/module-citizen/ensuring-stability) says handled environmental failures must not be ERROR,
because they flood the feed with things the author can't fix. Phase 56's Error→Warn audit caught most of
these. The ones below survived, and they only start to matter now that there's an audience bigger than
one.

- **Downgrade three `Logger.Error` calls to `Warn`.** All three are handled environmental failures that
  already notify the user through `Debug.Contingency`. At Error level they're duplicate noise that will
  bury real reports:
  - `AchievementService.cs:353` and `:377`: "Failed to download achievement data and no cached copy."
    Network failure, AV interference or a server hiccup on someone's first run. Already calls
    `NotifyHttpAccessDenied`.
  - `PersistenceService.cs:184`: "Access denied writing `persistanceStorage.json`." Permissions or AV.
    Already paired with the Contingency notification Phase 56 added.

  That leaves 12 `Logger.Error` calls. All are coding errors or packaging failures and should stay. There
  are no `Logger.Fatal` calls, and none should be added.

- **Upgrade one `Warn` to `Error`: the Pathing shape mismatch.** `PathingBridge.LogShapeMismatchOnce`
  (line 173) warns, so it never reaches Sentry. A `CategoryStates` shape mismatch means Pathing changed
  its internals, and Hunt mode is dead for every user running both modules. That's not environmental and
  not user-fixable, and it's the thing worth being told about. It's already rate-limited to once per
  session by `loggedShapeMismatch`, so it can't flood. `PathingBridge.cs:149` ("failed to locate Pathing
  module") is left out on purpose. That one is environmental (Pathing absent or disabled) and stays Warn.

- **Log the data-file version at load.** Nothing logs `version.json`'s `Version`; grep returns zero hits.
  Now that we serve the files ourselves, "which data version is this user on" is the first question on
  any data-shaped bug report. It's also the only way to see adoption when we eventually move off v9. One
  `Info` line in `AchievementService`.

- **Log Quarry's own version at startup.** Our `Logger.Info` lines are all timing and state, and none
  names the module version. Users who arrive through the in-game repository auto-update, so they won't
  know which version produced the log they paste into a report. Check first whether Blish already writes
  module versions into the log at load. If it does, this is a duplicate and should be dropped.

### Verified clean; don't re-check

A catch-block sweep on 2026-09-20 found no silent-failure gaps. Eighteen catch blocks take no logging
action. Fourteen are `OperationCanceledException` during shutdown, which is correct to swallow. The rest
are intended: `PathingBridge` routes through `LogShapeMismatchOnce`; `CurrentMapService:163` accumulates
into `lastException` and warns once after all floors are tried; and `AchievementService.TryDeleteQuietly`
swallows a best-effort temp-file delete, with a comment saying so. No `TEMP`/`HACK`/`FIXME` markers remain
in shipped code either.

## Visual

*Visual items leave this section when they become phases. The ones that shipped (UI arc Phases 18–22,
Batch F, Batch E) are in COMPLETED.md.*

- **Ship our own font once Blish releases TTF loading (2026-09-12).** `ContentsManager.
  GetBitmapFont(ttfPath, size, ranges, lineHeight, textureSize)` exists on Blish's dev branch. It
  rasterises a bundled `.ttf` at any size at runtime. In 1.2.0 and 1.3.0 the method throws
  `NotImplementedException`. When a release carries it, revisit `UiStyle`'s font roles. A face rasterised
  at the effective size (nominal × `UIScaleMultiplier`) would avoid the downscale blur that UI-DESIGN §1.1
  says we can only soften today. Watch the Blish releases page; nothing to do before then.

- **Bearing arrow: the risk is vector precision (2026-09-14, revised from 09-13).** Phase 43's arrow needs
  a player→objective direction, not just a distance. Distance tolerates a sloppy coordinate conversion:
  the number is just a bit off. A bearing snapped to eight directions doesn't. A small error at an octant
  boundary flips the answer. Pack-sourced objectives are native Mumble-space and should be precise.
  Wiki-sourced ones go through `TryContinentToWorld` from continent/map-pixel coordinates, and some are
  `HeightUnknown` (2-D only). Nobody has measured that conversion's accuracy for this purpose. As I recall,
  that's why the arrow didn't get built last time. It was a call made at build time and never run in Blish.
  - Before promising it, measure `TryContinentToWorld` against a few known points, and check whether
    Blish's or GW2's own continent↔world conversion is the canonical one to use. Checking that
    `Gw2Mumble.PlayerCharacter.Forward` exists (it appears to) isn't enough.
  - Snap to eight directions either way. A free-rotating arrow at 14 px reads as jitter.
  - Still unresolved from the earlier framing: `RemainingObjective`/`INearestObjectiveService` only expose
    a distance, never a world position, so there's nothing to compute a bearing from without a service
    change. Batch F ruled that change out, and the Inspector's place line and the Target List's rows
    shipped distance-only. Adding a `Vector3`/`Vector2` position to `RemainingObjective` is the
    prerequisite, however the precision question resolves.

- **Auto-size the Quarry window to its content (deferred out of Phase 42, 2026-09-13).** The brief asked
  for `CardGrid.ContentHeight`-driven sizing, so a short Here list opens without a dead band below it.
  What landed instead: `CanResize`, a persisted size and Fill-sized `CardGrid`s, so drag-resize reflows
  columns. That's useful, but it doesn't cover "no dead band on open". Doing that properly needs a hook for
  "the active tab's View finished building", so the window can measure that tab's `ContentHeight` and
  resize before the frame paints. `TabbedWindow2`'s tabs are lazy factories (`() => new HereView(...)`),
  and nobody traced how to catch a View's `Built` event from outside it in one sitting. Do this once, as
  its own piece of work, rather than bolting it onto an already-large phase.

- **Inspector images: let the window grow to near-native wiki size (2026-09-14).** Load-testing showed that
  the click-to-expand image (Phase 47/48 load-test fixes) can only get as big as the Inspector's fixed
  320 px width allows. `Image`/`ImageSpinner` doesn't stretch past its parent, and the window never
  resizes. For now I've kept the fixed-width, aspect-correct expand that already shipped. The bigger
  version grows the window itself on click, in both directions, reflows chips, notes and buttons to the
  new width, and shrinks back on the next click. That's feature work in the same territory as the resize
  investigation in `COMPLETED.md`, and it deserves its own phase rather than a same-session extension of
  the load-test fix.

- **The flat window body reads as blank, not as GW2 (2026-09-14, Batch E load-test).** Phase 41
  (`docs/UI-DESIGN.md` §4) dropped the stretched `background.png` for a flat `WindowBody` fill, a 1 px
  border, a `fade-down-46` gradient under the title bar, and (on the Quarry window only) the `605025`
  left-side accent. At the time that was what my own BACKLOG note asked for. It wasn't a 9-slice GW2
  texture, because the asset ids were never verified. After living with it through Batch F and Batch E, it
  reads as empty sections rather than a GW2 panel (see the 2026-09-14 Target List/Quarry screenshot). I've
  left it as-is for now, to decide later with a proper look at the options instead of rushing it
  mid-batch. The options: lighten or texture the flat fill without a new asset, or verify Blish's
  window-edge and background texture ids and bring back a textured, possibly 9-sliced, body. Both windows
  already paint through `WindowBodyPainter` (`UserInterface/WindowBodyPainter.cs`), so either direction
  lands in one file.

## Features

*Features leave this section when they become phases. RC2's multi-map work and Batch D (Phases 31–34) are
in COMPLETED.md.*

- **Wiki row names → floor POIs, not just sectors (deferred out of Phase 29, 2026-09-11; measure
  first).** Phase 29 resolves an area-named row against the current map's sectors. The same
  `/v2/continents/:c/floors/:f/regions/:r/maps/:m` fetch also returns `PointsOfInterest`: landmarks and
  vistas with names and coordinates. Matching a row name against those too is nearly free now that Phase
  29 has landed. A landmark is a better target than a sector centroid, because it's a point rather than
  an area. It was kept out of Phase 29 for three reasons. It was never measured. The Explorer cohort Phase
  29 targets is sector-named by definition, so it would gain nothing there. And adding an unmeasured
  second matcher to a tier that has already been reshaped once would make the tier harder to trust. Do the
  measurement first: how many rows that miss on sectors hit exactly on a POI name, and how many of those
  hits are the right place. Then decide. Exact matching only, whatever the answer.

- **Gated / sequential steps, the step-level half (2026-09-09; measured).** The achievement-level half
  shipped in Phase 25. Here excludes `RequiresUnlock` achievements you haven't unlocked and anything whose
  `prerequisites` aren't done, and `IgnoreNearlyComplete` too; the All tab's Nearest-to-done sort demotes
  the last. What's left is per-step gating, which isn't in the API at all. Measured over the 1,112 table
  achievements: there's no `Progression` flag, and `bits` carry no ordering or prerequisite data. The best
  available is a runtime heuristic. If the account's completed bits are always a contiguous prefix, treat
  the achievement as sequential and offer "show next step only". That's confounded, since people do
  collections in order anyway, so make it a per-achievement toggle and let the heuristic only choose the
  default. A lead worth checking first: subpages carry `Preceded by` (1,097) and `Followed by` (1,008)
  keys. Check whether those ever appear on collection item pages, or only on story and event pages.

- **Item-collection achievements and the trading post (2026-09-09; measured).** The useful version is
  narrower than it sounds. Bit types across the table set: Item 5,952, Skin 3,801, Text 3,537, Minipet
  315. So "acquire a thing" is the dominant collection step. But in a sample of 800 unique item bits (605
  resolved), only 26 (4 %) are tradeable on the TP; 86 % are `AccountBound` / `AccountBindOnUse` /
  `NoSell`. So this isn't a "buy vs find vs craft" classifier. It's a shortcut finder over a small set,
  which is still worth having, because each hit saves an errand.
  - It's cheap: one `/v2/commerce/prices?ids=` batch over the item bits we're showing, cached. Show the
    price on the remaining-item line and mark it buyable.
  - Limit one: a price is a warning as often as a shortcut. Sampled hits include Sunrise at ~1,630 g and
    Pendant of Arah at ~434 g, where "buyable" means "no".
  - Limit two: Skin and Minipet bits can't be priced without a skin→item index, which would mean scanning
    the whole item catalogue. Don't claim buyability for those; leave them unmarked.
  - Wallet-aware "can you afford it" needs the `wallet` scope. Skip it unless it's ever worth asking for.

- **Core Tyria: three sources plus a manual one, and an honest gap for the rest (2026-09-09).** What makes
  it tractable is that Here only needs achievement → map. Coordinates are only needed for the Next line.
  The sources, in order:
  1. The pack index, which already covers Dive Master and festival JPs.
  2. A wiki subpage coordinate implies its map for free, by reverse-looking-up the continent coordinate
     against `continent_rect`. So the 3,487 coordinate-bearing pages give map membership as a by-product.
  3. `Zone`/`Area` keys give map membership with no coordinate.
  4. Coverage will still be partial, so the pin-your-own-location item (ROADMAP, RC4) is the fix for the
     tail. The guidance badge makes the gap visible instead of hiding it.

  Not the plan: a hand-maintained core-Tyria map table.

*The ★ personal-marker-pack idea, "pin a location while you play", "route mode" and the
`FileSystemWatcher` auto-reload idea have been tracked privately since 2026-09-14. ROADMAP's RC4 section
summarises the first three. The older "Core Tyria, second attempt" and "personal marker pack" entries that
sat here duplicated the Core Tyria entry above and the marker-pack idea, and were removed 2026-09-21.*

## Performance / robustness

- **Disk-persisted API cache, a follow-up from the Phase 23 session cache** (done; see
  `docs/COMPLETED.md`). Its AP tie-break half shipped in Phase 25: "Nearest to done" breaks ties on points
  from `BitAlignmentService`'s cache. Still open: persisting processed API results to disk, as the Blish
  API guide advises (responses are header-cached in memory, but that's lost on restart), and the 300
  req/min limit Blish shares across modules.

*Contingency hooks and the log-level audit, and retry/backoff on API batch failures, have been tracked
privately since 2026-09-14. The contingency-hooks half shipped in Batch H Phase 56.*

- **Scrape cadence and a lean payload: how data ownership should work (2026-09-09; sizes measured).** Two
  questions, both answerable now. *Phase 48 (2026-09-13) already shipped the lean-payload half, as a
  build-time generator (`DerivedSubpageGenerator`) rather than a hosted pipeline. What's below is the
  still-unscheduled cadence and hosting question for the source files it reads.*
  - How often: tie it to releases, not the calendar. Achievements change with releases (roughly
    quarterly) and festivals, not continuously. A monthly scheduled run plus an on-demand run after each
    release is plenty. The hosted files are `last-modified 2026-04-22`, which is what months of drift looks
    like. Add a `generated` timestamp to `version.json` so staleness is visible instead of guessed at.
  - How: a scheduled GitHub Action in our repo runs `Gw2WikiDownloader` and commits the output to the
    `bhud-static/<namespace>` branch, which SSRD deploys by webhook. There's no server to run. The access
    half is in place as of 2026-09-20: the repo is public, the SSRD account exists, the webhook is
    configured, and `bhud-static/ArranPell.Quarry` is live and serving. So access is no longer the
    blocker. The only blocker is that the scraper can't yet produce what the module verifies against (see
    the next point). An Action that commits to that branch would work today if it had something correct
    to commit.
  - What the plan still has to solve, found 2026-09-13 by reading the scraper (review §4.4). It scrapes
    rendered HTML, not `api.php`. `Program.cs:21-29` requires a `cookies.txt` at startup, unguarded. The
    achievement-data and tables paths are commented out at `Program.cs:117-123`, so a run as committed
    writes only `subPages.json` and reads an existing `achievement_data.json`. And it produces no
    `version.json` and computes no md5s: whatever generates the three hashes `AchievementService` verifies
    against isn't in this repo. None of it is fatal, and none of it is budgeted.

- **Unit tests for the pure bits.** *Phase 20 extracts the progress/remaining-items helper
  (`AchievementProgress`), the first candidate for a test.* `specialSnowflakeCompletedHandling`, the
  bit-order fix for achievements where the wiki and the API disagree, isn't to be changed without a test
  case. That's still why this matters: those test cases would live here.

## Housekeeping

*Batch H (Phases 49–56) took the README, the rename, `Persistence` and the `data/` purge. The release
build and CHANGELOG shipped with 2.0.0. The four NuGet warnings are tracked privately.*

- **Other upstream PRs (optional, 2026-09-08).** The rule: tag an item `upstream-candidate` here when a fix
  is (a) a bug his users hit too, (b) in still-shared code, and (c) one self-contained change. To port one,
  make a fresh branch off `upstream/main` in a separate worktree, re-apply the fix against his layout and
  build it against his project. The test is that `git diff upstream/main -- <file>` is mostly the fix
  itself. Expect relevance to fall off as the UI diverges. The data layer stays shared longest.

## Known quirks / bugs

*Open bugs live on the public tracker: `ArranPell/Quarry#3` (Target List position resets on restart) and
`#4` (partially-tagged-pack suppression). How they got there (filed privately 2026-09-14, refiled publicly
2026-09-20) is in COMPLETED.md. Below are quirks and known issues that aren't filed.*

- **Noted by the Phase 54 review but outside its lens; all core-path and rare (2026-09-14).**
  - `PersistenceService.Reload`, when triggered from `Save()` on the autosave/debounce thread, calls
    `AchievementTrackerService.TrackAchievement`, and so mutates the unsynchronised tracked list
    off-thread.
  - `AchievementService.LoadPlayerAchievements` can overlap between `SubtokenUpdated` and the 5-minute
    poll.
  - `BitAlignmentService.GetAchievementsAsync` logs `OperationCanceledException` as a Warn on unload.
  - `Reload`'s "merged N" count increments even when `TrackAchievement` returns false at the 15-cap, and
    `AchievementTrackerService.Load` bypasses the cap.

  None is user-visible today. Fix the first two if the external-edit reload ever gets real use.

- **The BlishHUD build targets never remove a deleted `ref/` asset from the `.bhm` (found Phase 50,
  2026-09-14).** `BlishHUD.targets` copies `ref\**` into `$(OutDir)ref`, zips the whole output folder,
  then `RemoveDir`s that copy. The folder survives on this machine, though, so the next build copies over
  it without deleting what's gone, and the zip still carries every asset ever built. Five deleted PNGs
  showed up in a fresh `.bhm` until `bin/` and `obj/` were wiped. **After removing anything from `ref/`,
  delete `src/Quarry/bin` before trusting the `.bhm`.** Every Release build should start from a clean tree
  for the same reason.

- **A Blish HUD engine bug (found 2026-09-15, the cold-install crash).** It's filed as a note here, not
  tagged `upstream-candidate`: that tag is for Denrage's module, and this is the Blish HUD engine.
  `Label { WrapText = true, HorizontalAlignment != Left }` overflows the stack every time Blish draws it.
  `SpriteBatchExtensions.DrawStringOnCtrl`'s per-line recursive call (Blish HUD source, commit `8aa65ba1`)
  passes `wrap` and the alignment through unchanged, against a rectangle that never shrinks, so it never
  terminates. Quarry's fix, in `AchievementTrackWindow`'s empty-state label, hard-wraps the text with a
  literal `\n` and sets `WrapText = false`. It's worth reporting against `blish-hud/Blish-HUD` at
  some point, since any module hits it the same way. That's a different repo and maintainer from
  Denrage's, so it doesn't fit the upstream-PR workflow above.

- **The guidance badge hides a wiki tier whenever any bit on the same achievement is pack-tagged (found
  testing Phase 29, 2026-09-11).** `GetGuidance` returns `Tagged` as soon as `remainingTagged > 0` on the
  map, before it asks whether other, unrelated bits are covered by a wiki coordinate or sector match. So an
  achievement that's mostly pack-tagged but has one wiki-only step shows `* Guided`, with no hint that
  anything is wiki-derived. That's working as designed. The badge answers "how well can we tell you what's
  left", and Tagged is the best evidence available for some of what's left. But it made Phase 29 hard to
  check by eye. My best test case (Spiritual Childcare) is also pack-tagged by one of my installed packs,
  so the badge alone couldn't confirm the new tiers fired. The per-bit answer is still visible in the
  Target List's "Next:" line and its tooltip. Those are computed per bit via
  `RemainingOnMap`/`FormatNearestText`, and show the sector-centroid tooltip wording whenever the nearest
  remaining objective is a wiki one, whatever the badge says. Still open: a second, quieter marker saying
  "some remaining steps are wiki-only" even when the headline tier is Tagged. No user has asked for it yet.
  (A related fix, showing the badge in the Target List at all, shipped 2026-09-11.)

- **The pack index reads the shared `markers` folder directly, with no dependency on Pathing's runtime
  state (Phase 15 design, re-surfaced 2026-09-11).** `MarkerPackIndexService` opens every `.taco`/`.zip`
  in `Documents\Guild Wars 2\addons\blishhud\markers\` itself, via TmfLib. It doesn't ask Pathing what's
  loaded or enabled. So turning off a pack's categories in Pathing's UI, or even disabling or uninstalling
  the Pathing module, changes nothing about what Quarry sees. The files are still in the shared folder, so
  the next load (cache-keyed on file name, length and mtime) finds them again. I hit this while trying to
  get a pack-free test for Phase 29. Restarting Blish didn't help, because the packs were only toggled off
  in Pathing, never removed from disk. To exclude a pack from our index, move its file out of that folder
  (disabling isn't enough), then restart Blish. The cache-miss path handles a changed file count correctly
  (confirmed by reading `PacksMatch`). There's no in-module setting to ignore marker packs; add one if this
  need comes up again.

## Risks

- **Wiki data came from Denrage's URLs. The serving half closed 2026-09-20 (2.0.2).** The three files are
  mirrored on our own orphan `bhud-static/ArranPell.Quarry` branch and served from
  `https://bhm.blishhud.com/ArranPell.Quarry/data/`. `AchievementService` points there as of commit
  `cf3bfae`. If Denrage's files were pulled from the package repo, fresh installs would still get data.
  The full record, including the Windows/LF `.gitattributes` hazard that guards it, is ROADMAP
  publish-gate item 6.
  - What remains is staleness, and it's the only part of this risk left. The mirrors are byte-for-byte
    copies of files dated `last-modified: Wed, 22 Apr 2026`, with `version.json` still at v9. That's five
    months of drift so far. We serve the files but can't regenerate them: `Gw2WikiDownloader` as
    committed can't rebuild them (the cadence entry under Performance / robustness says what it can't
    do). Nothing is broken today, and it will stay the same until someone writes the generation half.
  - The vendored-snapshot fallback is still worth having and isn't affected by this change. It covers
    offline and AV-blocked first runs, which our own hosting does nothing for. Still unscheduled.
  - A smaller loose end: `DerivedSubpageGenerator` still reads `subPages.json` (70 MB) from Denrage's
    namespace. It's build-time only and not mirrored on purpose. Archiving a local copy is cheap
    insurance.

  *(Original entry 2026-09-06; updated 2026-09-08, 2026-09-09 and 2026-09-20.)*

## Closed

- **Card grid at one column (2026-09-12, revised 09-13).** Phase 40 sets `MinCardWidth` 380 for two
  columns, and the grid would drop to one column under ~770 px usable. It can't: `AchievementOverviewWindow`
  enforces a 900×680 minimum (`MinWindowSize`). Closed 2026-09-22.
- **Meta-event timers and Wizard's Vault objectives.** The API ties neither to a map, so both would mean
  more hand-maintained tables. Not going on the list.
- **Distance on Here cards: measure the first paint before making it the default (2026-09-13).** The place
  row shipped anyway in Batch F (Phase 40), and four load-test rounds reported no slowness. The
  measurement itself was never taken. Closed 2026-09-21 as overtaken by events. Reopen if anyone reports a
  slow Here paint.

## Explicitly out of scope

Raising the 15-tracked cap (it's already a setting), Pathing-style markers of our own, anything WvW/PvP,
and regenerating wiki data as a user-facing feature. Keeping our own hosted files fresh is a different
thing, and is in scope: see the scrape-cadence entry under Performance / robustness and the Risks section.
"Hosting our own data files" left this list on 2026-09-20. It was done, and cost far less than this line
assumed (ROADMAP gate item 6).
