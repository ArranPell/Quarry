# Roadmap

> **Reading this in the public repo?** `docs/DECISIONS.md` (the decision log) and `docs/COMPLETED.md`
> (the completed-phase archive), cited throughout, live in the author's private working repo along with
> the other archive-only docs. The citations are left as-is: they point at a record that exists, just not
> here.

Written 2026-09-09 after Denrage settled the licence (issue #7); reconciled 2026-09-09 (evening) once
RC1 shipped and the docs were re-split; **reconciled again 2026-09-11** against PLAN.md after the
hunter-code review ran and Batch C shipped — this file had drifted a full milestone behind — and
**corrected the same day** once Batch C's load-test came back confirmed, and again once Phase 29 (RC2's
last piece) shipped and was confirmed the same day. This file says **what the milestones are and what goes in
each**. The doc layout around it:

- `docs/PLAN.md` — only what's active or not started, with **Status** lines. Nothing sits here between
  milestones.
- `docs/COMPLETED.md` — the historical record: every finished phase verbatim, including load-test
  results. Search it by phase number.
- `docs/BACKLOG.md` — unscheduled ideas. An idea becomes a PLAN.md phase when picked up and leaves the
  backlog; this roadmap only *groups* backlog items into milestones, it doesn't schedule them.
- `docs/DECISIONS.md` — append-only, why we chose what we chose.

**RCs are milestones for ArranPell, not releases.** Each ends with a load-test batch and a stretch of actually
playing with it. Going public is a separate gate (below), taken when an RC feels worth showing someone,
not on a date. Product rule unchanged: bounded sessions, not completeness.

## Where we are

```
Upstream PR ✅  →  RC1 ✅  →  hunter-code review ✅  →  Batch C ✅  →  Phase 29 ✅  →  Batch D ✅
  →  Batch F ✅  →  Batch G ✅  →  Batch E ✅ (RC3 closed)  →  Batch H ✅  →  publish gate ✅ (2.0.0)
  →  [post-release ← HERE: SSRD listing + hosting, load-test fixes, BACKLOG]
```

- **Batch D (Phases 31–34) — done, confirmed in-game 2026-09-11.** In COMPLETED.md.
- **Batch F — the UI redesign — done, confirmed in-game 2026-09-13.** Our own card grid, card, window
  body, the Inspector, the Target List, resize, and retiring the old card path — built across eight
  phases plus four rounds of load-test fixes (Target List layout bugs, Inspector title/chip/navigation
  fixes, drag-resize added to both windows, new navigation shortcuts between the Quarry and Target List
  windows). Brief in `docs/UI-DESIGN.md`, reasoning in DECISIONS 2026-09-12 and 2026-09-13, full record
  in COMPLETED.md.
- **Batch G — the data batch — done, confirmed in-game 2026-09-14.** Three service changes Batch F ruled
  out: stopped parsing `achievement_tables.json` at startup (Phase 46), the wiki Notes column in the
  Inspector (Phase 47), the derived subpage file replacing `subPages.json`'s 70 MB runtime download
  (Phase 48) — plus one load-test round fixing undecoded HTML entities, a leaking wiki chat-link script,
  a missing Map-column fallback image, and turning wiki chat-link placeholders and Inspector images into
  in-app, click-to-copy/expand affordances instead of both sending the player to a browser. Full record
  in COMPLETED.md.
- **Batch E — Phase 35, the Here strip — done, confirmed in-game 2026-09-14. RC3 closed on it.** The
  strip in the Target List, one height function over what's actually visible, the taller top bar with the
  tinted Quarry shortcut — plus one load-test round (a bigger "+" glyph; the strip no longer sticks on a
  stale "permissions not granted" when its first fetch beats the subtoken update). Full record in
  COMPLETED.md.
