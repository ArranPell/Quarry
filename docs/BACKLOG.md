# Backlog

> **Reading this in the public repo?** `docs/DECISIONS.md` (the decision log) and `docs/COMPLETED.md`
> (the completed-phase archive), cited throughout, live in the author's private working repo along with
> the other archive-only docs. The citations are left as-is: they point at a record that exists, just not
> here.

Idea dump. Anything goes in here; nothing in here is scheduled or decided. When an item is picked up it
becomes a phase in `docs/PLAN.md` and gets deleted from this file (don't keep both). When an item is
rejected it moves to **Closed** with a one-line reason so we don't re-litigate it. Keep entries short —
what and why, 1–3 lines. Tag who suggested it and when if it isn't obvious.

Product rule still applies to everything here: **bounded sessions, not completeness** (see CLAUDE.md).

## Visual

*All six visual items (progress fill, near-done highlight, richer toast, compact Here window, LW
category captions, Track window sort + fill) scheduled 2026-09-06 as PLAN.md Phases 18–22 (UI arc) and
removed here per the rule above. The category-grid width item became Phase 33d on 2026-09-11. The 22C
Here strip **became Phase 35** and the compact-view toggles were **retired** when Batch E was written up
in full on 2026-09-14 (DECISIONS that date) — both removed here per the rule. The UI refresh item (ArranPell, 2026-09-09) became
**Batch F Phases 39 and 41** on 2026-09-12 — brief in `docs/UI-DESIGN.md` — and left here per the rule.
Add new ones below.*

- **Ship our own font once Blish releases TTF loading (Wren, 2026-09-12).** `ContentsManager.
  GetBitmapFont(ttfPath, size, ranges, lineHeight, textureSize)` exists on Blish's dev branch (rasterises
  a bundled `.ttf` at any size at runtime); in 1.2.0 and 1.3.0 the method throws `NotImplementedException`.
  When a release carries it, revisit `UiStyle`'s font roles: a face rasterised at the *effective* size
  (nominal × `UIScaleMultiplier`) would sidestep the downscale blur that UI-DESIGN §1.1 says we can only
  mitigate today. Watch the Blish releases page; not before.

- **Distance on Here cards — measure before it's the default (Wren, 2026-09-13).** Phase 40's place row
  costs one `INearestObjectiveService` lookup per candidate (ten, cached). Log the first paint of Here
  on a 10-card map at the load-test; if it's visibly slower than today, make the place row fill in
  after the first paint rather than gating it.

- **Bearing arrow: the risk is vector precision, not vector existence (ArranPell/Wren, 2026-09-14, revised
  from 09-13).** Phase 43's arrow needs a player→objective *direction*, not just a distance. Distance
  tolerates a sloppy coordinate conversion gracefully — the number is just a bit off; a bearing snapped
  to eight directions doesn't — a marginal error right at an octant boundary flips the answer outright.
  Pack-sourced objectives are native Mumble-space (should be precise); wiki-sourced ones go through
  `TryContinentToWorld` from continent/map-pixel coordinates, and some are `HeightUnknown` (2-D only) —
  that conversion's accuracy for this purpose has never been measured. ArranPell recalls this is why the
  arrow didn't get built last time: a build-time call, not a tested one — it was never actually run in
  Blish. Before promising it: measure `TryContinentToWorld` against a few known points (see whether
  Blish/GW2's own continent↔world conversion is the canonical one to use), not just whether
  `Gw2Mumble.PlayerCharacter.Forward` (which does appear to exist) is present. Snap to eight directions
  regardless — a free-rotating arrow at 14 px reads as jitter. Also unresolved from the earlier framing:
  `RemainingObjective`/`INearestObjectiveService` still only expose a distance, never a world position,
  so there's nothing to compute a bearing *from* without a service change (Batch F ruled that out; the
  Inspector's place line and the Target List's rows shipped distance-only). Adding a `Vector3`/`Vector2`
  position to `RemainingObjective` is still the prerequisite regardless of how the precision question
  above resolves.

- **Card grid at one column (Wren, 2026-09-12, revised 09-13).** Phase 40 sets `MinCardWidth` 380 for
  two columns. If Phase 42's resize ever lets the window under ~770 px usable, the grid drops to one
  column — check the card still reads at that width before allowing it.

