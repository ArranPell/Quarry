using Quarry.Interfaces;
using Quarry.Models;
using Quarry.Models.Markers;
using Blish_HUD;
using Quarry.WikiData.Achievement;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace Quarry.Services
{
    // Phase 28: objective locations for achievements no marker pack tags, taken from the wiki subpage the
    // achievement's own row already links to.
    //
    // Why this is worth having despite covering only ~1.8 % of rows overall: the coverage is bimodal, not
    // thin. Measured against the live v9 data on 2026-09-09 -- 379 of 20,914 rows carry a coordinate, but
    // **31 achievements have one on every row** and 66 on at least half (Master of Ceremonies, Llama
    // Roundup, Captain Leo the Relentless, the Janthir event collections). That cohort is hunt-shaped:
    // event and champion collections, i.e. exactly what packs tag worst. Silent where absent, so it reads
    // as an opportunistic hint rather than a fallback that fires one row in five (DECISIONS 2026-09-09).
    //
    // Better behaved than an untagged pack marker, too: the row is mapped to an API bit through
    // BitAlignmentService, so a wiki coordinate disappears when you finish that step. An untagged marker
    // never can.
    //
    // Phase 29 adds a second tier for rows that carry no InteractiveMap.Coordinates but do name a place in
    // the Zone/Location/Locations/Area/Region keys of SubPageInformation.DescriptionList: resolved against
    // the current map's sectors, that place name becomes a real centroid coordinate (still GuidanceTier
    // .Coordinate -- a sector centroid is a real countdown, just to an area rather than a point), and the
    // overwhelming beneficiary is the Explorer achievements (Verdant Brink 23/23 named, 18/23 matching a
    // sector). A name with no matching sector falls back to GuidanceTier.Area only when it names the
    // current map itself; anything else says nothing rather than guess.
    public class WikiLocationService : IWikiLocationService
    {
        // The wiki markup for these values is "<a ...>Name</a>" alone, or "<a ...>Name</a><br><small>(<a
        // ...>Parent</a>)</small>" when a broader zone/region trails in parentheses, or -- checked against
        // the real data before trusting the format -- occasionally several genuine places <br>-separated
        // with no parenthetical at all (a wandering NPC's stops). Never comma-separated in the data seen.
        // The five keys themselves are DerivedSubpageGenerator's filter now (Phase 48) -- this class only
        // sees values already restricted to them.
        private static readonly Regex HtmlTagRegex = new Regex("<[^>]+>", RegexOptions.Compiled);

        private readonly IAchievementService achievementService;
        private readonly IWikiSubpageDataService wikiSubpageDataService;
        private readonly IBitAlignmentService bitAlignmentService;
        private readonly ICurrentMapService currentMapService;
        private readonly Logger logger;

        private readonly object buildLock = new object();
        private Dictionary<string, Vector2> continentCoordinatesByLink;
        private Dictionary<string, List<string>> placeNamesByLink = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // achievementId -> the rows that carry a coordinate or a place name. Built on first use per
        // achievement; the map test and the completion test are re-done per call because both change as
        // you play.
        private readonly Dictionary<int, IReadOnlyList<WikiRowLocation>> rowsByAchievementId = new Dictionary<int, IReadOnlyList<WikiRowLocation>>();

        public WikiLocationService(IAchievementService achievementService, IWikiSubpageDataService wikiSubpageDataService, IBitAlignmentService bitAlignmentService, ICurrentMapService currentMapService, Logger logger)
        {
            this.achievementService = achievementService;
            this.wikiSubpageDataService = wikiSubpageDataService;
            this.bitAlignmentService = bitAlignmentService;
            this.currentMapService = currentMapService;
            this.logger = logger;
        }

        public bool HasAnyLocations(int achievementId)
            => this.GetRows(achievementId).Count > 0;

        // Remaining wiki-located objectives for this achievement on this map, in the same shape a pack
        // objective takes so the nearest-objective code needs no second path. HeightUnknown is set: a
        // wiki coordinate (exact or a sector centroid) is 2-D, and inventing a height would put a
        // plausible-looking wrong number on the Next line, which is the failure mode Phase 24 just removed
        // for trails.
        public IReadOnlyList<AchievementObjective> GetRemainingOnMap(int achievementId, int mapId)
        {
            var rows = this.GetRows(achievementId);

            if (rows.Count == 0)
            {
                return Array.Empty<AchievementObjective>();
            }

            this.currentMapService.TryGetSectors(mapId, out var sectors);

            var result = new List<AchievementObjective>();

            foreach (var row in rows)
            {
                var bit = this.bitAlignmentService.MapRowToBit(achievementId, row.Row);

                if (bit < 0 || this.achievementService.HasFinishedBitIndex(achievementId, bit))
                {
                    continue;
                }

                Vector2 world;
                string sectorName = null;

                if (row.HasCoordinate)
                {
                    // The continent -> map transform doubles as the "is this even on this map" test: a
                    // coordinate outside the map's continent rectangle belongs to some other map, and we
                    // never had to know which one. An exact coordinate beats a sector match for the same
                    // row, so a row never checks both.
                    if (!this.currentMapService.TryContinentToWorld(mapId, row.ContinentX, row.ContinentY, out world))
                    {
                        continue;
                    }
                }
                else
                {
                    // Phase 29: no exact coordinate, so try the row's place name(s) against this map's
                    // sectors -- exact match only (case-insensitive, trimmed), never fuzzy. A miss here is
                    // silence, not a guess; HasAreaOnlyRemaining is what turns a same-map, no-sector miss
                    // into the · Area badge instead.
                    var sector = FindSectorMatch(sectors, row.PlaceNames);

                    if (sector is null)
                    {
                        continue;
                    }

                    world = sector.World;
                    sectorName = sector.Name;
                }

                result.Add(new AchievementObjective
                {
                    Namespace = null,
                    Bit = bit,
                    MapId = mapId,
                    // Stored in the pack's own axis convention (ObjectiveGeometry transposes to (x, z, y)),
                    // so X and Z are the horizontal pair and Y is the height we don't have.
                    X = world.X,
                    Y = 0f,
                    Z = world.Y,
                    IsTrail = false,
                    Waypoint = null,
                    HeightUnknown = true,
                    SectorName = sectorName,
                    Source = ObjectiveSource.Wiki,
                });
            }

            return result;
        }

        // Phase 29: true when this achievement has a remaining row that names this map's own zone but
        // matched no sector above -- meant to be checked only after GetRemainingOnMap comes back empty, so
        // a row that DID match a sector (and so already produced GuidanceTier.Coordinate) is never
        // double-counted here at the weaker Area tier.
        public bool HasAreaOnlyRemaining(int achievementId, int mapId)
        {
            var rows = this.GetRows(achievementId);

            if (rows.Count == 0 || !this.currentMapService.TryGetMapName(mapId, out var mapName) || string.IsNullOrEmpty(mapName))
            {
                return false;
            }

            foreach (var row in rows)
            {
                if (row.HasCoordinate || row.PlaceNames is null)
                {
                    continue;
                }

                var bit = this.bitAlignmentService.MapRowToBit(achievementId, row.Row);

                if (bit < 0 || this.achievementService.HasFinishedBitIndex(achievementId, bit))
                {
                    continue;
                }

                foreach (var place in row.PlaceNames)
                {
                    if (string.Equals(place, mapName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static MapSector FindSectorMatch(IReadOnlyList<MapSector> sectors, IReadOnlyList<string> placeNames)
        {
            if (sectors is null || sectors.Count == 0 || placeNames is null)
            {
                return null;
            }

            foreach (var place in placeNames)
            {
                foreach (var sector in sectors)
                {
                    if (string.Equals(place, sector.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return sector;
                    }
                }
            }

            return null;
        }

        private IReadOnlyList<WikiRowLocation> GetRows(int achievementId)
        {
            lock (this.buildLock)
            {
                if (this.rowsByAchievementId.TryGetValue(achievementId, out var cached))
                {
                    return cached;
                }

                var rows = this.BuildRows(achievementId);
                this.rowsByAchievementId[achievementId] = rows;
                return rows;
            }
        }

        private IReadOnlyList<WikiRowLocation> BuildRows(int achievementId)
        {
            var byLink = this.EnsureCoordinateIndex();

            if ((byLink.Count == 0 && this.placeNamesByLink.Count == 0) || !this.achievementService.AchievementsById.TryGetValue(achievementId, out var achievement))
            {
                return Array.Empty<WikiRowLocation>();
            }

            var entries = GetEntryLinks(achievement);

            if (entries is null)
            {
                return Array.Empty<WikiRowLocation>();
            }

            var rows = new List<WikiRowLocation>();

            for (var row = 0; row < entries.Count; row++)
            {
                var link = NormalizeLink(entries[row]);

                if (link is null)
                {
                    continue;
                }

                if (byLink.TryGetValue(link, out var coordinate))
                {
                    rows.Add(new WikiRowLocation { Row = row, HasCoordinate = true, ContinentX = coordinate.X, ContinentY = coordinate.Y });
                }
                else if (this.placeNamesByLink.TryGetValue(link, out var places))
                {
                    rows.Add(new WikiRowLocation { Row = row, PlaceNames = places });
                }
            }

            // Everything here hangs off row -> API bit, and MapRowToBit falls back to the identity
            // mapping until alignment has actually been computed for this achievement -- which would
            // attribute a coordinate to the wrong step and hide it against the wrong completion flag.
            // Kick the alignment off (it's cached and deduplicated) so the answer becomes right shortly
            // after first use rather than staying approximate all session.
            if (rows.Count > 0)
            {
                _ = this.bitAlignmentService.PrefetchAsync(achievementId, achievement);
            }

            return rows;
        }

        // Same two description shapes AchievementProgress and NearestObjectiveService's row lookup walk.
        private static IReadOnlyList<string> GetEntryLinks(AchievementTableEntry achievement)
        {
            switch (achievement.Description)
            {
                case CollectionDescription collection:
                    return collection.EntryList.Select(e => e.Link).ToList();
                case ObjectivesDescription objectives:
                    return objectives.EntryList.Select(e => e.Link).ToList();
                default:
                    return null;
            }
        }

        // Subpage links are absolute; a row's link is usually the relative "/wiki/..." form. The rest of
        // the module joins these two the same way (FormattedLabelHtmlService).
        private static string NormalizeLink(string link)
        {
            if (string.IsNullOrEmpty(link))
            {
                return null;
            }

            return link.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? link
                : "https://wiki.guildwars2.com" + link;
        }

        // One pass over the derived subpage set (Phase 48: DerivedSubpageGenerator already reduced this
        // from the 70 MB subPages.json to only entries with a coordinate, a place-name-bearing
        // DescriptionList value, or a Location page's Title), building both the coordinate index
        // (Phase 28) and the place-name index (Phase 29) together. Cached after first build.
        private IReadOnlyDictionary<string, Vector2> EnsureCoordinateIndex()
        {
            if (this.continentCoordinatesByLink != null)
            {
                return this.continentCoordinatesByLink;
            }

            var byLink = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
            var placesByLink = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var subpages = this.wikiSubpageDataService.ByLink;

            if (subpages.Count == 0)
            {
                // Wiki data still loading -- don't cache an empty index as if it were the answer.
                return byLink;
            }

            foreach (var pair in subpages)
            {
                var link = pair.Key;
                var subpage = pair.Value;

                if (TryParseCoordinates(subpage.Coordinates, out var coordinate))
                {
                    byLink[link] = coordinate;
                }

                // A place page never reliably names itself in the five keys below -- checked against the
                // live cache, not assumed, after Daigo Ward's own page (Type: Area) turned out to carry only
                // "Zone: Seitung Province" (its *parent*, one level up) and no self-referencing "Area: Daigo
                // Ward" entry at all, the same gap Caledon Forest's "Region: Maguuma Jungle" had one level
                // higher. Some sub-area pages *do* happen to self-reference in their own key (Noble Ledges'
                // "Area: Noble Ledges (Verdant Brink)"), but that's not a rule the wiki actually follows --
                // so a Location page's own Title is always added as a place name for its own link,
                // independent of the DescriptionList content (DerivedSubpageGenerator only sets Title for
                // the true Location type, not Item/Npc/Quest). AddPlace dedupes, so a page that also
                // self-references in its DescriptionList just adds the same name twice.
                if (!string.IsNullOrEmpty(subpage.Title))
                {
                    AddPlace(placesByLink, link, subpage.Title.Trim());
                }

                if (subpage.Places != null)
                {
                    foreach (var place in subpage.Places)
                    {
                        foreach (var name in ParsePlaceNames(place.Value))
                        {
                            AddPlace(placesByLink, link, name);
                        }
                    }
                }
            }

            this.continentCoordinatesByLink = byLink;
            this.placeNamesByLink = placesByLink;
            this.logger.Info($"WikiLocationService: indexed {byLink.Count} wiki subpage coordinate(s) and {placesByLink.Count} subpage place name(s).");
            return byLink;
        }

        private static void AddPlace(Dictionary<string, List<string>> placesByLink, string link, string place)
        {
            if (string.IsNullOrEmpty(place))
            {
                return;
            }

            if (!placesByLink.TryGetValue(link, out var existing))
            {
                existing = new List<string>();
                placesByLink[link] = existing;
            }

            if (!existing.Contains(place, StringComparer.OrdinalIgnoreCase))
            {
                existing.Add(place);
            }
        }

        // A DescriptionList value is wiki markup, not bare text -- checked against the real data before
        // writing this rather than assumed: "<a href=...>Name</a>" alone, or with a "<br><small>(<a
        // href=...>Parent</a>)</small>" tail naming the broader zone/region, which isn't a place of its
        // own and is dropped. A few (only the "Location" key, in the data checked) name several genuine
        // places <br>-separated with no parenthetical at all -- kept, one per segment.
        private static List<string> ParsePlaceNames(string raw)
        {
            var result = new List<string>();

            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            foreach (var segment in raw.Split(new[] { "<br>" }, StringSplitOptions.None))
            {
                var trimmedSegment = segment.Trim();

                if (trimmedSegment.Length == 0 || trimmedSegment.StartsWith("<small>", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var name = WebUtility.HtmlDecode(HtmlTagRegex.Replace(trimmedSegment, string.Empty)).Trim();

                if (name.Length > 0)
                {
                    result.Add(name);
                }
            }

            return result;
        }

        // The wiki stores these as a bare JSON-looking pair, e.g. "[48592.6, 42833.9]" -- continent
        // coordinates, invariant culture.
        internal static bool TryParseCoordinates(string raw, out Vector2 coordinate)
        {
            coordinate = default;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            var parts = raw.Trim().Trim('[', ']').Split(',');

            if (parts.Length != 2 ||
                !float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                return false;
            }

            coordinate = new Vector2(x, y);
            return true;
        }

        private class WikiRowLocation
        {
            public int Row { get; set; }

            // True when this row has an exact InteractiveMap coordinate (Phase 28); ContinentX/Y are only
            // meaningful then. False rows carry PlaceNames instead (Phase 29) and resolve, if at all,
            // against the current map's sectors.
            public bool HasCoordinate { get; set; }

            public float ContinentX { get; set; }

            public float ContinentY { get; set; }

            public IReadOnlyList<string> PlaceNames { get; set; }
        }
    }
}
