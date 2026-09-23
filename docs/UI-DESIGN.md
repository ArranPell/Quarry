# UI design brief — Batch F redesign

Written 2026-09-12 from my screenshots (the in-game Hero panel next to our overview and Tracked windows),
the Blish HUD docs, and Blish's v1.3.0 source where the docs are silent. Revised 2026-09-13 after two
rounds of mockups. I chose direction B (cards, bolder) for the Quarry window, the minimal Target List with
an Inspector pane for detail, and the naming in §8. This is the brief Phases 38–45 were built from.

Status: built as Batch F and confirmed in-game 2026-09-13 (phase records in `docs/COMPLETED.md`). It's
kept as the design rationale, not as a to-do list. Everything marked *measured* or *verified* was.
Everything marked *proposed* was a matter of taste that I could veto at the batch load-test. Pointers
to BACKLOG, PLAN and later phases describe the plan as it stood on 2026-09-13. Some of those items have
since moved, closed or been retired (Phase 36, for one), and the bearing arrow in §6 wasn't built.

The complaint, in my words: text feels grainy, there's too much wasted space, textures feel low-res, and
the whole thing feels old. The reference is the in-game achievement panel, which is clearer, elegant and
still clean. The second-round brief, also mine: go further than "the same with tweaks", and make it
visibly a hunter rather than a tracker. The design that came out of it puts where and how far on the card,
alongside progress.

---

## 1. What causes each complaint

### 1.1 Grainy text — a platform limit we can only soften

*Verified against Blish v1.3.0 `ContentService.cs` and `GraphicsService.cs`.*

- Blish ships Menomonia as pre-rasterised bitmap fonts (`fonts/menomonia/menomonia-<size>-<style>`) at
  sizes 8, 11, 12, 14, 16, 18, 20, 22, 24, 32, 34, 36. Regular, Bold and Italic exist for 11–18. Bold
  also exists at 20, 22, 24 and 36. The accessor is
  `GameService.Content.GetFont(FontFace.Menomonia, FontSize.SizeNN, FontStyle.Bold)`, and
  `DefaultFont12/14/16/18/32` are just Regular shortcuts.
- The whole overlay is drawn through one scale transform:
  `UIScaleMultiplier = DPI ratio × interface-size ratio`. The interface-size ratio is Small 0.810, Normal
  0.897, Large 1.000 and Larger 1.103. Text is crisp only at Large / 100 % DPI. At any other setting,
  every glyph is rasterised at its nominal size and then resampled.
- I run Small at 100 % DPI (confirmed 2026-09-12, and *measured*: the Tracked window is 350 px in code and
  284 px in the screenshot, and 350 × 0.81 = 283.5). So every module's text on my screen is shrunk 19 %
  after rasterisation. The game's own panel rasterises for the chosen size, which is why it's crisp.
- We can't ship a font. `ContentsManager.GetBitmapFont(ttfPath, size)` exists only on Blish's dev
  branch. In v1.2.0 and v1.3.0 the one-argument overload throws `NotImplementedException`. When a Blish
  release carries it, this brief's font choices should be revisited (BACKLOG has the note).
- Two of our own habits make it worse:
  1. No shadow. The Hero panel's text has a 1 px dark shadow and ours has none, which accounts for most
     of the "thin and grey" look. Compare "Misty Leap" with "A Sampling of Snargle" at 4× zoom.
  2. Stroke on small text. `AchievementButton` draws the progress text with `stroke: true`, which Blish
     implements as eight black copies around the glyphs. Downscaled, that's a fuzzy halo (the green
     `3 / 4`).

What we do: step sizes up (14 → 16 for titles, 18 Bold for the progress numerals), use shadow instead of
stroke, set a floor of 14 with nothing smaller anywhere, and use Bold where the game uses weight. That's a
visible improvement, but not game-crisp. No redesign can make it game-crisp on Blish 1.3.0.

### 1.2 Low-res textures — the window frame is being stretched

*Verified against `AchievementOverviewWindow.cs`, `AchievementTrackWindow.cs`, Blish `WindowBase2.cs`.*