- **Auto-size the Quarry window to its content (deferred out of Phase 42, 2026-09-13).** The brief asked
  for `CardGrid.ContentHeight`-driven sizing so a short Here list opens without a dead band below it.
  Landed instead: `CanResize` plus a persisted size and Fill-sized `CardGrid`s (so drag-resize, if it
  works, reflows columns) — real value, but not the "no dead band on open" case. Doing that properly
  needs a hook for "the active tab's View finished building" (`TabbedWindow2`'s tabs are lazy factories,
  `() => new HereView(...)`, and nothing traced how to catch a View's `Built` event from outside it with
  confidence in one sitting) so the window can measure that tab's `ContentHeight` and resize before the
  frame paints. Do this once, deliberately, rather than bolt it onto an already-large phase.

- **Inspector images: let the window itself grow to near-native wiki size (ArranPell, 2026-09-14).** Load-test
  found the click-to-expand image (Phase 47/48 load-test fixes) can only get as big as the Inspector's
  fixed 320 px width allows, since `Image`/`ImageSpinner` doesn't stretch past its parent and the window
  itself never resizes. ArranPell's call for now: keep the current fixed-width, aspect-correct expand (already
  shipped) rather than build this. The bigger version — click grows the window itself (both directions,
  not just the image), reflows chips/notes/buttons to the new width, shrinks back on the next click — is
  real feature work in the same territory as the resize investigation in `COMPLETED.md`, worth its own
  phase rather than a same-session extension of the load-test fix.

- **The flat window body reads as blank, not as GW2 (ArranPell, 2026-09-14, Batch E load-test).** Phase 41
  (`docs/UI-DESIGN.md` §4) deliberately dropped the stretched `background.png` for a flat `WindowBody`
  fill plus a 1 px border, a `fade-down-46` gradient under the title bar, and (on the Quarry window only)
  the `605025` left-side accent — explicitly *"what ArranPell's own BACKLOG note asked for"* at the time, and
  explicitly *not* a 9-slice GW2 texture because the real asset ids were never verified. Living with it
  across Batch F and Batch E, it now reads as empty/blank sections rather than a GW2 panel — see the
  2026-09-14 Target List/Quarry screenshot. ArranPell's call today: leave it as-is for now, decide later with a
  real look at options rather than rushed mid-batch — either lighten/texture the flat fill without a new
  asset, or verify real Blish window-edge/background texture ids and bring back a textured (possibly
  9-sliced) body. `WindowBodyPainter` (`UserInterface/WindowBodyPainter.cs`) is the one shared place both
  windows already paint through, so whichever direction it takes lands in one file.

## Features

*Scheduled 2026-09-11 into RC3 Batch D and removed per the rule: hide/snooze (Phase 31), Track top N
(Phase 32), Here cap setting + account-wide nearly-done + session summary list (Phase 33), roaming
achievements (Phase 34). See PLAN.md.*

- **Multi-map achievements.** *Scheduled → Phase 15 (Hunter arc) 2026-09-06: marker packs supply bit→map via POI `MapID`s, so the wiki-table data spike below is no longer needed.* Some achievements (collections, "X across Tyria" types) have sub-objectives
  spread over several maps, so no single category-to-map link fits them and the Here view misses them.
  Two shapes: (a) Here view includes an achievement when at least one of its *remaining* bits is on this
  map, with the bit names in the tooltip; (b) Tracked window highlights, per tracked achievement, which of
  its remaining bits are on the current map. Both need **bit → map** data the API doesn't have (bits carry
  no location; Phase 7 confirmed `CollectionDescriptionEntry` has no location field either). Only lead is
  `achievement_tables.json`'s free-form per-achievement columns. Do a data spike first: pick ~20 known
  multi-map collections and check whether the wiki table rows yield a map name reliably. If under ~70 %,
  shelve like core Tyria. *(ArranPell, 2026-09-06)*
- **Wiki row names → floor POIs, not just sectors (deferred out of Phase 29, 2026-09-11; measure first).**
  Phase 29 resolves an area-named row against the current map's **sectors**. The same
  `/v2/continents/:c/floors/:f/regions/:r/maps/:m` fetch also returns `PointsOfInterest` — landmarks and
  vistas with names and coords — so matching a row name against those too is nearly free once Phase 29
  lands, and a landmark is a *better* target than a sector centroid because it's a point rather than an
  area. Kept out of Phase 29 deliberately: it was never measured, the Explorer cohort Phase 29 targets is
  sector-named by definition so it would gain nothing there, and bolting an unmeasured second matcher onto
  a tier that has already been reshaped once is how this stops being trustworthy. **Do the measurement
  first** — how many rows that miss on sectors hit exactly on a POI name, and how many of those hits are
  the right place — then decide. Exact matching only, whatever the answer.
