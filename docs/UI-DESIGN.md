# UI design brief — Batch F redesign

Written 2026-09-12 from ArranPell's screenshots (in-game Hero panel vs. our overview and Tracked windows),
the Blish HUD docs, and Blish's **v1.2.0 source** where the docs are silent. **Revised 2026-09-13**
after the mockup canvas (two rounds; the Cowork artifact "Achievement Tracker+ Redesign") — ArranPell chose
direction **B** (cards, bolder) for the Quarry window, the **minimal Target List** with an **Inspector**
pane for detail, and the naming in §8. This is the brief Phases 39–45 in `docs/PLAN.md` build from.
Everything marked *measured* or *verified* was; everything marked *proposed* is a taste call ArranPell can
veto at the batch load-test.

The complaint, in ArranPell's words: text feels grainy; too much wasted space; textures feel low-res; the
whole thing feels old. The reference is the in-game achievement panel — clearer, elegant, still clean.
The second-round brief, also his: push past "the same with tweaks" — make it visibly a *hunter*, not a
tracker. The answer that stuck: the card is about **where and how far**, not just progress.

---

## 1. What's actually causing each complaint

### 1.1 Grainy text — a platform limit we can only mitigate

*Verified against Blish v1.2.0 `ContentService.cs` and `GraphicsService.cs`.*

- Blish ships **Menomonia as pre-rasterised bitmap fonts** (`fonts/menomonia/menomonia-<size>-<style>`)
  at sizes 8, 11, 12, 14, 16, 18, 20, 22, 24, 32, 34, 36. Regular/Bold/Italic exist for 11–18; Bold also
  at 20, 22, 24, 36. `GameService.Content.GetFont(FontFace.Menomonia, FontSize.SizeNN, FontStyle.Bold)`
  is the accessor; `DefaultFont12/14/16/18/32` are just Regular shortcuts.
- The whole overlay is drawn through one scale transform:
  `UIScaleMultiplier = DPI ratio × interface-size ratio`, where the interface-size ratio is **Small 0.810,
  Normal 0.897, Large 1.000, Larger 1.103**. Text is crisp only at Large / 100 % DPI. At any other setting
  every glyph is rasterised at its nominal size and then resampled.
- **ArranPell runs Small at 100 % DPI** (confirmed 2026-09-12; also *measured*: the Tracked window is 350 px in
  code and 284 px in the screenshot — 350 × 0.81 = 283.5). So every module's text on his screen is shrunk
  19 % after rasterisation. The game's own panel rasterises for the chosen size, which is why it's crisp.
- **We cannot ship a font.** `ContentsManager.GetBitmapFont(ttfPath, size)` exists only on Blish's dev
  branch; in v1.2.0 and v1.3.0 the one-argument overload throws `NotImplementedException`. When a Blish
  release carries it, this brief's font choices should be revisited (BACKLOG has the note).
- Two of our own habits make it worse. (1) **No shadow.** The Hero panel's text has a 1 px dark shadow;
  ours has none, which is most of the "thin and grey" look — compare "Misty Leap" with "A Sampling of
  Snargle" at 4× zoom. (2) **Stroke on small text.** `AchievementButton` draws the progress text with
  `stroke: true`, which Blish implements as eight black copies around the glyphs; downscaled, that's a
  fuzzy halo (the green `3 / 4`).

**What we do:** step sizes up (14 → 16 for titles, 18 Bold for the progress numerals), shadow instead of
stroke, a floor of 14 with nothing smaller anywhere, and Bold where the game uses weight. Real,
visible improvement; *not* game-crisp, and no redesign can make it so on Blish 1.2.0.

### 1.2 Low-res textures — the window frame is being stretched

*Verified against `AchievementOverviewWindow.cs`, `AchievementTrackWindow.cs`, Blish `WindowBase2.cs`.*

- `WindowBase2.ConstructWindow` computes width/height ratios from the background texture and
  `PaintWindowBackground` draws the *whole* texture into `BackgroundDestinationBounds` — a plain
  stretch, no tiling, no 9-slice. The overview window is built on asset 156006's 900×640 region and then
  sized up to 1200×1100; in ArranPell's screenshot it's ~1.4× vertically and ~1.0× horizontally. The
  Tracked window does the same to a 350×600 `background.png`.
