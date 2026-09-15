# Changelog

Keyed by the `version` in `src/Quarry/manifest.json`. Dates are release dates.

## 2.0.1 — cold-install crash fix (2026-09-15)

- **Fixed:** opening the Target List crashed Blish HUD outright (a stack overflow) the first time it
  ever ran with an empty tracked set — i.e. every fresh install. Caused by a Blish HUD engine bug
  (`Label { WrapText = true, HorizontalAlignment != Left }` recurses without end when drawn) triggered by
  the Target List's own "No targets yet" empty-state message. Existing installs with anything already
  tracked never hit it, which is why this shipped in 2.0.0 undetected.

## 2.0.0 — first public release (2026-09-15)

Quarry began as a fork of Denrage's [Achievement Tracker](https://github.com/Denrage/AchievementTrackerModule)
(MIT) and is now its own module: an achievement hunter. The `1.x` line was the private fork
(`Achievement Tracker+`); `2.0.0` is the first build under the Quarry name and namespace, so it installs
alongside Denrage's module rather than replacing it, and starts with an empty tracked set.

### What it does

- **Here** — a short, ranked list of the achievements you can make progress on in your current map,
  nearest to done first, with a guidance badge (marker-pack objectives, exact wiki coordinates, a named
  area, or nothing but the name). Capped at 10 by default (5–15). Hide or snooze what you don't want.
  An *Anywhere* block lists the closest-to-done across your account, and "Target these" fills your free
  slots from the map's list.
- **The Target List** — the small in-play window: what's left on each target, the nearest remaining
  objective and its distance, a waypoint to copy when porting beats walking, a three-row Here strip so
  you can pick up a new target without opening the main window, and a session line.
- **The Inspector** — one detail pane: the objectives with their real completion state (API-confirmed or
  ticked by you), the wiki's notes and image for the selected one, place name and distance.
- **Hunt mode** (off by default) — tracking an achievement a marker pack covers turns that pack's routes
  on in the Pathing module; untracking or finishing it turns off only what Quarry turned on.
- A one-line notification on entering a map with something for you, a "Done:" toast on completion,
  auto-untracking, and reload of the tracked set when `persistanceStorage.json` changes on disk.

### Data

- Achievement tables and wiki-derived locations come from Denrage's hosted data files; the wiki subpage
  data that used to be a 70 MB runtime download ships inside the `.bhm` as a 1.2 MB derived file.
- Marker packs are indexed from the shared `markers` folder (TmfLib) and cached; packs are never modified.

### Compared with Achievement Tracker

Everything above is new. Removed: the per-achievement detail windows, the item detail windows and the
in-app wiki subpage window (wiki links open your browser), and the `AnyCPU`/`x64` split in the build.