- **Gated / sequential steps — what the API can and can't tell us (ArranPell, 2026-09-09; measured).** Some
  achievements only let you do step N after N-1, so showing later steps is noise. **Per-step gating is not
  in the API at all** — measured over the 1,112 table achievements: no `Progression` flag anywhere, and
  `bits` carry no ordering or prerequisite data. What *does* exist, and is worth using:
  - **Achievement-level gating is real:** 137/1,112 carry `prerequisites` (other achievement ids), and 244
    are `RequiresUnlock`. Cheap win: an achievement whose prerequisites aren't done shouldn't be a Here
    candidate or a track suggestion — today we'd happily suggest something you can't start.
  - **`IgnoreNearlyComplete` on 424 of them** is ANet's own "don't show this as nearly done" hint, and our
    Nearest-to-done sort ignores it. Respecting it is a one-line honesty fix.
  - **Step-level, best available:** a runtime heuristic — if the account's completed bits are always a
    contiguous prefix, treat it as sequential and offer "show next step only". Confounded (people do
    collections in order anyway), so make it a per-achievement toggle with the heuristic only choosing the
    default. Lead worth checking first: subpages carry `Preceded by` (1,097) / `Followed by` (1,008) keys —
    verify whether those ever appear on *collection item* pages or only on story/event pages.

- **Item-collection achievements and the trading post (ArranPell, 2026-09-09; measured — the useful version is
  narrower than it sounds).** Bit types across the table set: **Item 5,952, Skin 3,801, Text 3,537,
  Minipet 315**, so "acquire a thing" really is the dominant collection step. But sampling 800 unique item
  bits (605 resolved): **only 26 (4 %) are tradeable on the TP**; 86 % are `AccountBound` /
  `AccountBindOnUse` / `NoSell`. So this is not a "buy vs find vs craft" classifier — it's a **shortcut
  finder** over a small set, which is still worth having because each hit saves a real errand.
  Cheap: one `/v2/commerce/prices?ids=` batch over the item bits we're showing, cached. Show the price on
  the remaining-item line and mark it buyable. Two honest limits: prices double as a *warning* as often as
  a shortcut (sampled hits include Sunrise at ~1,630 g and Pendant of Arah at ~434 g — "buyable" there
  means "no"), and **Skin/Minipet bits can't be priced** without a skin→item index, which would mean
  scanning the whole item catalogue — don't claim buyability for those, leave them unmarked. Wallet-aware
  "can you afford it" needs the `wallet` scope; skip unless it's ever worth asking for.

- **Core Tyria — the answer is now three sources plus a manual one, and honest silence for the rest
  (ArranPell, 2026-09-09).** Reframing that makes it tractable: **Here only needs achievement → *map***, not
  coordinates; coordinates are only needed for the Next line. Sources, in order: (1) the pack index
  (already covers Dive Master, festival JPs); (2) **a wiki subpage coordinate implies its map for free** —
  reverse-look-up the continent coord against `continent_rect` — so the 3,487 coord-bearing pages give map
  membership as a by-product; (3) `Zone`/`Area` keys give map membership with no coordinate. Coverage will
  still be partial, so (4) the pin-your-own-location item below is the real fix for the tail, and the
  guidance badge is what makes the gap honest instead of invisible. Explicitly *not* the plan: a
  hand-maintained core-Tyria map table.

*The standout "generate a personal marker pack" idea, "pin a location while you play", "route mode", and
the `FileSystemWatcher` auto-reload idea moved to GitHub Issues #6, #7, #8, #12 on 2026-09-14 and are
removed here per the rule above.*

- **Core Tyria, second attempt.** *Partially scheduled → Phase 15 (Hunter arc) 2026-09-06: pack-covered core achievements (Dive Master, festival JPs, ~100) get Here coverage from the pack index; the wiki-text spike below stays for the rest.* Data spike in the parent project, not module work: can wiki text map
  ~70 % of core achievements (Explorer areas, JP names) to a map? If yes, a best-effort Here list with a
  "wiki-derived, may be wrong" note beats "unsupported". Until then it stays out. *(pre-1.0 backlog)*
