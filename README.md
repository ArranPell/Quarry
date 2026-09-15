# Quarry

An achievement hunter for Guild Wars 2, built as a [Blish HUD](https://blishhud.com/) module. It answers one question while you play: you're standing on this map — what can you actually finish here, and where's the next piece of it?

It started as a fork of Denrage's [Achievement Tracker](https://github.com/Denrage/AchievementTrackerModule) (MIT) and grew into a different module with a different job. Denrage's tracks whatever achievement you point it at. Quarry figures out which ones are worth your time on the map you're on.

## What it does

- **Here.** A short, ranked list of achievements you can make progress on in your current map. Nearest to done comes first, with ties broken by achievement points, and each one gets a badge for how well it can be guided: marker-pack objectives, exact wiki coordinates, a named area, or just the name. Anything you can't start yet (locked, prerequisites unmet) stays off the list, and anything you've hidden stays hidden.
- **The Target List.** The small window you keep open while playing. Each target shows what's left, the nearest remaining objective and how far away it is, and a waypoint to copy when porting beats walking. A three-row Here strip at the bottom lets you pick up a new target without opening the main window.
- **The Inspector.** One detail pane instead of a window per thing. It shows the objectives with their real completion state (from the API, or ticked off by you), the wiki's notes and image for whichever one you've selected, and the place name and distance.
- **Hunt mode.** Track an achievement a marker pack covers and Quarry turns that pack's routes on in the Pathing module. Untrack it or finish it and Quarry turns off only what it turned on. Off by default.
- **Quiet feedback.** One line when you enter a map that has something for you, a "Done:" toast when an achievement completes, and automatic untracking when it does.

## Why the lists are short

Every list in Quarry is capped, and the caps are small on purpose. Here defaults to 10 and can be set anywhere from 5 to 15, the Target List stops at 15, and the strip shows 3. Nobody needs to see all four hundred things they haven't finished. When the data can't answer, as in most of core Tyria or on a map Quarry doesn't know, the window says so instead of sitting empty.

## What it needs

- Blish HUD with an API key that has the `account` and `progression` permissions. Quarry never touches the key itself; Blish hands it a subtoken.
- Optional but recommended: the [Pathing](https://blishhud.com/modules/?module=bh.community.pathing) module and one or more marker packs in the shared `markers` folder. Packs give the best guidance, with objectives that disappear as you finish them and routes Hunt mode can switch on. Without packs, Quarry falls back to the wiki's coordinates and place names, which are sparse and cover far fewer achievements. The guidance badge tells you which kind you're getting.
- Expansion and Living World maps are covered. Core Tyria is only partly covered (whatever the packs and the wiki can place), and the UI tells you when a map has nothing guided.

## Install

Download `Quarry.bhm` from the [latest release](https://github.com/ArranPell/Quarry/releases/latest), drop it into `Documents\Guild Wars 2\addons\blishhud\modules\`, and enable it in Blish HUD's Manage Modules panel. It'll show up in the Blish HUD module repository once it's listed there.

Quarry keeps its own data under `Documents\Guild Wars 2\addons\blishhud\quarry\`: your tracked set and window positions (`persistanceStorage.json`), a cache of the wiki-derived achievement data it downloads, and an index of your marker packs. It reads packs from the shared `markers` folder and never modifies them.

## Using it

- **Open Quarry.** Click the corner icon (or its keybind) for the main window's **Here** and **All** tabs. Here is the ranked list for your current map; All is the full category tree with search, for when you'd rather find something by name than by location.
- **Target something.** Click a card, in either tab, to add it to your Target List — the eye icon lights up to confirm, click again to drop it. The Here strip at the bottom of the Target List does the same thing from its **+** buttons, so you don't have to leave that window.
- **Open the Target List.** It pops open on its own the first time you target something. After that, get it back from Quarry's **⋯** menu or its keybind. Each row shows what's left and, once it knows your map and position, the nearest remaining objective and its distance — with a waypoint icon when porting beats walking, so a click there copies the chat code.
- **Open the Inspector.** Click any card or Target List row — the name, the progress, the next-step text all work — to see the full objective list, with real completion state, the wiki's notes and image, and a place name for whichever objective you've selected.
- **Tick a step yourself.** Right-click a step's chip in the Inspector to mark it done for yourself — or use the "Ticked by you" checkbox, same thing. It's your mark only, so the API's own confirmation replaces it once that lands, and you can untick it any time before then.
- **Hide something from Here.** Right-click a card for **Not today** (hidden until the daily reset) or **Not interested** (hidden until you undo it). A hidden card's tooltip tells you why, with an unhide option to bring it back.
- **Turn on Hunt mode.** With Pathing and a covering marker pack installed, flip Hunt mode on in settings, then track an achievement it covers — Quarry switches that pack's routes on for you, and back off when you untrack or finish it. See a guidance badge with a route on it? Click it to peek the route in Pathing without tracking first.

## Settings worth knowing

- **Here: how many to list** (5 to 15, default 10) and **Here: minimum guidance** (show everything, or only achievements guided at least this well).
- **Hunt mode** (off by default) and **Revert hunt routes on disable**.
- **Untrack achievements on completion**, **Notify on map change**, the corner icon, and a keybind to toggle the Target List.
- **Auto save every 5 minutes**. Otherwise Quarry saves on unload and a few seconds after every track or untrack.

Quarry is English-only. There's no localisation layer.

## Building

You'll need Visual Studio 2022 (or the Build Tools) with the .NET Framework 4.7.2 targeting pack, plus the .NET 8 SDK.

```
dotnet build src/Quarry/Quarry.csproj -c Debug
```

The BlishHUD NuGet package's build targets produce `src\Quarry\bin\Debug\net4.7.2\Quarry.bhm`. To run it against a Blish HUD install without installing it:

```
& "<Blish HUD install>\Blish HUD.exe" --debug --module "<this repo>\src\Quarry\bin\Debug\net4.7.2\Quarry.bhm"
```

`docs/` holds the plan, roadmap, backlog, the UI design brief, and the Pathing notes. Some of those docs, and a few source comments, cite a decision log (`docs/DECISIONS.md`) and a completed-phase archive (`docs/COMPLETED.md`). Both exist, but they live in my private working repo, so the citations point at files you won't find here.

## Credits and licence

- **Denrage** and **Atzie**, for the original Achievement Tracker module this grew out of, and for the wiki-derived data Quarry still downloads.
- The Guild Wars 2 Wiki, whose achievement tables and location pages are what make "where is it" answerable at all.
- The marker-pack authors whose packs Quarry indexes.

MIT. `LICENSE` carries both copyright notices, Denrage's for the original work and ArranPell's for Quarry, and both stay.
