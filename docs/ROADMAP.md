# Roadmap

> **Reading this in the public repo?** `docs/DECISIONS.md` (the decision log) and `docs/COMPLETED.md`
> (the completed-phase archive) are cited throughout. They live in my private working repo, along with
> the other archive-only docs. The citations stay as they are: they point at a record that exists, just
> not here.

Written 2026-09-09 after Denrage settled the licence (issue #7), and reconciled that evening once RC1
shipped and the docs were re-split. Reconciled again 2026-09-11 against PLAN.md after the hunter-code
review ran and Batch C shipped, because this file had drifted a full milestone behind. Corrected the same
day once Batch C's load-test came back confirmed, and again once Phase 29 (RC2's last piece) shipped and
was confirmed. Reconciled again 2026-09-21, after 2.0.3, when the "Where we are" history below was cut to
a pointer. Tidied 2026-09-22 (stale tenses and pointers only), and edited for voice the same day.

This file says what the milestones are and what goes in each. The docs around it:

- `docs/PLAN.md`: current status. The releases so far, what's next, and any phase that's active or
  not started.
- `docs/COMPLETED.md`: the historical record, with every finished phase verbatim, including load-test
  results. Search it by phase number.
- `docs/BACKLOG.md`: unscheduled ideas. An idea becomes a PLAN.md phase when it's picked up, and leaves
  the backlog. This roadmap only groups backlog items into milestones; it doesn't schedule them.
- `docs/DECISIONS.md`: append-only, why we chose what we chose.

An RC is a milestone for me, not a release. Each one ends with a load-test batch and a stretch of playing
with it. Going public is a separate gate (below), taken when an RC feels worth showing someone rather than
on a date. The product rule is unchanged: bounded sessions, not completeness.

## Where we are

```
Upstream PR ✅  →  RC1 ✅  →  RC2 ✅  →  RC3 ✅ (Batches D, F, G, E)  →  Batch H ✅  →  publish gate ✅ (2.0.0)
  →  2.0.1 ✅  →  2.0.2 ✅ (own hosting)  →  2.0.3 ✅ (in-game repository)  →  2.0.4/2.0.5 ✅ (Quarry#5)
  ← HERE: 2.0.x maintenance  →  2.1 (fresh data and polish, planned)  →  2.2 (was RC4, candidate)
```

- Every milestone through the publish gate is done and confirmed in-game. PLAN.md lists the releases
  since. The public repo is `ArranPell/Quarry`, and Quarry installs from Blish's in-game module repository
  as of 2.0.3.
- Upstream PR #8 (Phase 23 bit alignment) is still open, waiting for Denrage's review:
  https://github.com/Denrage/AchievementTrackerModule/pull/8. That keeps the promise from issue #7.
- Next: 2.0.x keeps taking bug fixes (the open public bugs `ArranPell/Quarry#3` and `#4`, and what's
  left at the top of BACKLOG.md). The longer-running work is 2.1 below, settled 2026-09-24. RC4 is now
  2.2 and is still a candidate, not a commitment.

What each batch built, and its load-test rounds, is in COMPLETED.md under the batch name. The reasoning is
in DECISIONS.md under its date. The milestone sections below say what each RC contained.

## RC1 — The hunter exists ✅ (Phases 15, 16, 17 — all in COMPLETED.md)

The module stopped being a tracker with a Here tab. What landed:

- **Phase 15, pack ingest.** `IMarkerPackIndexService`/`MarkerPackIndexService` on TmfLib 2.2.5, option 1
  of the plan's parser decision. Two TmfLib API deviations were found and worked around before coding
  (DECISIONS 2026-09-09). The cache is `markerPackIndex.json`, schema v2. The Here consumer shipped with
  it: it takes the union of the category and index sources, shows a `Guided` label on the card, and
  reports `NoCategoryForMap` only when both sources are empty.
- **Phase 16, hunt mode.** `PathingBridge` works by reflection, on three members. The manifest-dependency
  and compile-time-reference options were both rejected (DECISIONS 2026-09-09). `HuntService` follows the
  rule "only revert what we flipped, and only if nothing else needs it". The phase added the settings
  `HuntMode`, `AutoUntrackCompleted` and `HuntRevertOnUnload`, the completion toast and the Here-card peek
  button. The Phase 10 Pathing probe is deleted. It also absorbed the backlog's auto-untrack item.
- **Phase 17, nearest objective.** `NearestObjectiveService`, and `ObjectiveGeometry` for the `(x, z, y)`
  transpose. Pack coordinates are Mumble-space metres, so no distance conversion is needed. Full Track
  panels show "Next: <name> · N m" with waypoint copy, on a 2 s accumulator gated on `Visible`.
