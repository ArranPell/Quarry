# Changelog

Keyed by the `version` in `src/Quarry/manifest.json`. Dates are release dates.

## 2.0.6 — newer achievements, a Here fix and a lighter icon (2026-09-25)

- **Added:** achievements newer than Quarry's wiki data (22 April 2026) now show up. That's about 730 of
  them, including both newer Visions of Eternity maps, Solitary Throne, the missing Rare Collections and
  a few Explorer and Rift Hunting stragglers. Quarry fetches them from the GW2 API once the categories
  load, so they appear in the All tab, the Inspector and the Target List, and can be tracked. They carry
  the API's text and objectives but no wiki notes, coordinates or images, so their guidance badge reads
  "name only". The Wiki button runs a wiki search for the name.

- **Fixed:** Here showed a multi-map achievement on a map where you'd already finished your part of it.
  An achievement that's in Here only because a marker pack places it on the map now needs something left
  to do on that map. Achievements linked to the map through their category are unchanged.

- **Fixed:** the corner icon was much darker than Blish HUD's other icons
  ([#6](https://github.com/ArranPell/Quarry/issues/6)). It's now light grey, and turns gold on hover.

## 2.0.5 — version bump only (2026-09-23)

- No changes from 2.0.4. The version number moved on so 2.0.4 isn't published twice.

## 2.0.4 — progress fix for accounts with duplicate API entries, and better logs (2026-09-23)

- **Fixed:** on some accounts Quarry showed no progress for any achievement, including completed ones
  ([#5](https://github.com/ArranPell/Quarry/issues/5)). The GW2 API can list the same achievement twice
  for one account (seen for *Raid Mentor: Decima* and *Raid Mentor: Ura*). Quarry failed on the
  duplicate and gave up on every refresh. It now keeps the most-progressed of the two entries.

- **Changed:** logging, so real problems stand out in Blish HUD's error reports:
  - Failures caused by your setup, like a blocked download or a Documents folder Quarry can't write
    to, are now warnings. They were errors. Blish still shows you its own dialog for them.
  - Pathing changing in a way that breaks Hunt mode is now an error. It was a warning.
  - The startup log line now names the achievement data version in use, which helps with bug reports.

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