- The **title bar, exit button, emblem, corner and tab textures are drawn at native size** — they are
  *not* the problem, and they're what makes it look like a GW2 window. Keep them.
- Card icons: `DetailsButton` draws its icon at 64×64 from render-service icons that are 64×64 — fine —
  but the Hero panel's icons are bigger and cleaner because the game has its own high-res versions. Draw
  ours at 64 or smaller, never larger.

### 1.3 Wasted space — fixed heights and a button stack

- `DetailsButton`'s layout is fixed: 35 px bottom band plus the 112 px card height means ~40 % of every
  card is chrome. Two columns of 112 + 8 px gutter for 10 cards is 600 px before headers.
- The overview window opens at a clamped fraction of the screen regardless of content (old Phase 40's
  brief, now Phase 42).
- The Tracked window's full mode spends 150 px (five × 30 px buttons) before the first achievement,
  and its per-achievement icon column is 32 px wide for 16 px-worth of glyph.

### 1.4 "Feels old" — it's `DetailsButton`

Blish's 2019 control imitating the *old* in-game achievement row: vignette fill, crest, black
translucent background, fixed everything. Replacing it is the Phase 40 card control.

---

## 2. Tokens — `UiStyle`, one place (Phase 39)

Extends `GuidanceStyle` (`Models/Guidance.cs`) rather than starting a second palette; `GuidanceStyle`
becomes a nested/adjacent member of the same static class so every colour and font role has one home.

| Role | Value | Notes |
|---|---|---|
| `TitleFont` | Menomonia 16 Regular | card title, Track panel title, section headers |
| `NumeralFont` | Menomonia 18 Bold | progress `x / y` — the game gives the number the weight |
| `BodyFont` | Menomonia 14 Regular | Next line, meta, compact rows — **the floor; nothing below 14** |
| `HeaderFont` | Menomonia 18 Regular | the Here header line |
| `TextPrimary` | `Color.White` | with `ShowShadow = true`, `ShadowColor = Black * 0.8` |
| `TextSecondary` | `Color(200, 200, 200)` | section labels, meta, "Also on this map" |
| `TextMuted` | `Color(150, 150, 150)` | hidden-card dimming, disabled |
| `Accent` | `ContentService.Colors.ColonialWhite` | the GW2 cream used by window titles — headers, hover |
| `ProgressFill` | `Color(120, 170, 220)` | default bar; today's `DefaultBarColor` in the Track window |
| `NearDone` | `Color(212, 175, 55)` | unchanged from `NearDoneFillColor` — gold at 75 %+ |
| `Complete` | `Color(120, 200, 120) * 0.35` | unchanged intent from today's green tint |
| `CardBackground` | `Color.Black * 0.40` | flat; the Hero panel's rows are a dark translucent |
| `CardBackgroundHover` | `Color.Black * 0.28` | lighter on hover, like the game |
| `CardBorder` | `Color(238, 233, 217) * 0.12` | 1 px hairline, cream-tinted rather than white |
| `WindowBody` | `Color(12, 11, 9) * 0.84` | warm-black, see §4 |
| `Rank` | `Color(143, 138, 124)` | the rank numeral and "no route on this map" |
| `PipDone` | `TextPrimary` (`NearDone` gold at 75 %+) | objective pips, §3 |
| `PipTodo` | `Color(238, 233, 217) * 0.16` | |
| `ManualDone` | `Color(238, 233, 217)` outline + tick glyph | chip ticked by the player, §7 |
| `SectionFont` | Menomonia 14 Bold, `TextSecondary`, +1 letter-spacing | section labels; Menomonia has no small caps |
| `Gutter` | 8 px | between cards and rows (unchanged) |
| `CardPadding` | 8 px | inside the card |

Rules: shadow, never stroke, below 20 px. No `Color.Gray`/`Color.LightGray`/`Color.LightGreen`
literals left in UI code after this batch — every one becomes a token. Guidance colours stay exactly
what Phase 26 settled (glyph + word + colour, trail-only neutral). Colour is never the only carrier of
a state: the tier is also a glyph and a word, a manual tick is also a hollow shape and a glyph.

---

## 3. The card — `AchievementCard` (Phase 40) — direction B, chosen 2026-09-13

