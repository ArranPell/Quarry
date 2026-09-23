# Quarry

An achievement hunter for Guild Wars 2, built as a [Blish HUD](https://blishhud.com/) module. It answers one question while you play: on the map you're standing on, what can you finish, and where's the next piece of it?

It started as a fork of Denrage's [Achievement Tracker](https://github.com/Denrage/AchievementTrackerModule) (MIT) and grew into a different module with a different job. Denrage's tracks whatever achievement you point it at. Quarry works out which ones are worth your time on the map you're on.

## What it does

- **Here.** A short, ranked list of achievements you can make progress on in your current map. The one nearest to done comes first, and achievement points break ties. Each one gets a badge for how well it can be guided: marker-pack objectives, exact wiki coordinates, a named area, or just the name. Achievements you can't start yet (locked, or prerequisites unmet) stay off the list, and anything you've hidden stays hidden.
- **The Target List.** The small window you keep open while playing. Each target shows what's left, the nearest remaining objective and how far away it is, and a waypoint to copy when porting beats walking. A three-row Here strip at the bottom lets you pick a new target without opening the main window.
- **The Inspector.** One detail pane, instead of a separate window for each achievement or objective. It lists the objectives with their completion state, either from the API or ticked off by you. For the objective you've selected, it shows the wiki's notes and image, the place name and the distance.
- **Hunt mode.** Track an achievement a marker pack covers and Quarry turns that pack's routes on in the Pathing module. When you untrack or finish it, Quarry turns off only what it turned on. Off by default.
- **Quiet feedback.** One line when you enter a map that has something for you, and a "Done:" toast when an achievement completes. Completed achievements are untracked automatically.

## Why the lists are short

Every list in Quarry has a small cap. Here defaults to 10 and can be set from 5 to 15, the Target List stops at 15, and the strip shows 3. Nobody needs to see all four hundred things they haven't finished. When the data can't answer, as in most of core Tyria or on a map Quarry doesn't know, the window says so instead of sitting empty.

## What it needs

- Blish HUD with an API key that has the `account` and `progression` permissions. Quarry never touches the key itself; Blish hands it a subtoken.
- Optional but recommended: the [Pathing](https://blishhud.com/modules/?module=bh.community.pathing) module and one or more marker packs in the shared `markers` folder. Packs give the best guidance. Their objectives disappear as you finish them, and Hunt mode can switch their routes on. Without packs, Quarry falls back to the wiki's coordinates and place names, which are sparse and cover far fewer achievements. The guidance badge tells you which kind you're getting.
- Expansion and Living World maps are covered. Core Tyria is only partly covered, by whatever the packs and the wiki can place, and the UI tells you when a map has nothing guided.

## Install

Install it from Blish HUD's in-game module repository by searching for Quarry in the Manage Modules panel. To install by hand, download `Quarry.bhm` from the [latest release](https://github.com/ArranPell/Quarry/releases/latest), put it in `Documents\Guild Wars 2\addons\blishhud\modules\`, and enable it in Manage Modules.

Quarry keeps its own data under `Documents\Guild Wars 2\addons\blishhud\quarry\`. That's your tracked set and window positions (`persistanceStorage.json`), a cache of the wiki-derived achievement data it downloads, and an index of your marker packs. It reads packs from the shared `markers` folder and never modifies them.

## Using it

- **Open Quarry.** Click the corner icon, or use its keybind, to open the main window. It has two tabs. The Here tab is the ranked list for your current map. The All tab is the full category tree with search, for when you'd rather find something by name than by location.
- **Target something.** Click a card in either tab to add it to your Target List. The eye icon lights up to confirm; click again to drop it. The + buttons on the Here strip at the bottom of the Target List do the same thing, so you don't have to leave that window.
- **Open the Target List.** It opens on its own the first time you target something. After that, reopen it from Quarry's ⋯ menu or its keybind. Each row shows what's left. Once Quarry knows your map and position, the row also shows the nearest remaining objective and its distance. A waypoint icon appears when porting beats walking; click it to copy the chat code.
- **Open the Inspector.** Click any card or Target List row. The name, the progress and the next-step text all work. The Inspector shows the full objective list with completion state, plus the wiki's notes, image and a place name for the objective you've selected.
- **Tick a step yourself.** Right-click a step's chip in the Inspector, or use the "Ticked by you" checkbox, to mark it done. The tick is yours only. The API's own confirmation replaces it once that arrives, and you can untick it any time before then.
- **Hide something from Here.** Right-click a card and choose Not today (hidden until the daily reset) or Not interested (hidden until you undo it). A hidden card's tooltip tells you why and has an unhide option.
- **Turn on Hunt mode.** With Pathing and a covering marker pack installed, turn Hunt mode on in settings, then track an achievement the pack covers. Quarry switches that pack's routes on, and back off when you untrack or finish it. If a guidance badge shows a route, you can click it to preview the route in Pathing without tracking anything.

## Settings worth knowing

- **Here: how many to list** (5 to 15, default 10) and **Here: minimum guidance** (show everything, or only achievements guided at least this well).
- **Hunt mode** (off by default) and **Revert hunt routes on disable**.
- **Untrack achievements on completion**, **Notify on map change**, the corner icon, and a keybind to toggle the Target List.
- **Auto save every 5 minutes**. Without it, Quarry saves on unload and a few seconds after every track or untrack.

Quarry is English-only.

## Building

You'll need Visual Studio 2022 (or the Build Tools) with the .NET Framework 4.7.2 targeting pack, plus the .NET 8 SDK.

```
dotnet build src/Quarry/Quarry.csproj -c Debug
```

The BlishHUD NuGet package's build targets produce `src\Quarry\bin\Debug\net4.7.2\Quarry.bhm`. To run it against a Blish HUD install without installing it:

```
& "<Blish HUD install>\Blish HUD.exe" --debug --module "<this repo>\src\Quarry\bin\Debug\net4.7.2\Quarry.bhm"
```

`docs/` holds the plan, the roadmap, the backlog, the UI design brief and the Pathing notes. Some of those docs, and a few source comments, cite a decision log (`docs/DECISIONS.md`) and a completed-phase archive (`docs/COMPLETED.md`). Both files exist, but they live in my private working repo, so you won't find them here.

## Credits and licence

- **Denrage** and **Atzie**, for the original Achievement Tracker module this grew out of, and for the wiki-derived data Quarry still downloads.
- The Guild Wars 2 Wiki, whose achievement tables and location pages are how Quarry knows where things are.
- The marker-pack authors whose packs Quarry indexes.

MIT. `LICENSE` carries two copyright notices: Denrage's for the original work and mine (ArranPell) for Quarry. Both stay.

The three hosted data files Quarry downloads (`version.json`, `achievement_data.json`, `achievement_tables.json`) are derived from Guild Wars 2 Wiki content. Contributor-written text on the wiki is available under the [GNU Free Documentation License 1.3 (GFDL)](http://www.gnu.org/copyleft/fdl.html). Official Guild Wars 2 game content that the wiki reproduces, such as achievement names, descriptions and other in-game text, remains ArenaNet's and NCSoft's and is not covered by the GFDL.

Quarry is not affiliated with ArenaNet or NCSoft. Guild Wars 2 is a trademark of NCSoft.