- **Post-batch play fixes (2026-09-09).** Both came from hunting. First, bit-tagged objectives now beat
  untagged ones on the same achievement and map. This was the Auric Basin masks problem: an untagged
  breadcrumb trail kept winning "nearest" and sent the player to cleared ground. The same fix made
  `NearestWaypoint` check completion state, which it never had. Second, remaining-objective names get
  row-number prefixes, so similar-sounding steps can be told apart. Both are in DECISIONS.

## RC2 — Here covers multi-map achievements ✅ (done, confirmed 2026-09-11)

The original pain: achievements whose sub-objectives span several maps never appeared in Here, because
Here worked from a single category→map link. Phase 15's index fixed that for every pack-tagged
achievement. Phase 17 delivered the per-map presentation that was RC2's polish item: "Next" for routes
here, "Not on this map" for routes elsewhere. RC2 then grew content of its own: the guidance tiers, the
threshold and the wiki-location hint. They shipped as Phases 26, 27, 28 and, on 2026-09-11, 29, RC2's
last piece.

- **Measurement, no code: what still doesn't show?** RC1 has been played with. The misses to log are
  achievements I hunt that no pack covers. So far, play has turned up pack-data quality issues (tagged vs
  untagged) rather than coverage gaps. That's weak evidence that coverage is adequate. Misses go in
  BACKLOG.md as they come up; they don't open a phase.
- **The wiki-derived spike answered yes, and shipped.** The source is `subPages.json`, not
  `achievement_tables.json`. The first measurement used the wrong file and concluded no. Tier 1, exact
  `InteractiveMap.Coordinates`, shipped as Phase 28. The coverage numbers once quoted here (5.6 % of rows,
  58 achievements ≥50 %, 48 at 100 %) were superseded when Phase 28 re-measured on the join the module
  performs (`achievement_data.json` EntryList rows). On that join, 379 of 20,914 rows (1.8 %) carry a
  coordinate, 31 achievements have one on every row, and 66 have one on at least half. The shape is the
  same: bimodal, hunt-shaped, and opportunistic rather than a general fallback. Only the headline number
  is smaller. Use COMPLETED.md Phase 28's figures, not the old ones.
- **Tier 2 shipped and was confirmed as Phase 29 (2026-09-11).** Area-named rows resolve against the
  map's sectors: `/v2/continents/:c/floors/:f/regions/:r/maps/:m` returns each sector's `name` and
  `coord`. That gives these rows coordinates and a countdown, so they reach tier-1 quality instead of the
  no-distance tier this roadmap first imagined. It covers the Explorer achievements, which nothing we'd
  shipped before had touched. Confirmed live on my account data: `Zone Defense` (Caledon Forest) for the
  `Area` fallback, and `Spiritual Childcare` (Seitung Province, Daigo Ward) for the sector-match
  `Coordinate` path. It took four load-test fixes to get there. The full story is in COMPLETED.md.
- **No re-scrape is needed for the above.** That conclusion belonged to the tables-only measurement. The
  scraper-quality finding still stands on its own: `Closest landmark` cells in the tables leak the wiki's
  chat-link copy JS, so those codes exist on the wiki and are being dropped. It's now a data-quality item
  under "Later", not a blocker for RC2.
- **Guidance-level badge and guided-only threshold, shipped as Phases 26 and 27.** The badge is a tier
  (`* Guided` / `+ Coords` / `~ Route` / `· Area` / none), computed per achievement per map over
  remaining bits only. The filter is a threshold (`HereGuidanceFilter`, default `Everything`) applied to
  Here only. DECISIONS 2026-09-09 settled the design questions this entry left open: glyph, word and
  colour together, never colour alone; trail-only is neutral, not red; a threshold, not a boolean. All
  four colours live in one `GuidanceStyle` class.
- **Two honesty fixes, shipped as Phase 25.** `IgnoreNearlyComplete` achievements (424 of 1,112) are
  demoted in the Nearest-to-done sort instead of hidden, with an AP tie-break. Here no longer suggests
  achievements that are `RequiresUnlock` (244) or have unmet `prerequisites` (137).

RC2 is closed. Phase 29 shipped and was confirmed in-game 2026-09-11, and Batch C's half was confirmed
2026-09-10. Ongoing, and not a blocker: coverage misses I hunt go into BACKLOG.md as they come up, without
reopening this milestone.

## RC3 — The hunt loop made good ✅ (closed 2026-09-14, designed 2026-09-11)

I split RC3 into two load-test batches (2026-09-11, see DECISIONS). The list half came first and the
in-play UI half second. The exit criterion is about the second half, and that's better tested against a
Here list that already behaves. The scheduled items left BACKLOG.md when they became phases, and their
records are in COMPLETED.md.