- **Batch H — cleanup and public-readiness — done, confirmed in-game 2026-09-14** (the full 13-step
  checklist including both destructive checks; no regressions). All eight phases on `fork` (`ca8e3de` …
  `4c1e707`): the module is **Quarry** (`ArranPell.Quarry`); the tree is 9 MB, 73 `.cs` files, one build
  configuration; the review ran and its 41 findings are fixed. One post-review call by ArranPell: `CLAUDE.md`
  and `.claude/` are untracked and never ship (`252b7c4`; DECISIONS 2026-09-14). Full record in
  COMPLETED.md. Phases 49–56 were written
  in full in PLAN.md: repo purge (87.25 MB of `data/`, `InteractiveMapTest`, a tracked `__pycache__`,
  and the two docs carrying a third party's private correspondence), 297 KB of unreferenced `ref/`
  assets plus the csproj's inherited ClickOnce cruft and the solution's x64→Any CPU mismatch, retiring
  the Denrage names from our own code, the module rename pulled forward from the gate, a dead-code
  sweep whose size is conditional on Phase 48's Blocker 1 (44 files / 2,508 lines / 19.1 % of the
  module's C# if that answer is "browser"), the public-readiness review, and the README the repo has
  never had. **It runs after Batch E**, not before: the review has to cover E's code, and a repo-wide
  review run ahead of the work buys nits in code about to change. Reasoning in DECISIONS 2026-09-14.

- **Upstream PR — done.** Phase 23 bit alignment ported to Denrage's module; PR opened 2026-09-09 and
  still open awaiting Denrage's review: https://github.com/Denrage/AchievementTrackerModule/pull/8. The
  promise from issue #7 is kept.
- **RC1 — done, confirmed 2026-09-09.** Details below.
- **Hunter-code review — ran 2026-09-09.** Its findings became **Phase 24**; see "Code review".
- **Batch C = Phases 24–28, built 2026-09-09**, plus **Phase 30** and its follow-ups (fixes from ArranPell's
  Batch C run) built 2026-09-10. **All confirmed in-game by ArranPell 2026-09-10**, the whole `docs/handoff.md`
  checklist walked end to end; the phases are in COMPLETED.md and the docs were committed 2026-09-11.
  PLAN.md owns the per-phase Status lines.
- **Phase 29 — done, confirmed in-game by ArranPell 2026-09-11.** RC2's last piece; both new guidance tiers
  (sector-match `Coordinate` and `Area`) verified live, after four same-session follow-up fixes (two wiki
  place-name bugs, a Track-window visibility gap, an Area-tier wording fix). Full story in COMPLETED.md.
- **RC3 designed 2026-09-11** as two batches, then a third (F) inserted the same day and reshaped into
  the UI redesign on 2026-09-12, and a fourth (G, the data batch) scheduled 2026-09-13. Batch D, Batch F
  and Batch G are all done. **Batch E was written up in full 2026-09-14 and collapsed from two phases to
  one** — Phase 36's toggle set is retired (DECISIONS 2026-09-14). **Batch E is done and confirmed
  2026-09-14; RC3 is closed. Batch H is done and confirmed 2026-09-14. The publish gate was taken
  2026-09-15: `ArranPell/Quarry` is public, release v2.0.0 carries `Quarry.bhm`.** Next action: the SSRD
  contributor account (listing + hosting), then whatever the first public users report.

## RC1 — The hunter exists ✅ (Phases 15, 16, 17 — all in COMPLETED.md)

The module stopped being a tracker with a Here tab. What actually landed:

- **Phase 15 — pack ingest.** `IMarkerPackIndexService`/`MarkerPackIndexService` on **TmfLib 2.2.5**
  (option 1 of the plan's parser decision; two TmfLib API deviations found and worked around before
  coding — see DECISIONS 2026-09-09 and handoff.md). Cached `markerPackIndex.json`, schema v2. Here
  consumer shipped: union of the category and index sources, `Guided` label on the card,
  `NoCategoryForMap` only when both sources are empty.
- **Phase 16 — hunt mode.** `PathingBridge` by reflection (three members; the manifest-dependency and
  compile-time-reference options were both rejected — DECISIONS 2026-09-09), `HuntService` with the
  "only revert what we flipped, and only if nothing else needs it" rule, settings `HuntMode` /
  `AutoUntrackCompleted` / `HuntRevertOnUnload`, completion toast, Here-card peek button. **The Phase 10
  Pathing probe is deleted.** Absorbed the backlog's auto-untrack item.
- **Phase 17 — nearest objective.** `NearestObjectiveService`, `ObjectiveGeometry` (the `(x, z, y)`
  transpose; no distance conversion needed — pack coords are Mumble-space metres), "Next: <name> · N m"
  on full Track panels with waypoint copy, 2 s accumulator gated on `Visible`.
- **Post-batch play fixes (2026-09-09), both from real hunting:** prefer bit-tagged objectives over
  untagged ones on the same achievement+map (the Auric Basin masks problem — an untagged breadcrumb trail
  kept winning "nearest" and sent the player to cleared ground), plus `NearestWaypoint` never checking
  completion state at all; and row-number prefixes on remaining-objective names so similar-sounding steps
  can be told apart. Both in DECISIONS.

## RC2 — Here covers multi-map achievements — ✅ **done, confirmed 2026-09-11**

The original pain: achievements whose sub-objectives span several maps never appeared in Here, because
Here worked from a single category→map link. Phase 15's index fixed that for every pack-tagged
achievement, and Phase 17 delivered the per-map presentation ("Next" for routes here, "Not on this map"
for routes elsewhere) that was RC2's polish item. That answered the original pain, but RC2 then grew real content of
its own — the guidance tiers, the threshold and the wiki-location hint — which shipped as Phases 26,
27, 28 and (2026-09-11) **29**, RC2's last piece:

- **Measurement, no code: what still doesn't show?** RC1 has been played with; the misses to log are
  achievements ArranPell actually hunts that *no* pack covers. The play session so far surfaced pack-data
  quality issues (tagged vs untagged), not coverage gaps — which is weak evidence that coverage is
  adequate. Keep noting misses in BACKLOG.md as they come up rather than opening a phase.
- **The wiki-derived spike answered yes, and shipped.** The source is `subPages.json`, not
  `achievement_tables.json` (the first measurement used the wrong file and concluded no). **Tier 1 —
  exact `InteractiveMap.Coordinates` — shipped as Phase 28.** Note that the coverage numbers once quoted
  here (5.6 % of rows, 58 achievements ≥50 %, 48 at 100 %) were **superseded when Phase 28 re-measured
  on the join the module actually performs** (`achievement_data.json` EntryList rows): **379 of 20,914
  rows (1.8 %) carry a coordinate, 31 achievements have one on every row, 66 on at least half.** Same
  shape — bimodal, hunt-shaped, opportunistic rather than a general fallback — smaller headline number.
  Use PLAN.md Phase 28's figures, not these.
- **Tier 2 — shipped and confirmed as Phase 29 (2026-09-11).** Area-named rows resolve against
  the map's **sectors** (`/v2/continents/:c/floors/:f/regions/:r/maps/:m` returns each sector's `name`
  and `coord`), so they yield real coordinates and a real countdown — **tier-1 quality, not the
  no-distance tier this roadmap originally imagined**. It covers the Explorer achievements, which
  nothing we shipped before this touched. Confirmed live on real account data: `Zone Defense` (Caledon
  Forest) for the `Area` fallback, `Spiritual Childcare` (Seitung Province, Daigo Ward) for the
  sector-match `Coordinate` path. Took four load-test fixes to get there — full story in COMPLETED.md.
