# Quarry

An achievement **hunter** for Guild Wars 2, as a [Blish HUD](https://blishhud.com/) module. It answers one
question while you play: *you're standing in this map — what can you actually finish here, and where is
the next piece of it?*

Based on Denrage's [Achievement Tracker](https://github.com/Denrage/AchievementTrackerModule) (MIT).
Quarry started as a fork of it and is now a different module with a different job: Denrage's tracks any
achievement you point it at; Quarry works out which ones are worth your time on the map you're on.

## What it does

- **Here.** A short, ranked list of the achievements you can make progress on in your current map:
  nearest to done first, ties broken by achievement points, with a badge saying how well each one can be
  guided (marker-pack objectives, exact wiki coordinates, a named area, or nothing but the name). Things
  you can't start yet (locked, prerequisites unmet) are left out; things you've hidden stay hidden.
- **The Target List.** The small in-play window. Each target shows what's left, the nearest remaining
  objective and how far away it is, and a waypoint to copy when porting beats walking. A three-row *Here*
  strip at the bottom lets you pick up a new target without opening the main window.
- **The Inspector.** One detail pane instead of a window per thing: the objectives with their real
  completion state (from the API, or ticked by you), the wiki's notes and image for the selected one,
  and the place name and distance.
- **Hunt mode.** Tracking an achievement that a marker pack covers turns that pack's routes on in the
  Pathing module; untracking or finishing it turns off only what Quarry turned on. Off by default.
- **Quiet feedback.** A one-line notification on entering a map that has something for you, a "Done:"
  toast when an achievement completes, and automatic untracking when it does.

## The rule that shapes everything

**Bounded sessions, not completeness.** Every list Quarry shows is capped, and the caps are small on
purpose (Here defaults to 10 and can be set between 5 and 15; the Target List stops at 15; the strip
shows 3). Listing all four hundred things you haven't finished is the failure mode, not the goal. When the
data can't answer — most of core Tyria, an unknown map — the window says so rather than showing nothing.

## What it needs

- Blish HUD with an API key that has the `account` and `progression` permissions. Quarry never handles
  the key itself; Blish hands it a subtoken.
- **Optional but recommended:** the [Pathing](https://blishhud.com/modules/?module=bh.community.pathing)
  module and one or more marker packs in the shared `markers` folder. Packs are where the best guidance
  comes from — objectives that disappear as you finish them, and routes Hunt mode can switch on. Without
  packs, Quarry still works from the wiki's coordinates and place names, and the guidance badge tells you
  which kind you're getting.
- Expansion and Living World maps are covered. Core Tyria is only partly covered (what the packs and the
  wiki can place); the UI says when a map has nothing guided.

## Install

Download `Quarry.bhm` from the [latest release](https://github.com/ArranPell/Quarry/releases/latest),
drop it into `Documents\Guild Wars 2\addons\blishhud\modules\` and enable it in Blish HUD's Manage
Modules panel. It will appear in the Blish HUD module repository once listed there.

Quarry keeps its own data under `Documents\Guild Wars 2\addons\blishhud\quarry\`: the tracked set and
window positions (`persistanceStorage.json`), a cache of the wiki-derived achievement data it downloads,
and an index of your marker packs. It reads packs from the shared `markers` folder and never modifies
them.

## Settings worth knowing

- **Here: how many to list** (5–15, default 10) and **Here: minimum guidance** (show everything, or only
  achievements guided at least this well).
- **Hunt mode** (off by default) and **Revert hunt routes on disable**.
- **Untrack achievements on completion**, **Notify on map change**, the corner icon, and a keybind to
  toggle the Target List.
- **Auto save every 5 minutes** — otherwise Quarry saves on unload and a few seconds after every
  track/untrack.

Quarry is English-only; there is no localisation layer.

## Building

Visual Studio 2022 (or the Build Tools) with the .NET Framework 4.7.2 targeting pack and the .NET 8 SDK.

```
dotnet build src/Quarry/Quarry.csproj -c Debug
```

The BlishHUD NuGet package's build targets produce `src\Quarry\bin\Debug\net4.7.2\Quarry.bhm`. To run it
against a Blish HUD install without installing it:

```
& "<Blish HUD install>\Blish HUD.exe" --debug --module "<this repo>\src\Quarry\bin\Debug\net4.7.2\Quarry.bhm"
```

`docs/` holds the plan, roadmap, backlog, the UI design brief and the Pathing notes. Those docs — and a few
source comments — cite a decision log (`docs/DECISIONS.md`) and a completed-phase archive
(`docs/COMPLETED.md`) that live in the author's private working repo; the citations point at a record that
exists, just not here.

## Credits and licence

- **Denrage** and **Atzie**, for the original Achievement Tracker module this grew out of, and for the
  wiki-derived data it still downloads.
- The Guild Wars 2 Wiki, whose achievement tables and location pages are what make "where is it"
  answerable at all.
- The marker-pack authors whose packs Quarry indexes.

MIT. `LICENSE` carries both copyright notices — Denrage's for the original work and ArranPell's for
Quarry — and both stay.