**Batch D, Phases 31–34: the Here list becomes something you act on** (full text in COMPLETED.md):

1. **Phase 31, hide / snooze from Here.** Two actions: *Not interested* (permanent) and *Not today*
   (until the 00:00 UTC reset). A new `HereExclusionService` owns them, so they survive
   `PersistanceService`'s rebuild-on-save. They apply to Here and the toast, never to the category tree
   or the Track window. Un-hide is a "Show hidden (N)" toggle in Here.
2. **Phase 32, track top N.** "Track these" fills the free tracked slots from Here's ranked list, and
   never untracks. One click arms a whole map's hunt, with Phase 16 flipping the routes.
3. **Phase 33, four half-slices.** The Here cap as a 5–15 slider (default 10). An "Anywhere: closest to
   done" section under Here, whose candidates are started-but-not-done account records under the same
   rules and cap. Session summary names on hover. Card grid columns from window width (build-time only).
4. **Phase 34, roaming achievements are opportunistic.** An achievement with no `bits` leaves the ranked
   cap and goes into a "While you're here" line capped at 5. Enemy locations aren't promised (11 %
   coverage). This phase was first to drop if D ran long.

**Batch F, Phases 38–45: the UI redesign. Done, confirmed in-game 2026-09-13.** It was inserted
2026-09-11 as "own card grid and card". On 2026-09-12, after I compared screenshots with the in-game Hero
panel, it grew into the full redesign, which was settled 2026-09-13 against two rounds of mockups. It ran
before E so that E would land on primitives we own. The full record, including the four load-test rounds,
is in COMPLETED.md:

5. Phase 38, `CardGrid` (built). Phase 39, `UiStyle` tokens and a text pass (was Phase 37, pulled forward).
   Phase 40, `AchievementCard`, which replaces `DetailsButton` and shows rank, place and distance, objective
   pips and a tier edge. Phase 41, `WindowBodyPainter`, a flat body under the native title bar and tabs,
   plus the Quarry / Target List window copy. The stretched background it replaced was the "low-res texture"
   complaint. Phase 45, the Inspector: one detail pane replacing the detach and subpage pop-outs, with
   objective chips that show API-confirmed vs ticked-by-you states. Phase 43, the Target List: compact rows
   only, with the nearest objective and its distance, and full mode retired. The bearing arrow the brief
   planned wasn't built (see below). Phase 42, size to content and resize. Phase 44, retire the old paths.
   The brief is `docs/UI-DESIGN.md`. Text would be better, but not game-crisp: Blish 1.3.0's bitmap fonts
   through a 0.81× transform at my Interface Size are the ceiling.

**Batch E, Phase 35: the Here strip in the Target List. Done, confirmed in-game 2026-09-14** (in
COMPLETED.md):

6. **Phase 35, the Here strip, one derived height, and a top bar that carries the Quarry shortcut.**
   Three pieces, one build. The strip lets you pick up a new target without opening Quarry, which RC3's
   exit criterion requires. The height function makes the window's height follow what's visible. Before
   it, an 18 px summary band was reserved whether or not there was a summary. The top bar grows from 22
   to 28 px and tints the Quarry shortcut, so the way back to the full list is easy to find.
7. Phase 36, compact row toggles with one height function. Retired 2026-09-14. Batch F's Phase 43 left
   four of its six toggles with nothing to toggle, and the user-resizable window from Phases 42/43 was the
   fix the height function was after. I decided no toggles ship (DECISIONS 2026-09-14).
8. Phase 37, contrast, background, token set. Moved into Batch F as Phase 39 (2026-09-12).

Two items stayed on RC3's list without joining a batch, and on 2026-09-14 I kept them out of E:

- Buyable-item marking on remaining-item lines, from one cached `/v2/commerce/prices` batch. Only ~4 % of
  collection item bits are tradeable, so it's a shortcut finder over a small set, with skins and minipets
  unmarked (BACKLOG has why).
- The bearing arrow, which needs a player→objective world position that `NearestObjectiveService` doesn't
  expose.

Both stay in BACKLOG as the first candidates after RC3.

RC3 closes when I play a session using only the Target List, the Inspector and toasts, and don't miss the
Quarry window. It closed 2026-09-14 on Batch E's confirmation.

## 2.1 — Fresh data and polish (planned 2026-09-24)

Longer-running than a 2.0.x release, and separate from them: bug fixes keep shipping as 2.0.x while this
is built. Nothing here is a phase yet. When an item is picked up, it becomes a phase in PLAN.md as usual.

