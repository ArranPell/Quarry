# Plan

> **Reading this in the public repo?** `docs/DECISIONS.md` (the decision log) and `docs/COMPLETED.md`
> (the completed-phase archive), cited throughout, live in the author's private working repo along with
> the other archive-only docs. The citations are left as-is: they point at a record that exists, just not
> here.

Phased. Each phase ends with a build ArranPell load-tests in Blish before the next starts. Update the
**Status** line of each phase as work lands; keep this file honest rather than aspirational.

Phases 0–17, 18–23, 24–30 and the upstream PR are done and confirmed — moved to `docs/COMPLETED.md` so
this file only carries what's active or not started. Search there for a phase number if you need the
history.

**RC1 (Phases 15–17) is done** — built, load-tested and confirmed 2026-09-09. **RC2 is done** — Batch C
(Phases 24–28, 30) confirmed 2026-09-10 and Phase 29 confirmed 2026-09-11. The promised upstream PR is
also done (Denrage PR #8). All of it is in `docs/COMPLETED.md`.

**RC3 — "the hunt loop made good" — is the current milestone**, designed 2026-09-11 (decisions in
`docs/DECISIONS.md` under that date).

- **Batch D = Phases 31–34 is done** — confirmed in-game 2026-09-11, moved to `docs/COMPLETED.md`. It
  made the bounded Here list the thing you act on rather than look at.
- **Batch F = Phases 38–45 is done** — confirmed in-game 2026-09-13, moved to `docs/COMPLETED.md`. Our
  own card grid, card, window body, the Inspector, the Target List, resize, and retiring the old card
  path. Brief: `docs/UI-DESIGN.md`; reasoning DECISIONS 2026-09-12/13; the four load-test rounds are in
  COMPLETED.md's Batch F entry.
- **Batch G = Phases 46–48 is done** — confirmed in-game 2026-09-14, moved to `docs/COMPLETED.md`. The
  data batch: stopped paying for `achievement_tables.json` at startup, put the wiki Notes column in the
  Inspector, and replaced `subPages.json`'s 70 MB runtime download with a small embedded derived file.
  Origin: the two analysis documents in `docs/analysis/`; reasoning in DECISIONS 2026-09-13; the
  load-test fix round is in COMPLETED.md's Batch G entry.
- **Batch E = Phase 35 is done** — confirmed in-game 2026-09-14, moved to `docs/COMPLETED.md`; was
  Phases 35–36. The Here strip lands in the Target List and the window's height follows what's actually
  visible. **RC3 closed here**, on its own exit criterion. **Phase 36 (compact row toggles) is retired**:
  Batch F's Phase 43 deleted or absorbed five of its six toggles, and made the window user-resizable,
  which was the real fix the "one height function" was reaching for. Phase 37 (contrast/tokens) was
  absorbed into Batch F as Phase 39. Reasoning for both: DECISIONS 2026-09-14.
- **Batch H = Phases 49–56 is done** — confirmed in-game by ArranPell 2026-09-14 (full load-test pass,
  including both destructive checks; no regressions), moved to `docs/COMPLETED.md`. The cleanup and
  public-readiness batch: repo purge, unused assets and csproj cruft, retiring the Denrage names from
  our own code, the module rename, the dead-code sweep, the public-readiness review, and the README. It
  absorbed publish-gate items 1, 2, 4, 5 and 8 from `docs/ROADMAP.md`. **This was the last batch before
  the publish gate.**
- **The publish gate was taken 2026-09-15**: `ArranPell/Quarry` is public (one snapshot commit), release
  `v2.0.0` carries `Quarry.bhm`, Hunt mode stays off by default, the data files keep coming from
  Denrage's URLs until an SSRD account exists. ROADMAP owns the gate and the new "Releasing" loop; nothing
  is active here until the first public feedback or a BACKLOG item is scheduled.

---

## Batch F — the UI redesign — done, confirmed in-game 2026-09-13

Moved to `docs/COMPLETED.md` in full (all eight phases, plus the four rounds of load-test fixes).
Search there for "Batch F" or a phase number 38–45 if you need the detail. Summary: our own card grid
and card, our own window body, the Inspector, the Target List, resize on both windows, and retiring the
old card path — confirmed by ArranPell after fixing the Target List's dead band and drop/open double-fire,
the Inspector's title/chip-colour/deselect/text-order/peek-gating issues, and adding drag-resize plus
navigation shortcuts (Target List ↔ Quarry) neither had before.

---

## Batch G — the data batch — done, confirmed in-game 2026-09-14

Moved to `docs/COMPLETED.md` in full (all three phases, plus the load-test fix round). Search there for
"Batch G" or a phase number 46–48 if you need the detail. Summary: stopped paying for
`achievement_tables.json` at startup, put the wiki Notes column in the Inspector, and replaced
`subPages.json`'s 70 MB runtime download with a small file embedded in the `.bhm` — confirmed by ArranPell
after fixing undecoded HTML entities, a leaking chat-link script, a missing Map-column fallback image
(a real regression against the old Item Details window), and turning wiki chat-link placeholders into
click-to-copy in-game codes and Inspector images into click-to-expand ones instead of both alt-tabbing
to a browser.

---

## Batch E — the Here strip — done, confirmed in-game 2026-09-14

Moved to `docs/COMPLETED.md` in full (Phase 35, the retired Phases 36 and 37, plus the load-test fix
round). Search there for "Batch E" or "Phase 35" if you need the detail. Summary: the Here strip in the
Target List (the top three candidates for the current map, click-to-Inspector, "+" to target, right-click
to hide), one height function derived from what's actually visible instead of a fixed three-way split
that reserved 18 px for an invisible summary line, and a 28 px top bar with 20 px icons and a tinted
Quarry shortcut — confirmed by ArranPell after enlarging the "+" glyph and fixing the strip sticking on a stale
"permissions not granted" state when its first fetch beat the subtoken update; the "flat window body reads
as blank" observation from the same test went to BACKLOG by ArranPell's call. **RC3 closed on it.**

## Backlog

Moved to `docs/BACKLOG.md` (2026-09-06) so ideas can pile up without cluttering this file. Rule: an idea
lives there until it's scheduled, at which point it becomes a phase here and is deleted from the backlog.
Rejected ideas go to that file's **Closed** section with a reason. Out-of-scope list lives there too.