- **Personal marker pack generated from the tracked set.** A parent-project PowerShell idea, needing no
  Pathing coupling from this module — Phases 16–17 (hunt mode, nearest objective) cover the in-module
  side of "Pathing integration" and are done; this is the one piece of the old broader "Pathing" idea
  that's still open. Overlaps with GitHub Issue #6 (the ★ idea above) — same destination, this entry is
  the earlier parent-project framing of it. Upstream PR plan (unrelated, ours to send eventually) is in
  `docs/PATHING-PR.md`. *(pre-1.0 backlog)*

## Performance / robustness

- **AP tie-break and disk-persisted API cache — follow-ups from the Phase 23 session cache** (which is
  done — see `docs/COMPLETED.md`). `AchievementItemOverview`'s "Nearest to done" sort still has no tier
  data for its AP tie-break; could now reuse `BitAlignmentService.GetAchievementsAsync`'s dictionary
  cheaply. Also still open: persisting processed API results to disk (the Blish API guide's advice,
  since responses are already header-cached in memory but that's lost on restart) and the 300 req/min
  shared network limit, which matters once Phase 15's pack ingest fans out.
*Contingency hooks + log-level audit and retry/backoff on API batch failures moved to GitHub Issues #13
and #9 on 2026-09-14 and are removed here per the rule above.*

- **Scrape cadence and a lean payload — how data ownership should actually work (ArranPell, 2026-09-09;
  sizes measured).** Two questions, both answerable now. *Phase 48 (2026-09-13) already shipped the
  lean-payload half as a build-time generator (`DerivedSubpageGenerator`) rather than a hosted pipeline —
  what's below is the still-unscheduled cadence/hosting question for the source files it reads from.*
  *How often:* tie it to releases, not the calendar — achievements change on quarterly-ish releases and
  festivals, not continuously. A **monthly scheduled run plus an on-demand run after each release** is
  ample; the hosted files are `last-modified 2026-04-22`, which is what months of drift looks like.
  Add a `generated` timestamp to `version.json` so staleness is visible instead of inferred.
  *How:* a scheduled GitHub Action in our repo runs `Gw2WikiDownloader` and commits the output to the
  `bhud-static/<namespace>` branch, which SSRD auto-deploys by webhook — no server to run. **The access
  half of this is now in place (2026-09-20):** the repo is public, the SSRD account exists, the webhook is
  configured and `bhud-static/ArranPell.Quarry` is live and serving. So the blocker is no longer access —
  it is purely that the scraper cannot yet produce what the module verifies against, below. An Action that
  commits to that branch would work today if it had something correct to commit.
  **What that plan still has to solve, found 2026-09-13 by reading the scraper** (review §4.4): it scrapes
  rendered HTML, not `api.php`; `Program.cs:21-29` requires a `cookies.txt` at startup, unguarded; the
  achievement-data and tables paths are commented out at `Program.cs:117-123`, so a run as committed
  writes only `subPages.json` and *reads* an existing `achievement_data.json`; and it **produces no
  `version.json` and computes no md5s** — whatever generates the three hashes `AchievementService`
  verifies against is not in this repo. None of it is fatal; all of it is unbudgeted.

- **Unit tests for the pure bits.** *Phase 20 extracts the progress/remaining-items helper
  (`AchievementProgress`) — first candidate for a test.* Moved to GitHub Issue #10 on 2026-09-14 and
  removed here per the rule above. The bit-order (`specialSnowflakeCompletedHandling`) rule in CLAUDE.md
  ("not without a test case") is still the reason this matters — that's where those test cases would live.

## Housekeeping

**Scheduled 2026-09-14 into Batch H (PLAN.md Phases 49–56) and deleted from here, per this file's own
rule:** the README + credit line, the module name, `Persistance` → `Persistence`, and dropping the repo
`data/` folder. The four NuGet warnings moved to **GitHub Issue #11** instead, on the same day — a later
routing decision than the Batch H one, so it supersedes it for that one item; PLAN.md's Batch H phase
text may still mention it and is worth a pass to point at #11 instead. What remains below is still
unscheduled.

- **Release build + changelog.** Ship `Release|x64` for the installed copy; add `docs/CHANGELOG.md`
  keyed by manifest version. *(Wren, 2026-09-06)*
- **Other upstream PRs — optional.** Rule in CLAUDE.md: tag `upstream-candidate` here
  when a fix is (a) a bug his users hit too, (b) in still-shared code, (c) one self-contained change.
  Porting = fresh branch off `upstream/main` in a separate worktree, re-applied against his layout,
  built against his project; the test is `git diff upstream/main -- <file>` being mostly the fix
  itself. Expect
  relevance to fall off as the UI diverges; the data layer stays shared longest. *(Wren, 2026-09-08)*
- **Data provenance note.** ~~The hosted scrape is still being regenerated (v9 covers JW + VoE), so the
  pipeline is alive~~ — **retracted 2026-09-13.** Re-measured against the live hosted files: same v9,
  same three md5s, same `last-modified 2026-04-22` as in April. The pipeline is frozen, not slow. The
  scraper is in-tree but is upstream code under the same no-licence status as the rest. Relevant to the
  LICENSE resolution (`docs/COMPLETED.md`) and to any own-data-source discussion.
  *(Wren, 2026-09-08; corrected 2026-09-13)*

## Known quirks / bugs

- **Noted by the Phase 54 review but out of its lens, all core-path and rare (2026-09-14):** `PersistenceService.Reload`, when triggered from `Save()` on the autosave/debounce thread, calls `AchievementTrackerService.TrackAchievement` and so mutates the unsynchronised tracked list off-thread; `AchievementService.LoadPlayerAchievements` can overlap between `SubtokenUpdated` and the 5-minute poll; `BitAlignmentService.GetAchievementsAsync` logs `OperationCanceledException` as a Warn on unload; `Reload`'s "merged N" count increments even when `TrackAchievement` returns false at the 15-cap, and `AchievementTrackerService.Load` bypasses the cap. None is user-visible today; fix the first two if the external-edit reload ever gets real use.

- **The BlishHUD build targets never remove a deleted `ref/` asset from the `.bhm` (found Phase 50,
  2026-09-14).** `BlishHUD.targets` copies `ref\**` into `$(OutDir)ref`, zips the *whole* output folder,
  then `RemoveDir`s that copy — but the folder survives on this machine, so the next build re-copies over
  it without deleting what's gone and the zip still carries every asset ever built. Five deleted PNGs
  showed up in a fresh `.bhm` until `bin/` and `obj/` were wiped. Rule: after removing anything from
  `ref/`, delete `src/AchievementTrackerPlus/bin` before trusting the `.bhm` — and the Release build at
  the publish gate should start from a clean tree for the same reason.

- **Blish HUD engine bug, not ours to fix here — filed as its own note, not tagged `upstream-candidate`
  (that tag is Denrage's module specifically; this is the Blish HUD engine itself) (found 2026-09-15,
  the cold-install crash).** `Label { WrapText = true, HorizontalAlignment != Left }` is an unconditional
  stack overflow the moment Blish draws it — `SpriteBatchExtensions.DrawStringOnCtrl`'s per-line
  recursive call (Blish HUD source, commit `8aa65ba1`) passes `wrap` and the alignment through unchanged
  against a rectangle that never shrinks, so it never terminates. See the CLAUDE.md coding-rules entry
  for the full mechanism and Quarry's fix (`AchievementTrackWindow`'s empty-state label). Worth a report
  against `blish-hud/Blish-HUD` at some point — any module hits this the same way — but that's a
  different repo/maintainer than Denrage's, so it doesn't fit the existing upstream-PR workflow above.