**Why data leads.** Measured 2026-09-24 (`docs/analysis/data_staleness.py`, re-runnable): the live API
lists 8,339 achievements and our hosted data 6,778. Of the 1,613 we lack, 884 are uncategorised and 447
are Historical. **268 are live**, mostly content released since April: all of *Eternity's Garden* and
*Leyspring Hollows* (47 each), the *Code of Creation* and *The Only Way* story chapters, the *Solitary
Throne* fractal, about 50 Bonus Events achievements and 16 Rare Collections. The module can't show any of
them. The All tab and Here both build from the wiki data, and `PersistenceService` drops a tracked id the
data doesn't know. So a player in Leyspring Hollows gets silence, not the "the data can't answer" message
the product rule asks for. The gap grows with every game release.

**Data, in this order** (each one makes the next safer):

1. **API-only achievements.** For an id the API has and the wiki data doesn't, build the entry from the
   API: name, description, bits, tiers, category. The achievement appears, can be tracked, and honestly
   shows guidance tier *None*. There's nothing to scrape or host, and the next expansion is covered on
   day one. Mostly `AchievementService` work.
2. **Unit tests for the pure bits** (BACKLOG, Performance / robustness). Regenerated wiki data can reorder
   rows, and `specialSnowflakeCompletedHandling` and bit alignment are where that would silently break
   progress. Tests come before any regenerated file ships.
3. **Generate the data ourselves, in our own format** (all-in, settled 2026-09-24, DECISIONS). The wiki
   now exposes what the old scraper had to dig out of rendered HTML. Semantic MediaWiki holds every
   achievement as a record keyed by game id (category, type, points, hidden/historical, mastery), and
   the objectives are named template fields (`Achievement table row | id = …`,
   `Objectives table row | col1 = {{point of interest|…}}`). So:
   - **Source:** `api.php` only (SMW `ask` plus template parsing), no rendered HTML and no `cookies.txt`,
     incremental by page revision. The old data's artefacts go with the scrape: 3,057 of 6,809 names
     carry stray whitespace, and the "Closest landmark" chat codes are lost.
   - **One file, keyed by API id, carrying only what features read.** It replaces
     `achievement_data.json` (8.8 MB, with unused `Reward`/`Cite`/`HasLink`), `achievement_tables.json`
     (20.7 MB, downloaded for 0.22 MB of Notes) and the embedded `derived_subpages.json` (6.8 MB, built
     from Denrage's 70 MB `subPages.json`). The same file, embedded in the `.bhm`, is the offline
     fallback. `Gw2WikiDownloader`, `DerivedSubpageGenerator` and very likely `Quarry.WikiData` retire.
   - **Hard parts at build time:** each row's API bit index written out (the runtime matcher becomes a
     fallback; `specialSnowflakeCompletedHandling` becomes a tested overrides file), and each row's
     map(s), from coordinates, `point of interest` names, sectors and zone names.
   - **New fields only where a feature reads them.** First candidate: the category's `requires`
     (e.g. `voe`), so Here can label content the account doesn't own.
   - **Provenance and safety:** our own `version.json` (schema version, `generated` stamp, sha256). The
     Action validates (every live API id accounted for, alignment coverage not regressing) and opens a
     PR with a diff summary rather than publishing blind.
   - **Constraints:** a new path, because 2.0.x installs keep reading the frozen version 9 files. The
     parser fails loudly on template drift. Wiki contributor content is GFDL 1.3, so the file and the
     README carry the notice and attribution.
   - **First step, a spike:** one category a player can test (Auric Basin), diffed row by row and bit by
     bit against the current data, and measuring what share of rows resolve to a map (see 4).
     **Done 2026-09-24** (`docs/analysis/generator-spike-findings.md`): all 35 achievements and 58 rows
     recovered, 57 of 58 rows aligned to their bit by position, and 46 rows placed on the map (17 exact
     points, 29 sectors) where the current data places none. Next: a cross-map place index, so core
     Tyria and multi-map rows resolve to a map id.

4. **Map-aware Here, built on per-row maps.** Today Here's membership is the category link plus "the
   pack index has a marker for it on this map", done or not. Wiki locations only colour the badge; they
   never add a candidate. So a multi-map achievement shows on a map where everything left is elsewhere,
   and a core-Tyria achievement that only the wiki places never shows at all. With each row's map
   known, the rule becomes *at least one remaining step is on this map*, with the category link as the
   fallback for achievements whose rows can't be placed. Where the category says "here" but what's left
   is elsewhere, the card can say where ("3 left, in Auric Basin"). The same data lifts some
   achievements out of the opportunistic line: a description like "Search in the Griffonfall area"
   links a place the generator can resolve. Coverage is unknown until the spike measures it. Phase 28
   found coordinates on only 1.8 % of rows, so map membership has to come mostly from landmark and zone
   names, which the old scrape lost. The pack-index half of the multi-map problem has a small runtime
   fix that doesn't wait for 2.1 (BACKLOG).