- `WindowBase2.ConstructWindow` computes width and height ratios from the background texture, and
  `PaintWindowBackground` draws the whole texture into `BackgroundDestinationBounds`. It's a plain
  stretch, with no tiling and no 9-slice. The overview window is built on asset 156006's 900×640 region
  and then sized up to 1200×1100. In my screenshot it's ~1.4× vertically and ~1.0× horizontally. The
  Tracked window does the same to a 350×600 `background.png`.
- The title bar, exit button, emblem, corner and tab textures are drawn at native size. They aren't the
  problem, and they're what makes it look like a GW2 window. Keep them.
- Card icons: `DetailsButton` draws its icon at 64×64 from render-service icons that are 64×64, which is
  fine. The Hero panel's icons are bigger and cleaner because the game has its own high-res versions.
  Draw ours at 64 or smaller, never larger.

### 1.3 Wasted space — fixed heights and a button stack

- `DetailsButton`'s layout is fixed. A 35 px bottom band plus the 112 px card height means ~40 % of every
  card is chrome. Two columns of 112 + 8 px gutter for 10 cards is 600 px before headers.
- The overview window opens at a clamped fraction of the screen regardless of content (old Phase 40's
  brief, now Phase 42).
- The Tracked window's full mode spends 150 px (five × 30 px buttons) before the first achievement, and
  its per-achievement icon column is 32 px wide for 16 px worth of glyph.

### 1.4 "Feels old" — it's `DetailsButton`

It's Blish's 2019 control, imitating the old in-game achievement row: vignette fill, crest, black
translucent background, everything fixed. The Phase 40 card control replaces it.

---

## 2. Tokens — `UiStyle`, one place (Phase 39)

This extends `GuidanceStyle` (`Models/Guidance.cs`) rather than starting a second palette. `GuidanceStyle`
becomes a nested or adjacent member of the same static class, so every colour and font role has one home.

| Role | Value | Notes |
|---|---|---|
| `TitleFont` | Menomonia 16 Regular | card title, Track panel title, section headers |
| `NumeralFont` | Menomonia 18 Bold | progress `x / y`; the game gives the number the weight |
| `BodyFont` | Menomonia 14 Regular | Next line, meta, compact rows; the floor: nothing below 14 |
| `HeaderFont` | Menomonia 18 Regular | the Here header line |
| `TextPrimary` | `Color.White` | with `ShowShadow = true`, `ShadowColor = Black * 0.8` |
| `TextSecondary` | `Color(200, 200, 200)` | section labels, meta, "Also on this map" |
| `TextMuted` | `Color(150, 150, 150)` | hidden-card dimming, disabled |
| `Accent` | `ContentService.Colors.ColonialWhite` | the GW2 cream used by window titles; headers, hover |
| `ProgressFill` | `Color(120, 170, 220)` | default bar; today's `DefaultBarColor` in the Track window |
| `NearDone` | `Color(212, 175, 55)` | unchanged from `NearDoneFillColor`; gold at 75 %+ |
| `Complete` | `Color(120, 200, 120) * 0.35` | same intent as today's green tint |
| `CardBackground` | `Color.Black * 0.40` | flat; the Hero panel's rows are a dark translucent |
| `CardBackgroundHover` | `Color.Black * 0.28` | lighter on hover, like the game |
| `CardBorder` | `Color(238, 233, 217) * 0.12` | 1 px hairline, cream-tinted rather than white |
| `WindowBody` | `Color(12, 11, 9) * 0.84` | warm black, see §4 |
| `Rank` | `Color(143, 138, 124)` | the rank numeral and "no route on this map" |
| `PipDone` | `TextPrimary` (`NearDone` gold at 75 %+) | objective pips, §3 |
| `PipTodo` | `Color(238, 233, 217) * 0.16` | |
| `ManualDone` | `Color(238, 233, 217)` outline + tick glyph | chip ticked by the player, §7 |
| `SectionFont` | Menomonia 14 Bold, `TextSecondary`, +1 letter-spacing | section labels; Menomonia has no small caps |
| `Gutter` | 8 px | between cards and rows (unchanged) |
| `CardPadding` | 8 px | inside the card |

Rules:

- Shadow, never stroke, below 20 px.
- No `Color.Gray`/`Color.LightGray`/`Color.LightGreen` literals left in UI code after this batch. Every
  one becomes a token.