*The overlay icon/tab desync, the Track window horizontal-resize bug, the Track window restart-position
bug, and the partially-tagged-pack suppression bug moved to GitHub Issues #2–#5 on 2026-09-14 (public
repo — first batch of issues filed to start using GitHub's tracker before going public) and are removed
here per the rule above.* **Flag for ArranPell: Issue #3 ("Track window's CanResize doesn't reflow content
horizontally") was filed from a stale base — it predates Batch F, and Phase 42's drag-resize (built on
`CardGrid`/`Fill`-sizing) is the thing that made resize actually work; ArranPell confirmed it in load-testing
("the Quarry window drag resize works perfectly"). Worth closing #3 as already fixed rather than leaving
a stale bug open in a soon-to-be-public tracker — the other three (#2, #4, #5) look genuinely still open.*

`upstream-candidate: bit alignment never ran for the restored tracked set` and `Track window CanResize
doesn't reflow its children` are both already resolved and archived in `docs/COMPLETED.md` — dropped
from here rather than reintroduced by this merge.

- **Guidance badge hides a wiki tier whenever any bit on the same achievement is pack-tagged (found
  testing Phase 29, 2026-09-11).** `GetGuidance` returns `Tagged` the moment `remainingTagged > 0` on the
  map, before it ever asks whether *other*, unrelated bits are covered by a wiki coordinate or sector
  match — so an achievement that's mostly pack-tagged but has one wiki-only step shows `* Guided`, not a
  hint that anything's wiki-derived. Working as designed (the badge answers "how well can we tell you
  what's left", and Tagged genuinely is the best evidence available for *some* of what's left), but it
  made Phase 29 hard to eyeball: ArranPell's best test case (Spiritual Childcare) is also pack-tagged by one of
  his installed packs, so the badge alone couldn't confirm the new tiers fired. **The actual per-bit
  answer is still visible** in the Track panel's "Next:" line and its tooltip — those are computed
  per-bit via `RemainingOnMap`/`FormatNearestText` and will show the sector-centroid tooltip wording when
  the *nearest* remaining objective happens to be a wiki one, regardless of the badge. **Still open:** a
  second, quieter marker noting "some remaining steps are wiki-only" even when the headline tier is
  Tagged — no user ask yet, separate from the visibility fix below.