Modelled on the Hero panel's "Nearly Completed" row, then pushed: rank, place and distance, and the
achievement's real shape (its bits) are on the card. Two columns.

```
┌──────────────────────────────────────────────────────────┐  height 92, 2 px tier-colour top edge
│ ┌──────┐  2  Lost Lore                                  x │  title row: rank · title (ellipsised) · hide
│ │ icon │     Arborstone · 172 m                           │  place row: sector · bold distance  (or
│ │  56  │                                                  │  "no route on this map", muted)
│ └──────┘  8 / 11  ▮▮▮▮▮▮▮▮▯▯▯          ~ Route   👁       │  numerals · pips · badge · eye
└──────────────────────────────────────────────────────────┘
   8 px pad · icon 56 (64 source, never upscaled) · 10 px · text column · 10 px pad
```

- **Height 92** (was 112): three 20-ish px lines plus 8 px padding. One height, fixed;
  `CardLayout.CardHeight` follows it. *(proposed; 88 if the third row has slack)*
- **Tier edge:** a 2 px top border in the guidance tier's colour (`GuidanceStyle`), hairline
  `CardBorder` otherwise. The word badge stays too — colour is never the only carrier.
- **Rank:** the card's position in the ranked Here list, `Rank` colour, `BodyFont`, left of the title.
  It says "this is a capped, ordered list" without a sentence. Not shown in the Achievements category
  view (that isn't ranked).
- **Title:** `TitleFont`, `TextPrimary`, **ellipsised to the real column width** measured with the real
  font (`StringUtils.TrimNameToWidth` against the actual `BitmapFont`). One line. Retires the Phase 33d
  two-line trim. Nothing else shares the title row except the hide glyph.
- **Place row:** `BodyFont`, `TextSecondary`: the sector name from the nearest remaining objective
  (Phase 29's sector data) and its distance in **Bold `TextPrimary`**, e.g. `Arborstone · 172 m`.
  When nothing can place it: `no route on this map` in `Rank` colour. This is the line that makes it
  a hunter card; it costs one `INearestObjectiveService` lookup per candidate (ten, cached) — measure
  the first paint on a 10-card map before it becomes the default, BACKLOG has the note. *Bearing arrow
  is NOT on the card* — it's on the Target List rows (§6), where you're actually walking.
- **Numerals:** `NumeralFont`, `x / y`; `NearDone` colour at 75 %+, else `TextPrimary`. "Complete" in
  `Complete`-green text instead of numerals when done.
- **Pips:** one per bit, 4 px tall, 2 px gap, filling the space between the numerals and the badge;
  `PipDone`/`PipTodo`, `NearDone` gold at 75 %+. Replaces the vignette fill and the bar. Above ~40 bits
  the pips are under 3 px each — fall back to a plain 4 px bar at that point (Spiritual Childcare's 38
  is the known worst case in ArranPell's list and still renders as pips).
- **Guidance badge:** `BodyFont`, tier colour, right of the pips; same tooltip and peek click as today
  (`GuidanceInfo.Describe()`, `HuntService.Peek`, pack-backed tiers only).
- **Hide affordance (Here only):** `x`/`+` glyph at the top-right of the title row, 16 px, same
  `ContextMenuStrip` (Not today / Not interested / Unhide). Dimmed card (opacity 0.55) when hidden.
  Unchanged behaviour from Phase 31.
- **Target toggle:** the eye, 22 px, far right of the third row, same `track_enabled/disabled.png`;
  `Checked` synced on `AchievementUntracked`. Card click = target/drop with the 15-cap notification.
- **Tooltip:** remaining items (`AchievementProgress.RemainingText`) on the whole card, as today.
- **Background:** `CardBackground` fill, hover → `CardBackgroundHover`; complete → `Complete` tint. One
  `Control` that paints itself and owns three child controls at most (badge label, hide label, eye).

**Must keep** (verified against the then-current `AchievementListItem.cs` on 2026-09-12 — it became `AchievementCard.cs` in Phase 40; unchanged from PLAN's list):
track/untrack on click + 15-cap notification; `AchievementUntracked` sync; badge with tier colour,
tooltip and peek click; near-done gold at 75 %+; remaining-items tooltip; complete state; category
icon; Phase 31 hide affordance + context menu with its `Dispose` in `Unload` (the `ContextMenuStrip`
reparents itself to `SpriteScreen`).

**Columns:** `CardLayout.MinCardWidth` 360 → **380**: two columns from ~770 px usable, which the 900 px
minimum window always has; one column below that if Phase 42's resize ever allows it. Three columns
was direction A's trade and lost with it — the place row needs the width.

## 4. The window body — `WindowBodyPainter` (Phase 41)

**Own frame, but only the part that's broken.** `TabbedWindow2` stays: tabs, sidebar, drag, close,
emblem, title text, `SavesPosition` are all native-size textures and they're the GW2 look. What goes is
the stretched background.

*Verified against Blish v1.2.0:* `WindowBase2.PaintBeforeChildren` calls, in order, the private
`PaintWindowBackground` (the stretch), `PaintSideBar`, `PaintTitleBar`; `TabbedWindow2` does **not**
override `PaintBeforeChildren` (only `PaintAfterChildren`, for the tabs). `ContentService.Textures.
TransparentPixel` exists. So:

1. Build the window with a **transparent background texture** sized to the frame we want the padding
   maths done for (a bundled `ref/window_blank.png` at e.g. 1000×760 with the content rect we choose;
   `ConstructWindow` only reads its dimensions). The stretched draw is now invisible.
2. Override `PaintBeforeChildren`: paint the body first, then `base.PaintBeforeChildren(...)` so the
   native sidebar fade and title bar land on top.
3. The body, in a shared `UserInterface/WindowBodyPainter` used by **both** windows:
   - `WindowBody` flat fill over `bounds` below the title bar (the Hero panel's content area is a flat
     dark translucent; nothing is textured there either);
   - a 1 px `CardBorder` line around the body and a 1 px lighter line under the title bar;
   - Blish's own `fade-down-46` (`GameService.Content.GetTexture("fade-down-46")`) as a 46 px top
     gradient under the title bar, which is exactly what the sidebar already uses — so the transition
     from native title bar to our body reads as one piece;
   - the existing `605025` left-side accent (already in `ref/`) at native size along the content's left
     edge on the overview window only, so it still says "GW2 panel" rather than "flat box".
   Nothing in the body scales with the window; it fills. That's the whole fix.
4. `AchievementTrackWindow` (`WindowBase2`) does the same with its `background.png` replaced by the
   blank, and `ApplyWindowFrame`'s comment about `ConstructWindow` painting at native 350×600 becomes
   moot — but keep its "set `Size`, don't re-`ConstructWindow`" rule, that part still holds.

Not doing: a 9-slice from game window-edge assets. The asset ids aren't verified, the Hero panel doesn't
visibly use ornate edges on its content area, and a flat body is what ArranPell's own BACKLOG note asked for.

---

## 5. Size to content and live resize (Phase 42 — was Phase 40)

Unchanged brief: `CardGrid.ContentHeight` sizes the overview window to its content up to the screen
clamp; `CanResize = true` with the size persisted in `Storage`. Two notes from the redesign: with 92 px
cards in two columns, ten cards are 5 rows ≈ 500 px — still under the 640 px body, so the "short window
on a short list" case will finally be visible on a two- or four-card map; and the BACKLOG resize mystery (children not reflowing on horizontal drag) only ever
bit `FlowPanel` children — `CardGrid` re-lays out in `RecalculateLayout`, which `OnResized` triggers.
If horizontal drag *still* doesn't reflow the grid, that's the empirical bisect BACKLOG asks for, and
the phase ships without `CanResize` rather than with a handle that lies.

---

## 6. The Target List (Phase 43) — the compact window is the window

ArranPell's call 2026-09-13, superseding the 09-12 "keep both modes": the **minimal Target List plus the
Inspector (§7)** replaces both modes of the Tracked window. Full mode's per-achievement panels were the
only place to read notes and tick objectives without a pop-out; the Inspector is now that place, so
the panels, the compact/full toggle, "Collapse All" and "Close all Subpages" all go. Every *feature*
survives — it just lives in one of two places.

```
┌ Target List ──────────────────────────────────── ✕ ┐  300 wide (Storage.TrackWindowCompactWidth), native title bar
│ NEAREST  ↗ Lost Lore record            172 m  ⧉  ⋯ │  22 px strip, then the rows
│ Lost Lore                          8 / 11   👁     │  40 px row, line 1: name · x/y · drop
│   ↗ Lost Lore record                       172 m   │           line 2: bearing · nearest objective · distance
│ Character Growth                  11 / 16   👁     │
│   ↘ Character Growth                       150 m   │
│ …                                                  │
│               Done this session: 2                 │  18 px, TextMuted
└────────────────────────────────────────────────────┘
```

- **Body:** §4's painter, `background.png` dropped. Title bar, emblem (the eye), close: native.
- **Rows, 40 px** (were 24): line one is the name (`BodyFont`, `TextPrimary`, ellipsised) with
  `x / y` right-aligned (`NearDone` gold at 75 %+) and the drop (untrack) eye at 14 px; line two is
  the **nearest remaining objective** (`TextSecondary`, truncated, full text plus the remaining list in
  the tooltip), a **relative bearing arrow** and the **distance** in Bold `TextPrimary`. Rows without a
  placeable objective show line two as `no route on this map` in `Rank` colour and no arrow.
  `ApplyNearestToRow` already exists in `AchievementTrackWindow.cs` — this is the row shape catching up
  with the plumbing. The 3 px bar is gone; progress is the numerals.
- **Bearing arrow:** the objective's direction relative to the way the character faces, snapped to
  eight directions, drawn as one small stroked arrow rotated (never emoji). Needs Mumble's avatar-facing
  vector — verify `GameService.Gw2Mumble.PlayerCharacter.Forward` (or equivalent) exists in Blish 1.2.0
  before promising it; **ship without the arrow if it doesn't**, the distance alone still works.
- **Nearest strip:** one 22 px line at the top: `NEAREST` in `SectionFont`, then the single closest
  remaining objective across *all* targets with its bearing, name and distance, and the waypoint-copy
  icon that copies the waypoint nearest *that* objective (`NearestObjectiveService.NearestWaypoint`).
  Refreshes on the same 2 s accumulator the Next lines use. Hidden when nothing is placeable.
- **Menu (`⋯`)** at the right of the Nearest strip, a `ContextMenuStrip`: *Open Quarry* · *Reload from
  file* · *Save now*. That's what's left of the five buttons once panels and subpages are gone.
- **Row click:** left-click a row → the Inspector shows that achievement (§7). Right-click a row → the
  same menu the Here card has where it applies (Drop, Show route in Pathing). The old "detach into own
  window" is the Inspector's pin.
- **Height:** rows × 40 + strip + summary, from the row count, capped at the screen clamp — the
  `GetDefaultCompactHeight` shape with the new constants. Phase 36's toggle-derived height function
  replaces it later; nothing here pre-empts that. Width stays `Storage.TrackWindowCompactWidth`.
- **Storage:** `TrackWindowCompact` becomes dead; leave the field, stop reading it (Phase 44 deletes).
- **Session summary** and empty-state label: `BodyFont`, `TextMuted`. Empty state: "No targets yet —
  open Quarry and target something."

---

## 7. The Inspector (Phase 45) — one detail pane, not N pop-outs

Replaces `AchievementDetailsWindow` **and** `SubPageInformationWindow` as the default detail surface.
One window, `SavesPosition`, ~320 px wide, content height up to a clamp then scrolling. It shows
whatever was clicked last — a whole achievement or one objective — and a new click *replaces* it.
"Close all Subpages" disappears because nothing accumulates. Why not inline expansion: a 300 px list
can't host a wiki image or three lines of notes without breaking the compact promise.

**Achievement view** (a Target List row click, or the achievement name in the breadcrumb):
achievement name (`NumeralFont` size, i.e. 18 Bold), the **chips row**, the description and
requirements (today's top-level text, through `FormattedLabel` with `BodyFont` as its base font), the
Next line with bearing/distance/waypoint copy, and the actions: *Show route in Pathing* (peek,
pack-backed tiers only) · *Copy waypoint* · *Wiki*.

**Objective view** (a chip click): breadcrumb `Lost Lore · objective 6 of 11` in `SectionFont`, the
objective name (18 Bold), the chips row with the selected chip ringed, the wiki image if the subpage
has one (`ExternalImageService`, scaled to width, never upscaled), the notes, the **place line**
(sector · bearing · distance · waypoint-copy icon), the **Ticked by you** toggle with its API note, and
the same three actions.

**Chips** — the objective tiles, redrawn. Three states, never colour alone (Phase 26 rule):
*confirmed by the API* = solid `Complete`-green fill, no glyph, immutable; *ticked by you* =
`ManualDone` hollow cream outline with a tick glyph, revertible; *not done* = `PipTodo` fill,
`TextSecondary` number. 24 px tall, 3 px gap, wrapping. Row-number text stays (the Next line and the
remaining-objective prefixes refer to it). The numerals' tooltip says `N confirmed · M ticked by you`.
**Reconcile:** when the API later confirms a bit that was ticked manually, drop the manual flag so the
chip goes solid — that flip is the visible "the game caught up" moment, which today's design can't
show. **Before building:** check whether `Storage`'s manual completion is per-bit or per-achievement
(CLAUDE.md names `ManualCompletedAchievements`; Phase 30 added manual-tick completion). If it is
per-achievement only, a per-bit manual set in `Storage` + `AchievementProgress` lands first.

**Navigation — the chips row is the navigation, and it shows in both views.** Click a chip →
objective view; click the *selected* chip again, or the achievement name in the breadcrumb → back to
the achievement view; click another chip → the selection moves directly. A Target List row click
always lands on the achievement view, never a remembered objective. Selected chip = 2 px `NearDone`
gold ring; that ring is the only "you are here" — the breadcrumb stays quiet.

**Click rules (these supersede any earlier "click to tick"):** left-click = inspect, always; ticking
is deliberate — the **Ticked by you** toggle in the objective view, or **right-click a chip → Mark
done / Unmark**; hover a chip = its name only.

**Pin:** a pin in the title bar (native `WindowBase2` chrome; the pin is a 16 px glyph left of the
close button). Pinned, the Inspector freezes its content and the *next* click opens a fresh Inspector —
today's "detach into own window", kept as the exception rather than the default. Pinned copies close
with their own ✕; `InspectorWindowManager` keeps a `pinnedWindows` list for exactly this (the old `AchievementDetailsWindowManager` was deleted in Phase 53).

**Reuse:** the two windows this replaces already render everything the Inspector needs
(`FormattedLabelHtmlService`, `ExternalImageService`, the `SubPageInformation` models, the objective
control factories). The Inspector is one control with two shapes over those, not new rendering.

---

## 8. Naming (Phase 41 for window copy; manifest rename stays a publish-gate item)

- **Module:** **Quarry** — ArranPell to confirm; check blishhud.com/modules for a collision before the
  manifest changes. One word, the hunting sense is exact, and it doesn't say "achievement", which is
  the point of the rename (ROADMAP publish gate item 1). Rejected: *Achievement Hunter* (an existing
  brand), *Pathfinder* (collides with Pathing), *Compass*. Alternatives if Quarry doesn't sit right:
  *Hunter's Mark*, *Trailhead*, *Nearly There*.
- **Windows:** the overview window is titled **Quarry**; the tracked window is **Target List**; the
  detail pane is **Inspector**.
- **Tabs:** *Here* (keep — it's the product's own word) and *All* for the category tree.
- **Verbs:** the header button is **Target these (N)** / *Target list full*; the eye's tooltips are
  *Target this* / *Drop this*; the Here card's menu keeps *Not today* / *Not interested* / *Unhide*.
- Manifest `name`, namespace and data directory do **not** change in Batch F — that's the publish-gate
  rename, expensive after publishing and cheap before, but a separate, deliberate commit.

---

## 9. What to look at during the load-test

Test at **Small / 100 % DPI** (ArranPell's setting) over a bright map (Seitung harbour at noon) and a dark one.
The bar for "done" is not "crisp as the game" — it's: every label readable at arm's length, no fuzzy
halos, no stretched frame, no card taller than its content, the Hero panel side-by-side no longer
makes ours look like a different decade — and, new this round, you can tell from the Target List
alone what to walk toward and how far it is, without opening anything.

Reference to study, not copy: *Regions of Tyria* (ArranPell's example, BACKLOG 2026-09-09) for text that
sits inside the game's visual language.