- Guidance colours stay as Phase 26 settled them (glyph, word and colour; trail-only is neutral).
- Colour is never the only carrier of a state. The tier is also a glyph and a word, and a manual tick is
  also a hollow shape and a glyph.

---

## 3. The card — `AchievementCard` (Phase 40) — direction B, chosen 2026-09-13

Modelled on the Hero panel's "Nearly Completed" row, then taken further: rank, place and distance, and the
achievement's shape (its bits) are all on the card. Two columns.

```
┌──────────────────────────────────────────────────────────┐  height 92, 2 px tier-colour top edge
│ ┌──────┐  2  Lost Lore                                  x │  title row: rank · title (ellipsised) · hide
│ │ icon │     Arborstone · 172 m                           │  place row: sector · bold distance  (or
│ │  56  │                                                  │  "no route on this map", muted)
│ └──────┘  8 / 11  ▮▮▮▮▮▮▮▮▯▯▯          ~ Route   👁       │  numerals · pips · badge · eye
└──────────────────────────────────────────────────────────┘
   8 px pad · icon 56 (64 source, never upscaled) · 10 px · text column · 10 px pad
```

- **Height 92** (was 112): three lines of about 20 px plus 8 px padding. One fixed height, and
  `CardLayout.CardHeight` follows it. *(Proposed; 88 if the third row has slack.)*
- **Tier edge:** a 2 px top border in the guidance tier's colour (`GuidanceStyle`), and the hairline
  `CardBorder` otherwise. The word badge stays too, so colour is never the only carrier.
- **Rank:** the card's position in the ranked Here list, in `Rank` colour and `BodyFont`, left of the
  title. It says "this is a capped, ordered list" without a sentence. It isn't shown in the Achievements
  category view, which isn't ranked.
- **Title:** `TitleFont`, `TextPrimary`, ellipsised to the column width as measured with the font in use
  (`StringUtils.TrimNameToWidth` against the actual `BitmapFont`). One line. This retires the Phase 33d
  two-line trim. Only the hide glyph shares the title row.
- **Place row:** `BodyFont`, `TextSecondary`. It shows the sector name from the nearest remaining
  objective (Phase 29's sector data) and its distance in Bold `TextPrimary`, for example
  `Arborstone · 172 m`. When nothing can place the achievement, it shows `no route on this map` in `Rank`
  colour. This is the line that makes it a hunter card. It costs one `INearestObjectiveService` lookup per
  candidate (ten, cached). Measure the first paint on a 10-card map before it becomes the default; BACKLOG
  has the note. The bearing arrow is not on the card. It's on the Target List rows (§6), where you're
  walking.
- **Numerals:** `NumeralFont`, `x / y`, in `NearDone` colour at 75 %+ and `TextPrimary` otherwise. When
  the achievement is done, "Complete" in `Complete`-green text replaces the numerals.
- **Pips:** one per bit, 4 px tall with a 2 px gap, filling the space between the numerals and the badge.
  They use `PipDone`/`PipTodo`, with `NearDone` gold at 75 %+. They replace the vignette fill and the bar.
  Above ~40 bits the pips are under 3 px each, so fall back to a plain 4 px bar at that point. Spiritual
  Childcare's 38 is the known worst case in my list and still renders as pips.
- **Guidance badge:** `BodyFont` in the tier colour, right of the pips, with the same tooltip and peek
  click as today (`GuidanceInfo.Describe()`, `HuntService.Peek`, pack-backed tiers only).
- **Hide affordance (Here only):** an `x`/`+` glyph at the top right of the title row, 16 px, with the same
  `ContextMenuStrip` (Not today / Not interested / Unhide). A hidden card is dimmed to opacity 0.55. The
  behaviour is unchanged from Phase 31.
- **Target toggle:** the eye, 22 px, far right of the third row, with the same
  `track_enabled/disabled.png`, and `Checked` synced on `AchievementUntracked`. Clicking the card targets
  or drops it, with the 15-cap notification.
- **Tooltip:** the remaining items (`AchievementProgress.RemainingText`) on the whole card, as today.
- **Background:** `CardBackground` fill, `CardBackgroundHover` on hover, and the `Complete` tint when
  complete. It's one `Control` that paints itself and owns at most three child controls (badge label,
  hide label, eye).