- **No re-scrape needed for the above** — that conclusion belonged to the tables-only measurement. The
  scraper-quality finding still stands on its own (`Closest landmark` cells in the tables leak the wiki's
  chat-link copy JS, so those codes exist on the wiki and are being dropped), but it's now a data-quality
  item under "Later", not a blocker for RC2.

- **Guidance-level badge + guided-only threshold — shipped as Phases 26 and 27.** The badge is a tier
  (`* Guided` / `+ Coords` / `~ Route` / `· Area` / none), computed per achievement per map over
  remaining bits only; the filter is a threshold (`HereGuidanceFilter`, default `Everything`) applied to
  **Here only**. The design questions this entry left open were all settled in DECISIONS 2026-09-09:
  glyph + word + colour never colour alone, trail-only is **neutral not red**, threshold not boolean.
  All four colours live in one `GuidanceStyle` class.
- **Two honesty fixes — shipped as Phase 25.** `IgnoreNearlyComplete` (424 of 1,112) demoted rather than
  hidden in the Nearest-to-done sort with an AP tie-break, and Here no longer suggests achievements that
  are `RequiresUnlock` (244) or have unmet `prerequisites` (137).

RC2 is closed: Phase 29 shipped and was confirmed in-game 2026-09-11, and Batch C's half was confirmed
2026-09-10. Ongoing, not a blocker: keep noting any coverage misses ArranPell actually hunts into BACKLOG.md
as they come up rather than reopening this milestone.

