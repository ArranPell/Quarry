using Blish_HUD;
using Blish_HUD.Modules.Managers;
using Quarry.Interfaces;
using System;
using Quarry.Models;
using Gw2Sharp.WebApi.V2.Models;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Quarry.Services
{
    public class CurrentMapService : ICurrentMapService
    {
        // GW2 map coordinates are in inches; Mumble (and therefore a marker pack, and therefore us) is
        // in metres.
        private const double InchesPerMetre = 39.3701;

        private readonly Gw2ApiManager gw2ApiManager;
        private readonly Logger logger;
        // The whole Map, not just its name: Phase 28 needs continent_rect/map_rect off the same fetch to
        // turn a wiki continent coordinate into a position we can measure against the player.
        private readonly ConcurrentDictionary<int, Map> mapCache = new ConcurrentDictionary<int, Map>();

        // Phase 30: the map's real waypoints, positioned. A marker pack's own waypoint annotations turned
        // out to be useless for "which waypoint is nearest my objective" -- they sit on scattered markers
        // up to 900 m from the waypoint they name (measured on Auric Basin), so the nearest annotated
        // marker routinely names a waypoint on the far side of the map. The API knows where waypoints
        // actually are; compute it instead of trusting the annotation.
        private readonly ConcurrentDictionary<int, IReadOnlyList<MapWaypoint>> waypointCache = new ConcurrentDictionary<int, IReadOnlyList<MapWaypoint>>();
        // Phase 29: the same fetch also has every sector's name and centroid -- wiki area rows resolve
        // against these. Kept alongside the waypoints rather than in a service of its own.
        private readonly ConcurrentDictionary<int, IReadOnlyList<MapSector>> sectorCache = new ConcurrentDictionary<int, IReadOnlyList<MapSector>>();
        private readonly ConcurrentDictionary<int, bool> floorDataFetchStarted = new ConcurrentDictionary<int, bool>();
        private readonly EventHandler<ValueEventArgs<int>> mapChangedHandler;

        public int MapId { get; private set; } = -1;

        public string MapName { get; private set; }

        public event Action Changed;

        public CurrentMapService(Gw2ApiManager gw2ApiManager, Logger logger)
        {
            this.gw2ApiManager = gw2ApiManager;
            this.logger = logger;

            // GameService.Gw2Mumble is a Blish-wide static, so this handler must be unsubscribed in
            // Dispose() -- otherwise it (and everything it closes over, all the way up through Module)
            // keeps running after the module is disabled instead of being garbage collected.
            this.mapChangedHandler = (s, e) => _ = this.UpdateMap(e.Value);
            GameService.Gw2Mumble.CurrentMap.MapChanged += this.mapChangedHandler;

            if (GameService.Gw2Mumble.IsAvailable)
            {
                _ = this.UpdateMap(GameService.Gw2Mumble.CurrentMap.Id);
            }
        }

        public bool TryGetWaypoints(int mapId, out IReadOnlyList<MapWaypoint> waypoints)
            => this.waypointCache.TryGetValue(mapId, out waypoints) && waypoints.Count > 0;

        public bool TryGetSectors(int mapId, out IReadOnlyList<MapSector> sectors)
            => this.sectorCache.TryGetValue(mapId, out sectors) && sectors.Count > 0;

        public bool TryGetMapName(int mapId, out string name)
        {
            if (this.mapCache.TryGetValue(mapId, out var map) && map != null)
            {
                name = map.Name;
                return true;
            }

            name = null;
            return false;
        }

        // One public, unauthenticated call per map, cached for the session. Failure is silent by design:
        // the copy icon falls back to the marker pack's own codes, which is what it used before this, and
        // wiki area rows simply can't resolve to a sector.
        //
        // Phase 29: some maps 404 on their own DefaultFloor -- Desert Highlands is one, found while
        // measuring for this phase -- so DefaultFloor is now just the first rung of a ladder over every
        // floor the map appears on, taking whichever answers first. Wasn't worth a retry ladder before
        // this phase gave sectors a reason to need one; it also fixes those maps' waypoints, which have
        // had none since Phase 30.
        private async Task EnsureFloorDataAsync(int mapId)
        {
            if (this.waypointCache.ContainsKey(mapId) || !this.floorDataFetchStarted.TryAdd(mapId, true))
            {
                return;
            }

            if (!this.mapCache.TryGetValue(mapId, out var map) || map is null)
            {
                return;
            }

            var floorsToTry = new List<int> { map.DefaultFloor };
            foreach (var floor in map.Floors)
            {
                if (!floorsToTry.Contains(floor))
                {
                    floorsToTry.Add(floor);
                }
            }

            Exception lastException = null;

            foreach (var floor in floorsToTry)
            {
                try
                {
                    var floorMap = await this.gw2ApiManager.Gw2ApiClient.V2.Continents[map.ContinentId]
                        .Floors[floor]
                        .Regions[map.RegionId]
                        .Maps
                        .GetAsync(mapId);

                    var waypoints = new List<MapWaypoint>();

                    foreach (var poi in floorMap.PointsOfInterest.Values)
                    {
                        if (poi.Type != PoiType.Waypoint || string.IsNullOrEmpty(poi.ChatLink))
                        {
                            continue;
                        }

                        if (this.TryContinentToWorld(mapId, poi.Coord.X, poi.Coord.Y, out var world))
                        {
                            waypoints.Add(new MapWaypoint { Name = poi.Name, ChatLink = poi.ChatLink, World = world });
                        }
                    }

                    var sectors = new List<MapSector>();

                    foreach (var sector in floorMap.Sectors.Values)
                    {
                        if (string.IsNullOrEmpty(sector.Name))
                        {
                            continue;
                        }

                        if (this.TryContinentToWorld(mapId, sector.Coord.X, sector.Coord.Y, out var world))
                        {
                            sectors.Add(new MapSector { Name = sector.Name, World = world });
                        }
                    }

                    this.waypointCache[mapId] = waypoints;
                    this.sectorCache[mapId] = sectors;

                    if (floor != map.DefaultFloor)
                    {
                        this.logger.Debug($"Map {mapId} 404'd on its default floor {map.DefaultFloor}; floor {floor} answered instead.");
                    }

                    this.logger.Debug($"Waypoints for map {mapId}: {waypoints.Count}, sectors: {sectors.Count} — {string.Join(", ", waypoints.Select(w => $"{w.Name} ({w.World.X:F0},{w.World.Y:F0})"))}");
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }
            }

            _ = this.floorDataFetchStarted.TryRemove(mapId, out _);
            this.logger.Warn(lastException, $"Couldn't fetch waypoints/sectors for map {mapId} on any of its {floorsToTry.Count} floor(s); the copy icon will use marker-pack codes instead.");
        }

        // Continent coordinate (what the wiki stores) -> Blish/Mumble world position, and simultaneously
        // the "is this point even on this map" test: a coordinate outside the map's continent rectangle
        // belongs to a different map, so no global continent->map index is needed.
        //
        // Verified empirically before being written, against four maps' pack data (Skywatch Archipelago,
        // Amnytas, The Echovald Wilds, Seitung Province): converting each map's API points of interest
        // this way landed 0.01-0.15 m from the marker a pack had placed on the same POI. The Y flip and
        // the inch->metre divisor are both load-bearing; getting either wrong yields plausible-looking
        // numbers, which is why it was checked rather than reasoned about.
        public bool TryContinentToWorld(int mapId, double continentX, double continentY, out Vector2 world)
        {
            world = default;

            if (!this.mapCache.TryGetValue(mapId, out var map) || map is null)
            {
                return false;
            }

            // Read the rectangles as min/max rather than by corner NAME. The raw API gives
            // continent_rect as [[left, top], [right, bottom]] and map_rect as [[left, bottom],
            // [right, top]] -- the Y ends are ordered oppositely -- so trusting a "TopLeft"/"BottomRight"
            // label to mean the same thing in both is how this got mirrored north-for-south, which made
            // the waypoint picker name Northwatch for an objective sitting at Southwatch (load-test,
            // 2026-09-10). Min/max is true regardless of which corner the model calls which.
            var continentRect = map.ContinentRect;
            var mapRect = map.MapRect;

            var continentLeft = Math.Min(continentRect.TopLeft.X, continentRect.BottomRight.X);
            var continentRight = Math.Max(continentRect.TopLeft.X, continentRect.BottomRight.X);
            var continentTop = Math.Min(continentRect.TopLeft.Y, continentRect.BottomRight.Y);
            var continentBottom = Math.Max(continentRect.TopLeft.Y, continentRect.BottomRight.Y);

            if (continentX < continentLeft || continentX > continentRight ||
                continentY < continentTop || continentY > continentBottom)
            {
                return false;
            }

            var continentWidth = continentRight - continentLeft;
            var continentHeight = continentBottom - continentTop;

            if (continentWidth <= 0 || continentHeight <= 0)
            {
                return false;
            }

            var mapLeft = Math.Min(mapRect.TopLeft.X, mapRect.BottomRight.X);
            var mapRight = Math.Max(mapRect.TopLeft.X, mapRect.BottomRight.X);
            var mapBottom = Math.Min(mapRect.TopLeft.Y, mapRect.BottomRight.Y);
            var mapTop = Math.Max(mapRect.TopLeft.Y, mapRect.BottomRight.Y);

            // Continent Y grows southward, map Y grows northward -- hence the flip.
            var mapX = mapLeft + ((continentX - continentLeft) / continentWidth * (mapRight - mapLeft));
            var mapY = mapBottom + ((continentBottom - continentY) / continentHeight * (mapTop - mapBottom));

            world = new Vector2((float)(mapX / InchesPerMetre), (float)(mapY / InchesPerMetre));
            return true;
        }

        public void Dispose()
            => GameService.Gw2Mumble.CurrentMap.MapChanged -= this.mapChangedHandler;

        private async Task UpdateMap(int mapId)
        {
            this.MapId = mapId;

            if (!this.mapCache.TryGetValue(mapId, out var cachedMap))
            {
                string mapName;

                try
                {
                    var map = await this.gw2ApiManager.Gw2ApiClient.V2.Maps.GetAsync(mapId);
                    mapName = map.Name;
                    this.mapCache[mapId] = map;
                }
                catch (Exception ex)
                {
                    this.logger.Warn(ex, $"Failed to resolve map data for map id {mapId}");
                    mapName = null;
                }

                // A newer map change landed while we were awaiting the API -- that call owns assigning
                // MapName and firing Changed for the map we're actually on now; don't stomp on it with
                // this stale result.
                if (this.MapId != mapId)
                {
                    return;
                }

                this.MapName = mapName;
            }
            else
            {
                this.MapName = cachedMap.Name;
            }

            // Fetched now rather than on the click, so the copy icon has an answer the moment it's needed.
            _ = this.EnsureFloorDataAsync(mapId);

            this.logger.Info($"Current map changed: Id={this.MapId} Name={this.MapName}");
            this.Changed?.Invoke();
        }
    }
}