Must keep (verified 2026-09-12 against the then-current `AchievementListItem.cs`, which became
`AchievementCard.cs` in Phase 40; unchanged from PLAN's list): track/untrack on click and the 15-cap
notification; `AchievementUntracked` sync; the badge with tier colour, tooltip and peek click; near-done
gold at 75 %+; the remaining-items tooltip; the complete state; the category icon; and the Phase 31 hide
affordance and context menu, with its `Dispose` in `Unload` (the `ContextMenuStrip` reparents itself to
`SpriteScreen`).

Columns: `CardLayout.MinCardWidth` goes from 360 to 380. That gives two columns from ~770 px usable, which
the 900 px minimum window always has, and one column below that if Phase 42's resize ever allows it.
Three columns was direction A's trade, and it lost along with A. The place row needs the width.

## 4. The window body — `WindowBodyPainter` (Phase 41)

We draw our own frame, but only the part that's broken. `TabbedWindow2` stays: tabs, sidebar, drag,
close, emblem, title text and `SavesPosition` are all native-size textures, and they're the GW2 look. The
stretched background goes.

*Verified against Blish v1.3.0:* `WindowBase2.PaintBeforeChildren` calls, in order, the private
`PaintWindowBackground` (the stretch), `PaintSideBar` and `PaintTitleBar`. `TabbedWindow2` doesn't
override `PaintBeforeChildren`, only `PaintAfterChildren`, for the tabs. `ContentService.Textures.
TransparentPixel` exists. So:

1. Build the window with a transparent background texture, sized to the frame we want the padding maths
   done for: a bundled `ref/window_blank.png` at, say, 1000×760 with the content rect we choose.
   `ConstructWindow` only reads its dimensions, so the stretched draw is now invisible.
2. Override `PaintBeforeChildren`. Paint the body first, then call `base.PaintBeforeChildren(...)` so the
   native sidebar fade and title bar land on top.
3. Paint the body in a shared `UserInterface/WindowBodyPainter` that both windows use:
   - a `WindowBody` flat fill over `bounds` below the title bar (the Hero panel's content area is a flat
     dark translucent, with no texture there either);
   - a 1 px `CardBorder` line around the body, and a 1 px lighter line under the title bar;
   - Blish's own `fade-down-46` (`GameService.Content.GetTexture("fade-down-46")`) as a 46 px gradient
     under the title bar. The sidebar already uses it, so the move from the native title bar to our body
     reads as one piece;
   - the existing `605025` left-side accent (already in `ref/`) at native size along the content's left
     edge, on the overview window only, so it still reads as a GW2 panel rather than a flat box.

   Nothing in the body scales with the window; it fills. That's the whole fix.
4. `AchievementTrackWindow` (`WindowBase2`) does the same, with its `background.png` replaced by the
   blank. `ApplyWindowFrame`'s comment about `ConstructWindow` painting at native 350×600 no longer
   applies, but keep its rule to set `Size` and not re-run `ConstructWindow`. That part still holds.

Not doing: a 9-slice from game window-edge assets. The asset ids aren't verified, the Hero panel doesn't
visibly use ornate edges on its content area, and a flat body is what my own BACKLOG note asked for.

---

## 5. Size to content and live resize (Phase 42 — was Phase 40)

The brief is unchanged: `CardGrid.ContentHeight` sizes the overview window to its content, up to the
screen clamp, and `CanResize = true` with the size persisted in `Storage`. Two notes from the redesign:

- With 92 px cards in two columns, ten cards are 5 rows, ≈ 500 px. That's still under the 640 px body, so
  a short window on a short list will finally be visible on a two- or four-card map.
- The BACKLOG resize mystery (children not reflowing on horizontal drag) only ever affected `FlowPanel`
  children. `CardGrid` re-lays out in `RecalculateLayout`, which `OnResized` triggers. If horizontal drag
  still doesn't reflow the grid, run the empirical bisect BACKLOG asks for, and ship the phase without
  `CanResize` rather than with a handle that doesn't work.

---

## 6. The Target List (Phase 43) — the compact window is the window

I decided on 2026-09-13, replacing the 09-12 plan to keep both modes: the minimal Target List plus the
Inspector (§7) replaces both modes of the Tracked window. Full mode's per-achievement panels were the only
place to read notes and tick objectives without a pop-out. The Inspector is now that place, so the panels,
the compact/full toggle, "Collapse All" and "Close all Subpages" all go. Every feature survives in one of
the two places.

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

- **Body:** §4's painter, with `background.png` dropped. The title bar, emblem (the eye) and close button
  are native.
- **Rows, 40 px** (were 24). Line one is the name (`BodyFont`, `TextPrimary`, ellipsised), with `x / y`
  right-aligned (`NearDone` gold at 75 %+) and the drop (untrack) eye at 14 px. Line two is the nearest
  remaining objective (`TextSecondary`, truncated, with the full text and the remaining list in the
  tooltip), a relative bearing arrow, and the distance in Bold `TextPrimary`. A row with no placeable
  objective shows `no route on this map` in `Rank` colour as line two, with no arrow.
  `ApplyNearestToRow` already exists in `AchievementTrackWindow.cs`, so the row shape is catching up with
  the plumbing. The 3 px bar is gone, and the numerals carry progress.
- **Bearing arrow:** the objective's direction relative to where the character faces, snapped to eight
  directions, and drawn as one small stroked arrow, rotated (never emoji). It needs Mumble's avatar-facing
  vector. Verify that `GameService.Gw2Mumble.PlayerCharacter.Forward` (or an equivalent) exists in Blish
  1.3.0 before promising it. **If it doesn't, ship without the arrow.** The distance alone still works.
- **Nearest strip:** one 22 px line at the top. It shows `NEAREST` in `SectionFont`, then the single
  closest remaining objective across all targets with its bearing, name and distance. The waypoint-copy
  icon copies the waypoint nearest that objective (`NearestObjectiveService.NearestWaypoint`). It
  refreshes on the same 2 s accumulator the Next lines use, and hides when nothing is placeable.
- **Menu (`⋯`):** at the right of the Nearest strip, a `ContextMenuStrip` with *Open Quarry*, *Reload
  from file* and *Save now*. That's what's left of the five buttons once panels and subpages are gone.
- **Row click:** left-click a row and the Inspector shows that achievement (§7). Right-click a row for the
  same menu the Here card has, where it applies (Drop, Show route in Pathing). The Inspector's pin
  replaces the old "detach into own window".
- **Height:** rows × 40 + strip + summary, from the row count, capped at the screen clamp. That's the
  `GetDefaultCompactHeight` shape with the new constants. Phase 36's toggle-derived height function
  replaces it later, and nothing here gets ahead of that. Width stays `Storage.TrackWindowCompactWidth`.
- **Storage:** `TrackWindowCompact` goes dead. Leave the field and stop reading it; Phase 44 deletes it.
- **Session summary and empty-state label:** `BodyFont`, `TextMuted`. The empty state reads "No targets
  yet — open Quarry and target something."

---

## 7. The Inspector (Phase 45) — one detail pane, not N pop-outs

The Inspector replaces both `AchievementDetailsWindow` and `SubPageInformationWindow` as the default
detail surface. It's one window, `SavesPosition`, ~320 px wide, with its content height growing up to a
clamp and then scrolling. It shows whatever was clicked last, either a whole achievement or one objective,
and a new click replaces it. "Close all Subpages" disappears, because nothing accumulates. It isn't inline
expansion because a 300 px list can't hold a wiki image or three lines of notes and stay compact.

**Achievement view** (from a Target List row click, or the achievement name in the breadcrumb): the
achievement name (`NumeralFont` size, i.e. 18 Bold), the chips row, the description and requirements
(today's top-level text, through `FormattedLabel` with `BodyFont` as its base font), the Next line with
bearing, distance and waypoint copy, and the actions: *Show route in Pathing* (peek, pack-backed tiers
only), *Copy waypoint* and *Wiki*.

**Objective view** (from a chip click): the breadcrumb `Lost Lore · objective 6 of 11` in `SectionFont`,
the objective name (18 Bold), the chips row with the selected chip ringed, the wiki image if the subpage
has one (`ExternalImageService`, scaled to width, never upscaled), the notes, the place line (sector,
bearing, distance and a waypoint-copy icon), the Ticked by you toggle with its API note, and the same
three actions.

**Chips** are the objective tiles, redrawn. They have three states, never shown by colour alone (the
Phase 26 rule):

- Confirmed by the API: solid `Complete`-green fill, no glyph, can't be changed.
- Ticked by you: `ManualDone` hollow cream outline with a tick glyph, can be reverted.
- Not done: `PipTodo` fill, with the number in `TextSecondary`.

They're 24 px tall with a 3 px gap, and they wrap. The row-number text stays, because the Next line and
the remaining-objective prefixes refer to it. The numerals' tooltip says `N confirmed · M ticked by you`.

Reconcile: when the API later confirms a bit that was ticked by hand, drop the manual flag so the chip
goes solid. That change is the visible moment when the game catches up, which today's design can't show.
Before building, check whether `Storage`'s manual completion is per bit or per achievement (`Storage`
has `ManualCompletedAchievements`, and Phase 30 added manual-tick completion). If it's per achievement
only, a per-bit manual set in `Storage` and `AchievementProgress` has to land first.

**Navigation.** The chips row is the navigation, and it shows in both views. Click a chip to open the
objective view. Click the selected chip again, or the achievement name in the breadcrumb, to go back to
the achievement view. Click another chip and the selection moves straight to it. A Target List row click
always lands on the achievement view, never on a remembered objective. The selected chip has a 2 px
`NearDone` gold ring. That ring is the only "you are here" marker; the breadcrumb stays quiet.

**Click rules.** These replace any earlier "click to tick". Left-click always inspects. Ticking takes a
separate action: the Ticked by you toggle in the objective view, or right-click a chip and choose Mark done
/ Unmark. Hovering a chip shows its name only.

**Pin.** A pin in the title bar (native `WindowBase2` chrome; the pin is a 16 px glyph left of the close
button). A pinned Inspector freezes its content, and the next click opens a fresh Inspector. This is
today's "detach into own window", kept as the exception rather than the default. Pinned copies close with
their own ✕. `InspectorWindowManager` keeps a `pinnedWindows` list for this (the old
`AchievementDetailsWindowManager` was deleted in Phase 53).

**Reuse.** The two windows this replaces already render everything the Inspector needs
(`FormattedLabelHtmlService`, `ExternalImageService`, the `SubPageInformation` models, the objective
control factories). The Inspector is one control with two shapes over those, with no new rendering.

---

## 8. Naming (Phase 41 for window copy; manifest rename stays a publish-gate item)

- **Module:** Quarry, for me to confirm. Check blishhud.com/modules for a collision before the manifest
  changes. It's one word, the hunting sense fits, and it doesn't say "achievement", which is the point of
  the rename (ROADMAP publish gate item 1). Rejected: *Achievement Hunter* (an existing brand),
  *Pathfinder* (collides with Pathing) and *Compass*. Alternatives if Quarry doesn't sit right: *Hunter's
  Mark*, *Trailhead*, *Nearly There*.
- **Windows:** the overview window is titled Quarry, the tracked window is Target List, and the detail
  pane is Inspector.
- **Tabs:** *Here* (kept; it's the product's own word) and *All* for the category tree.
- **Verbs:** the header button is *Target these (N)* / *Target list full*. The eye's tooltips are *Target
  this* / *Drop this*. The Here card's menu keeps *Not today* / *Not interested* / *Unhide*.
- The manifest `name`, namespace and data directory don't change in Batch F. That's the publish-gate
  rename: expensive after publishing and cheap before, but a separate commit of its own.

---

## 9. What to look at during the load-test

Test at Small / 100 % DPI (my setting) over a bright map (Seitung harbour at noon) and a dark one. The bar
for "done" isn't "crisp as the game". It's this: every label is readable at arm's length, there are no
fuzzy halos, no stretched frame and no card taller than its content, and next to the Hero panel ours no
longer looks a decade older. New this round: you can tell from the Target List alone what to walk toward
and how far it is, without opening anything.

A reference to study, not copy: *Regions of Tyria* (my example, BACKLOG 2026-09-09), for text that sits
inside the game's visual language.