**Polish** rides along, picked from BACKLOG at batch boundaries. The first candidates: the flat window
body that reads as blank (one file, `WindowBodyPainter`), auto-sizing the Quarry window to its content,
the "some remaining steps are wiki-only" marker on the guidance badge, and buyable-item marking (useful on
~4 % of item bits, so a rider, not a headline). Not in 2.1: the bearing arrow until
`TryContinentToWorld`'s precision is measured, and our own font until Blish ships TTF loading.

**Testing constraint.** Most of what item 1 unlocks is Visions of Eternity content, which I can't check on
my account. The principle can be checked on Rare Collections, the Explorer/Rift Hunting stragglers and
Solitary Throne. VoE itself goes to a reporter to confirm, per CLAUDE.md. Item 4's core-Tyria and multi-map
behaviour is testable on my account.

## 2.2 (was RC4, candidate) — The module becomes a producer, not just a consumer

Renamed 2026-09-24, when 2.1 was set to fresh data and polish. This isn't committed. It would take the project past "a tracker plus routes" to something no other module
does. All three items were written up 2026-09-14, with feasibility notes, and are tracked privately. The order
matters, because each one feeds the next.

1. **Pin a location while you play.** Right-click a remaining bit to store the current Mumble position.
   It has no data dependency, fixes the core-Tyria tail by hand, and gives the next item its input when
   the wiki has nothing.
2. **★ Generate a personal marker pack from what you're hunting.** Write a `.taco` tagged with
   `achievementId`/`achievementBit` into Pathing's markers folder. Pathing renders it in-world and hides
   each icon as you complete its bit. That upgrades our own yellow tier to green and makes the module a
   producer of pack data. The open risk is height. Wiki coordinates are 2-D, so `zpos` is a guess. Prove
   the mitigation on ~10 objectives before committing.
3. **Route mode.** Put the map's remaining objectives in walking order, with a waypoint hop wherever a leg
   is long and an optional time limit. Ten things to do becomes one loop to run.

## Standing decision — no-overlay mode

Some people will run Quarry without Pathing by choice, so pack-less should be a supported mode, not a
degraded one. That argues permanently against making Pathing a hard dependency.

## Resize — resolved (worked around) by Batch F, 2026-09-13

This was "the resize mystery". Resize handles were blocked on an unexplained failure: `CanResize`
reflowed a window's height but not its width. Batch F's `CardGrid` (Phase 38) and its `Fill`-sizing
children (Phase 42) avoid the failing code path. Width is computed from `ContentRegion` on every layout
pass instead of being assigned once. I confirmed drag-resize "works perfectly" on both the Quarry and
Target List windows. The root cause is still unexplained, since nobody attached a debugger, but nothing
depends on it any more. The full story is in COMPLETED.md's "Resolved backlog items". The intermittent
corner-reset-on-restart bug this was paired with is still open, as `ArranPell/Quarry#3`.

## Data independence — Batch G, Phases 46–48 — done, confirmed in-game 2026-09-14

Measured 2026-09-12 and checked against the C# on 2026-09-13. Two of the three pieces were worth doing on
their own merits, whether or not the hosting risk ever lands. Phase 46 stopped deserializing 20.7 MB of
`achievement_tables.json` on every start, when nothing displayed it. Phase 47 put the wiki Notes column in
the Inspector: 9,051 hand-written hunter notes, 0.22 MB gzipped. Phase 48, the derived subpage file, is the
data-independence phase proper. `subPages.json`'s 70 MB runtime download is gone, replaced by a ~1.2 MB file
embedded in the `.bhm`. The full record, including the load-test round, is in COMPLETED.md.

Half the hosting risk closed on 2026-09-20 (2.0.2). The three files are now served from our own
`bhud-static/ArranPell.Quarry` branch instead of Denrage's namespace (see publish-gate item 6). They're
byte-for-byte mirrors, though, so the staleness half is untouched: `last-modified 2026-04-22`, still v9,
five months of drift. We serve the files but can't regenerate them. What the review changed:

- **Making the data leaner is a smaller job than this section claimed, and a different one.** The
  Inspector reads two subpage fields (`Description`, `ImageUrl`). The location index reads coordinates
  plus five `DescriptionList` keys. The old "0.53 MB" figure and the 2026-09-12 "0.90 MB" figure used
  different place-key sets, and neither matches the module's own. Re-measure before claiming a size.
- **Running `Gw2WikiDownloader` ourselves is more work than the cadence plan assumes.** It scrapes
  rendered HTML rather than `api.php`, and requires a `cookies.txt` at startup. Its achievement-data and
  tables paths are commented out, so a run as committed writes only `subPages.json`. It produces no
  `version.json` and computes no md5s, which is what the module verifies against, so that piece isn't in
  this repo. It has to be written before any GitHub Action can deploy something the module will accept.