- **The guidance badge was Here-card-only; the Track window never showed it at all (found same session,
  fixed 2026-09-11 follow-up).** Compounded the item above: with the badge invisible outside Here, and
  Here unable to discover a pure-Area-tier achievement on a core-Tyria map with no pack coverage (the
  `NoCategoryForMap` case), there was no screen anywhere that could show `GuidanceTier.Area` firing.
  Fixed by adding the same badge (label, colour, tooltip) to the Track window's full panel, above the
  Next: line — visible for anything tracked regardless of what Here can discover. Left as-is: the
  Tagged-masking behaviour above still applies to this badge too, since it's the same `GetGuidance` call.
- **The pack index reads the shared `markers` folder directly, with no dependency on Pathing's runtime
  state (Phase 15 design, re-surfaced 2026-09-11).** `MarkerPackIndexService` opens every `.taco`/`.zip`
  in `Documents\Guild Wars 2\addons\blishhud\markers\` itself via TmfLib — it doesn't ask Pathing what's
  loaded or enabled. So disabling a pack's categories in Pathing's own UI, or even disabling/uninstalling
  the Pathing module, changes nothing about what our module sees: the files are still in that shared
  folder, so the next load (cache-keyed on file name/length/mtime) finds them again. ArranPell hit this trying
  to get a pack-free test for Phase 29 — restarting Blish didn't help because the packs were never
  removed from disk, only toggled off in Pathing. **To actually exclude a pack from our index**, move
  (don't just disable) its file out of that folder, then restart Blish — the cache-miss path handles a
  changed file count correctly (confirmed by reading `PacksMatch`). There's no in-module setting to
  ignore marker packs; add one if this need recurs.
## Risks

- ~~**Wiki data comes from Denrage's URLs.**~~ **Serving half closed 2026-09-20 (2.0.2).** The three
  files are now mirrored on our own orphan `bhud-static/ArranPell.Quarry` branch and served from
  `https://bhm.blishhud.com/ArranPell.Quarry/data/`; `AchievementService` points there as of commit
  `cf3bfae`. Upstream being pulled from the package repo no longer starves fresh installs. Full record,
  including the Windows/LF `.gitattributes` hazard that guards it, is ROADMAP publish-gate item 6.
  **What remains is the staleness half, and it is now the whole risk.** The mirrors are byte-for-byte
  copies of files dated `last-modified: Wed, 22 Apr 2026`, `version.json` still v9 — five months of drift
  and counting. We control serving, not generating: `Gw2WikiDownloader` as committed cannot rebuild these
  (see the cadence entry in Features for exactly what it can't do). Nothing is broken today; it stays
  broken-in-the-same-way until someone writes the generation half.
  The **vendored-snapshot fallback** is still worth having and is unaffected by this change — it covers
  offline and AV-blocked first runs, which our own hosting does nothing for. Still unscheduled.
  A smaller loose end: `DerivedSubpageGenerator` still reads `subPages.json` (70 MB) from Denrage's
  namespace. It is build-time only and deliberately not mirrored; archiving a local copy is the cheap
  insurance. *(original entry Wren, 2026-09-06; updates 2026-09-08, 2026-09-09, 2026-09-20)*

## Closed

- **Meta-event timers, Wizard's Vault objectives** — the API ties neither to a map, so both mean more
  hand-maintained tables. Not going on the list.

## Explicitly out of scope

Raising the 15-tracked cap (it's already a setting), Pathing-style markers of our own, anything WvW/PvP,
regenerating wiki data. (“Hosting our own data files” left this list on 2026-09-20 — it was done, and
cost far less than this line assumed: ROADMAP gate item 6.)