## RC3 — The hunt loop made good — ✅ **done, closed 2026-09-14** (designed 2026-09-11)

**Two load-test batches** (ArranPell's call, 2026-09-11 — see DECISIONS). The list half first, the
in-play-UI half second, because the exit criterion is about the second and it's better tested against a
Here list that already behaves. Scheduled items have left BACKLOG.md per the rule; PLAN.md owns their
status.

**Batch D — Phases 31–34, the Here list becomes something you act on** (PLAN.md has the full text):

1. **Phase 31 — Hide / snooze from Here.** Two actions: *Not interested* (permanent) and *Not today*
   (until 00:00 UTC reset). Owned by a new `HereExclusionService` so it survives `PersistanceService`'s
   rebuild-on-save; applies to Here and the toast, never to the category tree or the Track window.
   Un-hide via a "Show hidden (N)" toggle in Here.
2. **Phase 32 — Track top N.** "Track these" fills the free tracked slots from Here's ranked list, never
   untracks. One click arms a whole map's hunt (Phase 16 flips the routes).
3. **Phase 33 — four half-slices:** Here cap as a 5–15 slider (default 10); an **"Anywhere: closest to
   done"** section under Here (candidate set = started-but-not-done account records, same rules, same
   cap); session summary names on hover; card grid columns from window width (build-time only).
4. **Phase 34 — roaming achievements are opportunistic.** No `bits` ⇒ out of the ranked cap, into a
   "While you're here" line capped at 5. Enemy locations explicitly not promised (11 % coverage).
   First to drop if D runs long.

**Batch F — Phases 38–45, the UI redesign — done, confirmed in-game 2026-09-13** (inserted 2026-09-11 as
"own card grid and card", reshaped 2026-09-12 into the full redesign after ArranPell's screenshot comparison
with the in-game Hero panel, settled 2026-09-13 against two rounds of mockups; ran **before** E so E
lands on primitives we own). Full record, including the four load-test rounds, in COMPLETED.md:

5. **Phase 38 — `CardGrid`** (built), **39 — `UiStyle` tokens + text pass** (was Phase 37, pulled
   forward), **40 — `AchievementCard`** (replaces `DetailsButton`: rank, place + distance, objective
   pips, tier edge), **41 — `WindowBodyPainter`** (a flat body under the native title bar/tabs; the
   stretched background was the "low-res texture" complaint) **+ the Quarry / Target List window
   copy**, **45 — the Inspector** (one detail pane replacing the detach and subpage pop-outs; objective
   chips with API-confirmed vs ticked-by-you states), **43 — the Target List** (compact rows only, with
   the nearest objective, bearing and distance; full mode retired), **42 — size to content + resize**,
   **44 — retire the old paths**. Brief: `docs/UI-DESIGN.md`. Text will be better, not game-crisp —
   Blish 1.2.0's bitmap fonts through a 0.81× transform at ArranPell's Interface Size are the ceiling.

**Batch E — Phase 35, the Here strip in the Target List — done, confirmed in-game 2026-09-14** (in COMPLETED.md):