- **Cadence is unchanged and still unscheduled.** Releases rather than the calendar, a monthly Action
  plus an on-demand run after each release, and a `generated` stamp in `version.json`. BACKLOG keeps it.

Still in BACKLOG.md and still unscheduled: a vendored snapshot fallback in the `.bhm`; the disk-persisted
API cache and the AP tie-break follow-up; retry/backoff on batch failures. SSRD `bhud-static` hosting left
this list on 2026-09-20, done as gate item 6.

## Publish gate — separate from the RCs — ✅ closed 2026-09-20

Taken when an RC feels worth showing someone (Denrage, the Blish Discord), not tied to a number. Every
item is in BACKLOG.md Housekeeping. This is the checklist.

Reconciled 2026-09-14: items 1, 2, 4, 5 and 8 moved into Batch H and aren't specified here. Batch H's Phases
49–56 covered them, and their records are in COMPLETED.md. What's left below is release mechanics. Those
belong at the gate because they depend on the repo already being public, or on a decision only the release
forces.

0. **Go public from a fresh repo instead of flipping this one.** Decided 2026-09-14 (DECISIONS). It's
   first because items 6 and 7 both need a public repo to exist. The plan: create a new public repo under
   the name Phase 52 settles, seeded with one initial commit of the Batch H end state.
   `ArranPell/AchievementTrackerPlus` stays private and frozen as the working archive. The new repo
   becomes the working repo, so there are never two live ones.

   This solves three problems at once. It keeps private correspondence in this repo's history out of the
   public one (Phase 49). It drops a history that's mostly Denrage's commits, with `main` still tracking
   upstream, which contradicts our position that this is a different module and not a fork. And it fixes
   the repo name. The cost is granular public commit history. In this project that history lives in
   `COMPLETED.md` and `DECISIONS.md` rather than in commit messages, and both stay in the archive. Losing
   the `upstream` remote costs nothing: the documented way to send Denrage a PR already uses a separate
   `prfork` clone of his repo.

   What ships in that initial commit is settled. It's the trimmed set: `src/`, `LICENSE`, `.gitignore`,
   `README.md`, `CHANGELOG.md` (item 7) and `docs/{PLAN,ROADMAP,BACKLOG,UI-DESIGN,PATHING-PR}.md`, since
   joined by `CONTRIBUTING.md`, `SECURITY.md` and the `.github/` issue templates. The private working docs
   stay private: `DECISIONS.md`, `COMPLETED.md`, and the working notes and analysis kept alongside them for
   backup and history. The manifest `url` changes to the new repo in the same commit (Phase 56 item 34).

   Done (2026-09-15): `ArranPell/Quarry`, one commit, seeded this way. One refinement (DECISIONS
   2026-09-15): the new repo can't be the working repo, because the never-ship docs need to keep living
   somewhere versioned. So this private repo stays the working repo, and the public one gets a fresh
   snapshot commit per release.

   The seed is mechanical, not a checklist. `.gitattributes` marks every never-ship path `export-ignore`,
   and `git archive` honours it. From this repo, on `fork`:

   ```
   git archive --format=tar --prefix=quarry/ fork | tar -x -C <somewhere outside this repo>
   git archive fork | tar -t | grep -E 'CLAUDE|\.claude|DECISIONS|COMPLETED|handoff|PROJECT-HANDOFF|SESSION-|analysis|private|secrets'
   ```

   **The second line must print nothing.** If it prints anything, a never-ship file would go public. Then,
   in the extracted folder: edit the manifest `url`, add `CHANGELOG.md` (item 7), `git init`, make one
   commit and push to the new public repo. Don't copy the working folder instead of archiving: that ships
   everything.
1. Module rename (Batch H Phase 52). Done (2026-09-14): the module is Quarry, namespace
   `ArranPell.Quarry`, collision-checked against `blish-hud/bhud-pkgs` (1,105 manifests, no "quarr"). The
   public repo took the name at item 0, and the manifest `url` changed then (Phase 56 item 34).
2. README and module description (Batch H Phase 55). Done (2026-09-14, Phases 51 and 55): the description
   carries the "Based on Denrage's Achievement Tracker (MIT)" line and link, `README.md` exists, and
   `LICENSE` carries both notices. Never drop his.
3. `HuntMode` default. Done (2026-09-15): it stays off. Someone installing for the first time shouldn't
   find their Pathing categories changing under them (DECISIONS 2026-09-09). The README and the setting's
   description both say how to turn it on.
