# Changelog

Keyed by the `version` in `src/Quarry/manifest.json`. Dates are release dates.

## 2.0.3 — manifest fix for the repo downloader (2026-09-21)

- **Fixed:** the module manifest had no `contributors` field, which the official repo downloader requires.
  Without it the download failed. Batch H's Phase 51 had replaced the field with a single-value `author`
  field. `contributors` is back as a one-element array, and the "Authored by" credit is unchanged.

## 2.0.2 — our own data hosting, and Quarry's own mark (2026-09-20)

- **Changed:** the three wiki data files now come from Quarry's own hosting at
  `bhm.blishhud.com/ArranPell.Quarry/` instead of Denrage's module namespace. The files are
  byte-identical mirrors, so existing installs keep their cached copies and download nothing. Quarry no
  longer depends on another module's hosting staying in place. The data is no fresher: it still dates
  from 22 April 2026.

- **Changed:** new artwork. The Blish corner icons, the window emblem and the All tab now use Quarry's
  own reticle mark instead of the icon inherited from Achievement Tracker. If you run both modules, they
  no longer share an icon in the taskbar.

## 2.0.1 — cold-install crash fix (2026-09-15)

- **Fixed:** on a fresh install, opening the Target List crashed Blish HUD with a stack overflow. It
  happened the first time the list ran with an empty tracked set. The cause is a Blish HUD engine bug:
  `Label { WrapText = true, HorizontalAlignment != Left }` recurses without end when drawn, and the
  Target List's "No targets yet" message was such a label. Installs that already had something tracked
  never showed the message, which is how the bug got past testing into 2.0.0.

## 2.0.0 — first public release (2026-09-15)

Quarry began as a fork of Denrage's [Achievement Tracker](https://github.com/Denrage/AchievementTrackerModule)
(MIT) and is now its own module, an achievement hunter. The `1.x` line was the private fork
(`Achievement Tracker+`). `2.0.0` is the first build under the Quarry name and namespace. It installs
alongside Denrage's module instead of replacing it, and starts with an empty tracked set.

### What it does

- **Here.** A short, ranked list of the achievements you can make progress on in your current map, with
  the nearest to done first. Each has a guidance badge: marker-pack objectives, exact wiki coordinates,
  a named area, or the name only. The list is capped at 10 by default (5–15). You can hide or snooze
  what you don't want. An *Anywhere* block lists the closest-to-done achievements across your account,
  and "Target these" fills your free slots from the map's list.
- **The Target List.** The small window you keep open while playing. For each target it shows what's
  left, the nearest remaining objective and its distance, and a waypoint to copy when porting beats
  walking. A three-row Here strip lets you pick a new target without opening the main window, and a
  session line counts what you've completed this session.
- **The Inspector.** One detail pane. It lists the objectives with their completion state, confirmed by
  the API or ticked by you, and shows the wiki's notes and image, the place name and the distance for
  the selected one.
- **Hunt mode** (off by default). Tracking an achievement a marker pack covers turns that pack's routes
  on in the Pathing module. Untracking or finishing it turns off only what Quarry turned on.
- A one-line notification when you enter a map with something for you, a "Done:" toast on completion,
  automatic untracking, and a reload of the tracked set when `persistanceStorage.json` changes on disk.

### Data

- Achievement tables and wiki-derived locations come from Denrage's hosted data files. The wiki subpage
  data used to be a 70 MB download at runtime. It now ships inside the `.bhm` as a 1.2 MB derived file.
- Marker packs in the shared `markers` folder are indexed with TmfLib and cached. Quarry never modifies
  them.

### Compared with Achievement Tracker

Everything above is new. Removed: the per-achievement detail windows, the item detail windows, the
in-app wiki subpage window (wiki links now open your browser), and the `AnyCPU`/`x64` split in the build.