6. **Phase 35 — the Here strip, one derived height, and a top bar that carries the Quarry shortcut.**
   Three pieces, one build. The strip is what lets a new target be picked up without opening Quarry,
   which is what RC3's exit criterion actually requires; the height function makes the window's height
   follow what is visible (today an 18 px summary band is reserved whether or not there's a summary);
   the top bar grows 22 → 28 px and tints the Quarry shortcut so the escape hatch to the full list is
   findable.
7. ~~**Phase 36 — compact row toggles with one height function**~~ — **retired 2026-09-14.** Batch F's
   Phase 43 left four of its six toggles with nothing to toggle, and Phase 42/43's user-resizable window
   was the real fix behind the height function. No toggles ship (ArranPell); DECISIONS 2026-09-14.
8. ~~**Phase 37 — contrast, background, token set**~~ — moved into Batch F as Phase 39 (2026-09-12).

**Still on RC3's list, not in a batch, and explicitly not riding E** (ArranPell, 2026-09-14):
**buyable-item marking** on remaining-item lines — one cached `/v2/commerce/prices` batch; only ~4 % of
collection item bits are tradeable, so a shortcut finder over a small set, skins/minipets unmarked
(BACKLOG has why) — and the **bearing arrow**, which needs a player→objective world position
`NearestObjectiveService` doesn't expose. Both stay in BACKLOG as the first candidates after RC3.

RC3 closes when: ArranPell plays a session using only the Target List, the Inspector and toasts, and doesn't
miss the Quarry window. **Closed 2026-09-14 on Batch E's confirmation.**

## RC4 (candidate) — The module becomes a producer, not just a consumer

Not committed, but this is where the project stops being "a tracker plus routes" and becomes something no
other module does. All three are in BACKLOG.md with feasibility notes; sequence matters because each one
feeds the next.

1. **Pin a location while you play** — right-click a remaining bit, store the current Mumble position.
   Zero data dependency, fixes the core-Tyria tail by hand, and is the input the next item needs when the
   wiki has nothing.
2. **★ Generate a personal marker pack from what you're hunting** — emit a `.taco` tagged with
   `achievementId`/`achievementBit` into Pathing's markers folder, and Pathing renders it in-world *and
   hides each icon as you complete its bit*. This upgrades our own yellow tier to green and makes the
   module a producer of pack data. The open risk is height: wiki coordinates are 2-D, so `zpos` is a guess —
   prove the mitigation on ~10 objectives before committing.
3. **Route mode** — order the map's remaining objectives into a walk, waypoint hop when a leg is long,
   optional time-box. The product rule taken literally: not "here are 10 things" but "here is the loop".

## External — pack author collaboration (started 2026-09-10)

Not a milestone and not ours to schedule, but it can change the tiers underneath RC2 more than any code we
write: the Lady Elyssa pack author agreed to add `achievementId` to her 73 untagged categories and to take
help with `achievementBit`, which would move whole achievements from trail-only to fully tagged for every
consumer of her pack. We owe two generated artefacts (the 73-category list; a bit-mapping sample), waiting
on her answers about format and source. Details, her constraints and the notes on her `GW2WikiTool` live in
`docs/PACK-AUTHOR-OUTREACH.md`.

Also raised there and worth a deliberate answer: **no-overlay mode** — some people will run this without
Pathing on purpose, so pack-less should be a supported mode rather than a degraded one (BACKLOG has the
implications). It argues permanently against making Pathing a hard dependency.

## Resize — resolved (worked around) by Batch F, 2026-09-13

Was "the resize mystery": real resize handles gated on an unexplained failure where `CanResize` reflowed
a window's height but not its width. Batch F's `CardGrid` (Phase 38) and its `Fill`-sizing children
(Phase 42) sidestep the failing code path entirely — width is computed from `ContentRegion` on every
layout pass instead of being assigned once — and ArranPell confirmed drag-resize "works perfectly" on both
the Quarry and Target List windows. The original root cause is still unexplained (nobody attached a
debugger), but nothing depends on knowing it any more. Full story in COMPLETED.md's "Resolved backlog
items". The intermittent corner-reset-on-restart bug this was paired with is untouched — see BACKLOG.md.

## Data independence — **Batch G, Phases 46–48 — done, confirmed in-game 2026-09-14**

Measured on 2026-09-12 and checked against the C# on 2026-09-13 —
`docs/analysis/DATA-INDEPENDENCE-FINDINGS-2026-09-12.md` and
`docs/analysis/DATA-INDEPENDENCE-REVIEW-2026-09-13.md`. Two of the three pieces turned out to be worth
doing on their own merits regardless of whether the hosting risk ever lands: **46** (stopped
deserializing 20.7 MB of `achievement_tables.json` on every start, when nothing displayed it), **47**
(the wiki Notes column — 9,051 hand-written hunter notes, 0.22 MB gzipped, in the Inspector). **48** (the
derived subpage file) is the data-independence phase proper: `subPages.json`'s 70 MB runtime download is
gone, replaced by a ~1.2 MB file embedded in the `.bhm`. Full record, including the load-test round, in
COMPLETED.md.

The hosting risk itself is unchanged: wiki data still comes from Denrage's URLs, `last-modified
2026-04-22`, still v9 — five months of drift. What the review changed about the answer:

- **Leanness is a smaller job than this section claimed, and a differently shaped one.** The Inspector
  reads exactly two subpage fields (`Description`, `ImageUrl`); the location index reads coordinates plus
  five `DescriptionList` keys. The old "0.53 MB" figure and the 2026-09-12 "0.90 MB" figure used
  different place-key sets and **neither matches the module's own** — re-measure before claiming a size.
- **Running `Gw2WikiDownloader` ourselves is more work than the cadence plan assumes.** It scrapes
  rendered HTML rather than `api.php`, requires a `cookies.txt` at startup, has its achievement-data and
  tables paths commented out (a run as committed writes only `subPages.json`), and **produces no
  `version.json` and computes no md5s** — the thing the module verifies against is not in this repo.
  That piece has to be written before any GitHub Action can deploy something the module will accept.
- **Cadence is unchanged and still unscheduled**: releases rather than the calendar, a monthly Action
  plus an on-demand run after each release, a `generated` stamp in `version.json`. BACKLOG keeps it.

Still in BACKLOG.md and still unscheduled: vendored snapshot fallback in the `.bhm`; SSRD `bhud-static`
hosting once the repo is public; disk-persisted API cache and the AP tie-break follow-up; retry/backoff
on batch failures.

## Publish gate — separate from the RCs

Taken when an RC feels worth showing someone (Denrage, the Blish Discord), not tied to a number. Every
item is in BACKLOG.md Housekeeping; this is the checklist.

**Reconciled 2026-09-14: items 1, 2, 4, 5 and 8 moved into Batch H** and are no longer specified here —
PLAN.md's Phases 49–56 own them. What is left below is release mechanics, which genuinely belong at the
gate because they depend on the repo already being public or on a decision only the release forces.

0. **Go public from a fresh repo, not by flipping this one** — decided 2026-09-14 (DECISIONS), and it is
   **first**, because items 6 and 7 both need a public repo to exist. Create a new public repo under the
   name Phase 52 settles, seeded with **one initial commit of the Batch H end state**.
   `ArranPell/AchievementTrackerPlus` stays private and frozen as the working archive; the new repo
   becomes the working repo, so there are never two live ones. Three problems it closes at once: the
   Lady Elyssa correspondence in this repo's history (Phase 49), a history that is overwhelmingly
   Denrage's commits with `main` still tracking upstream — which contradicts the "different module, not
   a fork" position we actually hold — and the repo name. What it costs: granular public commit history,
   which in this project lives in `COMPLETED.md` and `DECISIONS.md` rather than in commit messages, both
   of which stay in the archive. Losing the `upstream` remote costs nothing: the documented way to send
   Denrage a PR already uses a separate `prfork` clone off his repo. **What ships in that initial commit
   is settled** — the trimmed set: `src/`, `LICENSE`, `.gitignore`, `README.md`, `CHANGELOG.md` (item 7)
   and `docs/{PLAN,ROADMAP,BACKLOG,UI-DESIGN,PATHING-PR}.md`. **Done 2026-09-15: `ArranPell/Quarry`,
   one commit, seeded exactly this way.** Refinement to "the new repo becomes the working repo" (DECISIONS
   2026-09-15): it can't be, because the never-ship docs must keep living somewhere versioned — so this
   private repo stays the working repo and the public one gets a fresh snapshot commit per release. **Not** `CLAUDE.md` or `.claude/`
   (ArranPell, 2026-09-14, reversing the Phase 51 plan to ship a scrubbed CLAUDE.md — both are untracked
   now, so a checkout of this repo already omits them), and not `DECISIONS.md`, `COMPLETED.md`,
   `handoff.md`, `PROJECT-HANDOFF.md`, `SESSION-*.md`, `docs/analysis/` or `docs/private/`. The manifest
   `url` changes to the new repo in the same commit (Phase 56 item 34).
   **The seed is mechanical, not a checklist:** `.gitattributes` marks every never-ship path
   `export-ignore`, and `git archive` honours it. From this repo, on `fork`:

   ```
   git archive --format=tar --prefix=quarry/ fork | tar -x -C <somewhere outside this repo>
   git archive fork | tar -t | grep -E 'CLAUDE|\.claude|DECISIONS|COMPLETED|handoff|PROJECT-HANDOFF|SESSION-|analysis|private|secrets'
   ```

   The second line must print nothing. Then, in the extracted folder: edit the manifest `url`, add
   `CHANGELOG.md` (item 7), `git init`, one commit, push to the new public repo. Copying the working
   folder instead of archiving would ship everything — don't.
1. ~~**Module rename → Batch H Phase 52.**~~ **Done 2026-09-14:** the module is **Quarry**, namespace
   `ArranPell.Quarry`, collision-checked against `blish-hud/bhud-pkgs` (1,105 manifests, no "quarr").
   The public repo takes the name at item 0; the manifest `url` changes then (Phase 56 item 34).
2. ~~**README + module description → Batch H Phase 55.**~~ **Done 2026-09-14** (Phases 51 and 55): the
   description carries the "Based on Denrage's Achievement Tracker (MIT)" line and link; `README.md` exists;
   `LICENSE` carries both notices — never drop his.
3. ~~**`HuntMode` default**~~ **Decided 2026-09-15: stays off** (ArranPell). A first-time installer with
   Pathing shouldn't have category state change under them (DECISIONS 2026-09-09); the README and the
   setting's own description say how to turn it on.
4. ~~**Divergence housekeeping → Batch H Phases 49, 50 and 53.**~~ **Done 2026-09-14.** `Gw2WikiDownloader`
   stays in-tree (it is the only route to regenerating the hosted files, and `Quarry.WikiData` is kept as
   a separate project because it references it — Phase 48 did *not* extend it; `DerivedSubpageGenerator`
   is standalone). `Persistance` → `Persistence` done in Phase 53.
5. ~~**Contingency hooks + log-level audit → Batch H Phase 54.**~~ **Done 2026-09-14** as Phase 56 items
   12–15 and 27: `Blish_HUD.Debug.Contingency` at the three denied-write / no-network sites, and the log
   down to 12 Info lines per start (GitHub issue #13).
6. **SSRD static hosting** of the data files — **deferred past 2.0.0 (ArranPell, 2026-09-15): the three
   files keep coming from Denrage's URLs**, which work today and whose failures Phase 56 made honest. Next
   step is a human one: ask Freesnöw on the Blish HUD Discord for an SSRD contributor account (the same
   account lists the module in the in-game repository), then push a `bhud-static/ArranPell.Quarry` branch
   and repoint the URLs in a 2.0.x. Original text: needs a contributor account, which needs the repo public,
   so it runs after item 0, with the **three** remaining `AchievementService` URLs repointed after.
   (Phase 48 removed the fourth: `subPages.json` is now embedded in the `.bhm`.) **Stays at the gate**,
   and Batch H Phase 51 deliberately leaves those three Denrage-named URLs alone for this reason. With
   only three small files left, "embed the rest too and stop phoning home" is a live alternative worth
   costing here rather than assuming SSRD.
7. ~~**Release build + `CHANGELOG.md`** keyed by manifest version.~~ **Done 2026-09-15: 2.0.0.**
   `CHANGELOG.md` at the repo root, keyed by the manifest version; `dotnet build -c Release` writes
   `src\Quarry\bin\Release\net4.7.2\Quarry.bhm` (2.2 MB, pdb included so Blish crash reports keep line
   numbers), dev-loaded before release; GitHub release `v2.0.0` on the public repo carries it.
8. ~~**Public-readiness code review → Batch H Phase 54**, with its findings as Phase 56.~~ **Done
   2026-09-14:** six-lens review, 69 confirmed findings, 41 fix items, all landed but the manifest `url`
   (item 0's). What remains at the gate is 0, 3, 6 and 7.

## Code review — two scoped reviews at the gates

Decided 2026-09-09 (DECISIONS): no single full review. The 06 Sept review was lens-scoped and its
findings cost a whole arc (Phases 11–14); a repo-wide pass at the wrong moment buys nits in code about
to change.

- **Hunter-code review — ✅ ran 2026-09-09.** Its four findings became **Phase 24** (trails indexed at
  (0,0,0) being the significant one; cache schema is now **5**). Scope was Phases 15–17 only, plus the two
  post-batch fixes. Lenses were: lifecycle / threading / exceptions (as 06 Sept) **plus data** — pack parse
  correctness, cache invalidation and the schema-bump path, the tagged-vs-untagged preference rule, the
  reflection bridge's failure modes when Pathing is absent or changes, and the 2 s accumulator's
  behaviour across window show/hide and map change. Findings become a short fix phase; RC3 waits for it.
- **Public-readiness review — now Batch H Phase 54** (was "at the publish gate"; moved 2026-09-14 so it
  runs over the tree as it will actually ship, after the cleanup rather than before it). Scope: whole
  repo. Brief: dead code and upstream remnants, log levels, anything embarrassing in a public tree, and
  **the Phases 18–23 code that landed after the 06 Sept review** and has never had the threading lens
  run over it — backlog item 18 (the eight off-main-thread control sites) gets decided here. Findings
  become Phase 56, the last pre-public phase. PLAN.md carries the full brief, including what a
  2026-09-14 pass already checked and found clean so the review doesn't re-derive it.

Carried from the 06 Sept review, not re-reviewed: item 18 (optional main-thread marshalling) and item 33
(sync `ReadAllText` on load — do nothing unless it hangs).

## Not on the roadmap

Everything in BACKLOG.md's **Closed** and **Explicitly out of scope**: meta-event timers, Wizard's Vault,
WvW/PvP, drawing our own markers, raising the tracked cap, regenerating wiki data as a product feature.
Also not planned: taking over or merging back into Denrage's module (DECISIONS 2026-09-08). Further
upstream PRs stay optional under the `upstream-candidate` rule in CLAUDE.md — the promised one is done.

## Releasing — the snapshot-per-release loop (from 2.0.0, 2026-09-15)

This private repo is the working repo. The public repo, `ArranPell/Quarry`, holds one commit per release,
each a `git archive` snapshot of this tree (so `.gitattributes`' `export-ignore` set never ships). Steps,
from this repo on `fork` with a clean tree:

1. Bump `version` in `src/Quarry/manifest.json`; add the release's section to `CHANGELOG.md`; commit.
2. `dotnet build src/Quarry/Quarry.csproj -c Release`; dev-load `bin\Release\net4.7.2\Quarry.bhm` once.
3. Snapshot into a clone of the public repo and push:

   ```
   git clone https://github.com/ArranPell/Quarry <scratch>\Quarry && cd <scratch>\Quarry
   git rm -rq . && git archive --format=tar <private-repo-commit> | tar -x
   git add -A && git commit -m "Quarry <version>" && git push
   ```

   (`git rm -rq .` first so files deleted since the last release disappear from the snapshot too.)
4. `gh release create v<version> src\Quarry\bin\Release\net4.7.2\Quarry.bhm --repo ArranPell/Quarry --title "Quarry <version>" --notes-file <the CHANGELOG section>`.
5. Once the SSRD account exists: submit the release there so the in-game module repository picks it up.

Never push this repo's branches to the public repo — that is the history item 0 exists to keep private.