4. Divergence housekeeping (Batch H Phases 49, 50 and 53). Done (2026-09-14). `Gw2WikiDownloader` stays
   in-tree, because it's the only route to regenerating the hosted files. `Quarry.WikiData` stays a
   separate project because `Gw2WikiDownloader` references it. Phase 48 did *not* extend
   `Gw2WikiDownloader`; `DerivedSubpageGenerator` is standalone. `Persistance` → `Persistence` was done
   in Phase 53.
5. Contingency hooks and log-level audit (Batch H Phase 54). Done (2026-09-14) as Phase 56 items 12–15
   and 27: `Blish_HUD.Debug.Contingency` at the three denied-write / no-network sites, and the log cut to
   12 Info lines per start (GitHub issue #13).
6. SSRD static hosting of the data files. Done (2026-09-20), shipped in 2.0.2. The contributor account
   arrived 2026-09-19. `ArranPell/Quarry` is registered in SSRD with a push webhook. The three files live
   on an orphan `bhud-static/ArranPell.Quarry` branch and are served at
   `https://bhm.blishhud.com/ArranPell.Quarry/data/`. `AchievementService`'s three URLs were repointed in
   2.0.2 (commit `cf3bfae`) and cold-install tested. The served copies were re-downloaded and hashed.
   They're byte-for-byte identical to Denrage's originals, with md5s matching what `version.json`
   publishes.

   Hosting our own copies means fresh installs keep working if Denrage's namespace ever goes away. It
   doesn't make the data any newer. The files are still the April build (v9, `last-modified 2026-04-22`),
   and `Gw2WikiDownloader` can't rebuild them yet (see Data independence). If Denrage publishes v10, his
   users get it and ours don't. Existing installs paid nothing on upgrade: `version.json` still reports
   Version 9 and the cached md5s still match, so the download path never runs. That also means a warm test
   proves nothing. Only a cold install is evidence.

   Embedding the other files too is costed, and not ruled out. Raw, the three files are 28.1 MB
   (`version.json` 201 B, `achievement_data.json` 8.4 MB, `achievement_tables.json` 19.7 MB). The `.bhm`
   is a zip, though, so raw bytes are the wrong comparison. Measured 2026-09-20, they deflate 13.8:1 to
   2.0 MB (8.4 → 0.85 MB, 19.7 → 1.18 MB), which would take the `.bhm` from 2.2 MB to roughly 4.2 MB.
   That's close to the "~2.2 → ~6 MB" estimate in DECISIONS 2026-09-15. Embedding would also remove
   ~28 MB of first-run download per user, the md5 verification machinery, and the hosting dependency. The
   one thing hosting offers over embedding is refreshing data without a module release, and that's worth
   nothing while the data can't be regenerated. It's a live option for a 2.x, and it deserves its own
   decision.

   **Warning: the static branch must keep `* -text` as its first commit.** SSRD builds on Windows (its
   Sentry stack paths are `C:\BhApps\BhudRequestFor\work\...`), and the data files are LF-only, with
   585,648 newlines in `achievement_tables.json`. Any line-ending conversion on SSRD's clone would change
   every byte count and fail the md5 check. `DownloadFile` would use up its three retries and leave fresh
   installs with no data. It would fail silently, with the branch looking perfect. So the static branch's
   first commit is a `.gitattributes` containing `* -text`. Never remove it, and never add a data file to
   that branch ahead of it.

   Still on Denrage's URLs: `DerivedSubpageGenerator`'s `subPages.json` source (70 MB, build-time only).
   It isn't mirrored, because re-cloning 70 MB on every webhook push is out of proportion for a tool run
   twice a year. Archiving a local copy is cheap insurance if that dependency ever matters.
7. Release build and `CHANGELOG.md`, keyed by manifest version. Done (2026-09-15): 2.0.0. `CHANGELOG.md`
   is at the repo root, keyed by the manifest version. `dotnet build -c Release` writes
   `src\Quarry\bin\Release\net4.7.2\Quarry.bhm` (2.2 MB, pdb included so Blish crash reports keep line
   numbers). It was dev-loaded before release, and GitHub release `v2.0.0` on the public repo carries it.
8. Public-readiness code review (Batch H Phase 54), with its findings as Phase 56. Done (2026-09-14): a
   six-lens review, 69 confirmed findings and 41 fix items, all landed except the manifest `url` (item
   0's).

Every gate item is now done: 0, 3 and 7 on 2026-09-15, and 6 on 2026-09-20.

## Code review — two scoped reviews at the gates

Decided 2026-09-09 (DECISIONS): no single full review. The 06 Sept review was lens-scoped, and its
findings cost a whole arc (Phases 11–14). A repo-wide pass at the wrong moment buys nits in code that's
about to change.

- **Hunter-code review ✅ ran 2026-09-09.** Its four findings became Phase 24. The significant one was
  trails indexed at (0,0,0); the cache schema is now 5. The scope was Phases 15–17 only, plus the two
  post-batch fixes. The lenses were the 06 Sept ones (lifecycle, threading, exceptions) plus data: pack
  parse correctness, cache invalidation and the schema-bump path, the tagged-vs-untagged preference rule,
  the reflection bridge's failure modes when Pathing is absent or changes, and the 2 s accumulator's
  behaviour across window show/hide and map change.
- **Public-readiness review ✅ ran 2026-09-14 as Batch H Phase 54.** It moved from "at the publish gate"
  so it would run over the tree as it would ship. The scope was the whole repo: dead code and upstream
  remnants, log levels, anything embarrassing in a public tree, and the Phases 18–23 code that landed
  after the 06 Sept review. Six lenses, 69 confirmed findings and 41 fix items, all landed as Phase 56
  (publish-gate item 8). The brief and results are in COMPLETED.md under Batch H.

Carried from the 06 Sept review and not re-reviewed: item 18 (optional main-thread marshalling) and
item 33 (sync `ReadAllText` on load; do nothing unless it hangs).

## Not on the roadmap

Everything in BACKLOG.md's Closed and Explicitly out of scope sections: meta-event timers, Wizard's Vault,
WvW/PvP, drawing our own markers, raising the tracked cap, and regenerating wiki data as a product feature.
Also not planned: taking over or merging back into Denrage's module (DECISIONS 2026-09-08). Further upstream
PRs stay optional under the `upstream-candidate` rule (BACKLOG, Housekeeping). The promised one is done.

## Releasing — the snapshot-per-release loop (from 2.0.0, 2026-09-15)

This private repo is the working repo. The public repo, `ArranPell/Quarry`, holds one commit per release.
Each is a `git archive` snapshot of this tree, so the `.gitattributes` `export-ignore` set never ships.
The steps, from this repo on `fork` with a clean tree:

1. Bump `version` in `src/Quarry/manifest.json`. Check that `contributors` is still there, because
   Blish's repo downloader requires it (DECISIONS 2026-09-21). Add the release's section to
   `CHANGELOG.md` and commit.
2. `dotnet build src/Quarry/Quarry.csproj -c Release`; dev-load `bin\Release\net4.7.2\Quarry.bhm` once.
   This checks the source, not the file users get: SSRD builds its own `.bhm` from the public repo
   (step 5).
3. Snapshot into a clone of the public repo and push:

   ```
   git clone https://github.com/ArranPell/Quarry <scratch>\Quarry && cd <scratch>\Quarry
   git rm -rq . && git archive --format=tar <private-repo-commit> | tar -x
   git add -A && git commit -m "Quarry <version>" && git push
   ```

   `git rm -rq .` comes first so that files deleted since the last release disappear from the snapshot
   too. **The public commit message is exactly `Quarry <version>`, with no trailers.** No
   `Co-Authored-By`, no session links. An AI session's default attribution lines put a private session
   URL into public history. 2.0.4's `cbc3bb8` went out with both (2026-09-23). Author it as
   `ArranPell <35847740+ArranPell@users.noreply.github.com>` (DECISIONS 2026-09-20).
4. **Optional.** `gh release create v<version> src\Quarry\bin\Release\net4.7.2\Quarry.bhm --repo ArranPell/Quarry --title "Quarry <version>" --notes-file <the CHANGELOG section>`.
   The GitHub release is for people browsing the repo. Nothing in the install path reads it (step 5).
   From a cloud session: the git proxy refuses tag pushes (2.0.4, 2026-09-23), so the tag gets created
   along with the release, targeting the snapshot commit.
5. The in-game module repository. Quarry has been listed there since 2.0.3 (2026-09-21). The listing went
   in 2026-09-20, but 2.0.2's manifest lacked `contributors` and the downloader failed on it. The listing
   takes its description from the public repo's metadata, so keep Discussions on. **A new version
   reaches it by a manual SSRD submission** (recorded at 2.0.4, 2026-09-23). SSRD builds the `.bhm`
   fresh from the public repo's source, so no uploaded file is involved. A submission can be flagged
   **pre-release**, which reaches only users who opt in. That's the way to hand a fix to a bug reporter
   to confirm before everyone gets it. 2.0.4 went out that way first. **A version number used on the
   pre-release track isn't reused for the normal branch:** 2.0.5 is 2.0.4 with only the version bumped,
   submitted to the normal branch, and the `v2.0.4` GitHub release is marked Pre-release to match.

Never push this repo's branches to the public repo. Keeping that history private is why item 0 exists.
