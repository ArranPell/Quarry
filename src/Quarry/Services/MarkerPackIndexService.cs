using Blish_HUD;
using Blish_HUD.Modules.Managers;
using Quarry.Interfaces;
using Quarry.Models.Markers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TmfLib;
using TmfLib.Pathable;
using TmfLib.Prototype;
using TmfLib.Reader;

namespace Quarry.Services
{
    // Phase 15: builds an achievement id -> (maps, objectives, waypoints) table from every installed
    // marker pack, with no reference to Pathing at runtime -- it only reads the same shared markers
    // folder Pathing watches. Read-only, streaming (TmfLib.Pack), never extracts to disk.
    public class MarkerPackIndexService : IMarkerPackIndexService
    {
        private const string CacheFileName = "markerPackIndex.json";
        // Schema 5 (Phase 24 follow-up, 2026-09-09): trail points are sampled by distance rather than a
        // fixed count per section -- a validation launch showed the fixed count produced a 21 MB cache.
        // Schema 4 (Phase 24, 2026-09-09): trails are indexed from their own point data instead of the
        // xpos/ypos/zpos attributes they don't have -- every trail objective in a schema-3 cache sits at
        // (0,0,0), so those caches must be rebuilt, not reused. Schema 3 put each objective's Waypoint on
        // the objective itself; schema 2 fixed Bit = -1 ("no bit") vs 0 (a real bit index). Every bump
        // forces an old cache to rebuild rather than silently keep the wrong shape.
        private const int CacheSchema = 5;

        // A trail is a path, not a point, so "how far away is it" means "how far to the nearest point on
        // it" -- which needs samples along it, not one position. Sample by *distance* rather than by
        // count: a fixed count per section wastes two dozen points on a 10 m segment while leaving a
        // 500 m one coarse. Measured on four installed packs, a fixed 24 produced 113k objectives
        // and a 21 MB cache; spacing bounds the worst-case error instead, at a fraction of the size.
        //
        // 25 m means the reported distance to a trail is never wrong by more than ~12 m, which is well
        // inside "walk that way" precision for a line you can see drawn in the world anyway.
        private const float TrailSampleSpacingMetres = 25f;

        // Bounds pathological data (a section that wanders the whole map) rather than trusting the pack.
        private const int TrailMaxSamplesPerSection = 64;

        // Matches a Blish chat link, e.g. [&BOoAAAA=], the shape a pack's copy/copy-message attribute
        // uses for a waypoint code.
        private static readonly Regex WaypointCodeRegex = new Regex(@"\[&[A-Za-z0-9+/=]+\]", RegexOptions.Compiled);
        private static readonly string[] WaypointAttributeNames = { "copy", "copy-message" };

        private readonly DirectoriesManager directoriesManager;
        private readonly Logger logger;

        private readonly object indexLock = new object();
        private IReadOnlyDictionary<int, AchievementRoute> achievementsById = new Dictionary<int, AchievementRoute>();
        private IReadOnlyDictionary<int, IReadOnlyCollection<int>> achievementIdsByMapId = new Dictionary<int, IReadOnlyCollection<int>>();

        public bool Ready { get; private set; }

        public event Action Changed;

        public MarkerPackIndexService(DirectoriesManager directoriesManager, Logger logger)
        {
            this.directoriesManager = directoriesManager;
            this.logger = logger;
        }

        public bool TryGet(int achievementId, out AchievementRoute route)
        {
            lock (this.indexLock)
            {
                return this.achievementsById.TryGetValue(achievementId, out route);
            }
        }

        public IReadOnlyCollection<int> AchievementsOnMap(int mapId)
        {
            lock (this.indexLock)
            {
                return this.achievementIdsByMapId.TryGetValue(mapId, out var ids) ? ids : Array.Empty<int>();
            }
        }

        // The caller starts this with a bare `_ = Task.Run(...)` (DependencyInjectionContainer), so
        // nothing observes a fault: anything escaping here is lost, and hunt mode plus the Next line then
        // silently do nothing all session with no clue why. Enumerating the markers folder is the real
        // candidate -- one unreadable subdirectory throws UnauthorizedAccessException before any of the
        // per-pack handlers below get a chance.
        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await this.LoadCoreAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Module unloading mid-load; not an error.
            }
            catch (Exception ex)
            {
                this.logger.Warn(ex, "MarkerPackIndexService: indexing failed; hunt routes and nearest-objective distances will be unavailable this session.");
            }
        }

        private async Task LoadCoreAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var markersDir = this.directoriesManager.GetFullDirectoryPath(ModuleConstants.MarkerPacksDirectoryName);
            _ = Directory.CreateDirectory(markersDir);

            var packFiles = Directory.EnumerateFiles(markersDir, "*.taco", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(markersDir, "*.zip", SearchOption.AllDirectories))
                .ToList();

            var packCacheEntries = packFiles
                .Select(f => new FileInfo(f))
                .Select(fi => new PackCacheEntry { FileName = fi.FullName, Length = fi.Length, LastWriteTimeUtc = fi.LastWriteTimeUtc })
                .OrderBy(e => e.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var cacheFilePath = this.GetCacheFilePath();
            var cached = this.TryLoadCache(cacheFilePath);

            // The unpacked-dev-pack case (loose category/POI XML directly under the markers folder) has
            // no single file/mtime to key a cache on, so it isn't covered by PacksMatch below -- it's
            // reparsed every load regardless of cache hit, which is cheap when (as for every normal
            // install) there's nothing there to find.
            if (cached != null && PacksMatch(cached.Packs, packCacheEntries))
            {
                this.ApplyIndex(cached.AchievementsById);
                this.logger.Info($"MarkerPackIndexService: cache hit for {packCacheEntries.Count} pack(s) ({cached.AchievementsById.Count} achievement(s)), {stopwatch.ElapsedMilliseconds} ms.");
                return;
            }

            var achievementsById = new Dictionary<int, AchievementRoute>();
            var settings = new PackReaderSettings();
            settings.VenderPrefixes.Add("bh-");

            foreach (var file in packFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await this.IndexPackAsync(() => Pack.FromArchivedMarkerPack(file), Path.GetFileName(file), settings, achievementsById, cancellationToken);
            }

            await this.IndexPackAsync(() => Pack.FromDirectoryMarkerPack(markersDir), $"{markersDir} (unpacked)", settings, achievementsById, cancellationToken, quietIfEmpty: true);

            this.ApplyIndex(achievementsById);

            try
            {
                var cacheFile = new MarkerPackIndexCacheFile { Schema = CacheSchema, Packs = packCacheEntries, AchievementsById = achievementsById };
                _ = Directory.CreateDirectory(Path.GetDirectoryName(cacheFilePath));
                File.WriteAllText(cacheFilePath, JsonSerializer.Serialize(cacheFile));
            }
            catch (UnauthorizedAccessException ex)
            {
                // Phase 56 (review item 12): a denied write means the full TmfLib parse repeats every
                // start; Blish's dialog says so once instead of a Warn line per session.
                this.logger.Warn(ex, $"MarkerPackIndexService: access denied writing {CacheFileName}; the index will rebuild every load until this is fixed.");
                Blish_HUD.Debug.Contingency.NotifyFileSaveAccessDenied(cacheFilePath, "cache Quarry's marker-pack index");
            }
            catch (Exception ex)
            {
                this.logger.Warn(ex, $"MarkerPackIndexService: failed to write {CacheFileName}; the index will rebuild every load until this is fixed.");
            }

            this.logger.Info($"MarkerPackIndexService: indexed {packFiles.Count} pack(s), {achievementsById.Count} achievement(s) total, {stopwatch.ElapsedMilliseconds} ms.");
        }

        private void ApplyIndex(IReadOnlyDictionary<int, AchievementRoute> newAchievementsById)
        {
            var byMapId = new Dictionary<int, HashSet<int>>();

            foreach (var entry in newAchievementsById)
            {
                foreach (var mapId in entry.Value.MapIds)
                {
                    if (!byMapId.TryGetValue(mapId, out var ids))
                    {
                        ids = new HashSet<int>();
                        byMapId[mapId] = ids;
                    }

                    _ = ids.Add(entry.Key);
                }
            }

            lock (this.indexLock)
            {
                this.achievementsById = newAchievementsById;
                this.achievementIdsByMapId = byMapId.ToDictionary(kv => kv.Key, kv => (IReadOnlyCollection<int>)kv.Value);
                this.Ready = true;
            }

            // A throwing subscriber must not take the load down with it -- the index itself is already
            // applied by this point, and an exception escaping here would skip the cache write below and
            // leave the pack index rebuilding from scratch every session.
            try
            {
                this.Changed?.Invoke();
            }
            catch (Exception ex)
            {
                this.logger.Warn(ex, "MarkerPackIndexService: a Changed subscriber threw; the index is still applied.");
            }
        }

        // pack is a factory rather than an instance so a directory pack with nothing in it (the normal
        // case for anyone without an unpacked dev pack) can fail inside Pack.FromDirectoryMarkerPack
        // itself without that being treated as a load error.
        private async Task IndexPackAsync(Func<Pack> packFactory, string displayName, PackReaderSettings settings, Dictionary<int, AchievementRoute> achievementsById, CancellationToken cancellationToken, bool quietIfEmpty = false)
        {
            var stopwatch = Stopwatch.StartNew();
            Pack pack;

            try
            {
                pack = packFactory();
            }
            catch (Exception ex)
            {
                if (!quietIfEmpty)
                {
                    this.logger.Warn(ex, $"MarkerPackIndexService: failed to open pack '{displayName}'; skipping it this run.");
                }

                return;
            }

            IPackCollection collection;

            try
            {
                // TmfLib.PackCollection (the concrete IPackCollection it hands back) has no public
                // constructor -- Pathing's own PackInitiator doesn't call `new PackCollection()` either,
                // it passes in its own IPackCollection (SharedPackCollection) for LoadAllAsync/LoadMapAsync
                // to populate. MutablePackCollection below mirrors that shape.
                collection = await pack.LoadAllAsync(new MutablePackCollection(), settings);
            }
            catch (Exception ex)
            {
                this.logger.Warn(ex, $"MarkerPackIndexService: failed to load pack '{displayName}'; skipping it this run. If it's locked by another process (Pathing mid-load), it'll be retried next start.");
                return;
            }
            finally
            {
                pack.ReleaseLocks();
            }

            var achievementIdsInPack = new HashSet<int>();
            var objectiveCount = 0;
            var untaggedCount = 0;
            var trailMapZeroCount = 0;

            foreach (var poi in collection.PointsOfInterest)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var attributes = poi.GetAggregatedAttributes();

                if (!attributes.TryGetAttribute("achievementid", out var idAttribute) ||
                    !InvariantParseUtil.TryParseInt(idAttribute.Value, out var achievementId))
                {
                    untaggedCount++;
                    continue;
                }

                // -1 means "no achievementbit attribute" -- Phase 17's untagged-objective pooling relies
                // on this being distinct from a real (0-based) bit index.
                var bit = -1;
                if (attributes.TryGetAttribute("achievementbit", out var bitAttribute))
                {
                    _ = InvariantParseUtil.TryParseInt(bitAttribute.Value, out bit);
                }

                if (!achievementsById.TryGetValue(achievementId, out var route))
                {
                    route = new AchievementRoute();
                    achievementsById[achievementId] = route;
                }

                // This POI's own waypoint code (if any) -- kept on the objective itself, not just rolled
                // into route.Waypoints below, so a consumer can copy the waypoint tied to whichever
                // objective is actually relevant rather than an arbitrary one for the achievement.
                string objectiveWaypoint = null;

                foreach (var waypointAttributeName in WaypointAttributeNames)
                {
                    if (attributes.TryGetAttribute(waypointAttributeName, out var waypointAttribute))
                    {
                        foreach (Match match in WaypointCodeRegex.Matches(waypointAttribute.Value ?? string.Empty))
                        {
                            _ = route.Waypoints.Add(match.Value);
                            objectiveWaypoint = objectiveWaypoint ?? match.Value;
                        }
                    }
                }

                _ = route.Packs.Add(displayName);

                var categoryNamespace = poi.ParentPathingCategory?.Namespace;
                int added;

                if (poi is Trail trail)
                {
                    added = AddTrailObjectives(trail, route, categoryNamespace, bit, objectiveWaypoint, ref trailMapZeroCount);
                }
                else
                {
                    var x = 0f;
                    var y = 0f;
                    var z = 0f;

                    if (attributes.TryGetAttribute("xpos", out var xAttr))
                    {
                        _ = InvariantParseUtil.TryParseFloat(xAttr.Value, out x);
                    }

                    if (attributes.TryGetAttribute("ypos", out var yAttr))
                    {
                        _ = InvariantParseUtil.TryParseFloat(yAttr.Value, out y);
                    }

                    if (attributes.TryGetAttribute("zpos", out var zAttr))
                    {
                        _ = InvariantParseUtil.TryParseFloat(zAttr.Value, out z);
                    }

                    _ = route.MapIds.Add(poi.MapId);
                    route.Objectives.Add(new AchievementObjective
                    {
                        Namespace = categoryNamespace,
                        Bit = bit,
                        MapId = poi.MapId,
                        X = x,
                        Y = y,
                        Z = z,
                        IsTrail = false,
                        Waypoint = objectiveWaypoint,
                    });

                    added = 1;
                }

                if (added == 0)
                {
                    continue;
                }

                _ = achievementIdsInPack.Add(achievementId);
                objectiveCount += added;
            }

            if (objectiveCount == 0 && quietIfEmpty)
            {
                return;
            }

            this.logger.Info($"Pack index: {displayName} — {achievementIdsInPack.Count} achievements, {objectiveCount} objectives, {stopwatch.ElapsedMilliseconds} ms (untagged POIs: {untaggedCount}, trail MapId=0: {trailMapZeroCount}).");
        }

        // A Trail has no xpos/ypos/zpos attributes at all -- its geometry lives in TrailSections, each
        // carrying its own MapId and a list of System.Numerics.Vector3 points in the same coordinate
        // space a marker's xpos/ypos/zpos uses. Before Phase 24 the attribute lookups simply failed for
        // trails and every trail objective was indexed at (0,0,0), so its "distance" was really the
        // player's distance from the map origin -- a number that moves as you walk but has nothing to do
        // with the trail. That is the root cause behind the Auric Basin case the tagged-over-untagged
        // rule was written for: an untagged pack's trail kept winning "nearest" with a made-up
        // distance. Sampling the points (rather than keeping one per objective) is what lets the
        // existing "nearest objective" code answer "how far to this trail" with no changes of its own.
        private static int AddTrailObjectives(Trail trail, AchievementRoute route, string categoryNamespace, int bit, string waypoint, ref int trailMapZeroCount)
        {
            var added = 0;

            foreach (var section in trail.TrailSections ?? Enumerable.Empty<ITrailSection>())
            {
                // A section's own MapId is the reliable one; Trail.MapId is 0 on plenty of real packs
                // (which is what the old trailMapZeroCount was counting without acting on).
                var mapId = section.MapId != 0 ? section.MapId : trail.MapId;

                if (mapId == 0)
                {
                    trailMapZeroCount++;
                    continue;
                }

                foreach (var point in SampleTrailPoints(section.TrailPoints))
                {
                    _ = route.MapIds.Add(mapId);
                    route.Objectives.Add(new AchievementObjective
                    {
                        Namespace = categoryNamespace,
                        Bit = bit,
                        MapId = mapId,
                        X = point.X,
                        Y = point.Y,
                        Z = point.Z,
                        IsTrail = true,
                        Waypoint = waypoint,
                    });

                    added++;
                }
            }

            return added;
        }

        // Keeps the first point, then one roughly every TrailSampleSpacingMetres along the path, then the
        // last -- so a section's ends stay exact and its middle is covered to a known tolerance.
        private static IEnumerable<System.Numerics.Vector3> SampleTrailPoints(IEnumerable<System.Numerics.Vector3> points)
        {
            var all = points?.ToList();

            if (all is null || all.Count == 0)
            {
                yield break;
            }

            var kept = 1;
            var last = all[0];
            yield return last;

            var sinceKept = 0f;

            for (var i = 1; i < all.Count; i++)
            {
                sinceKept += System.Numerics.Vector3.Distance(last, all[i]);
                last = all[i];

                if (sinceKept < TrailSampleSpacingMetres || kept >= TrailMaxSamplesPerSection)
                {
                    continue;
                }

                sinceKept = 0f;
                kept++;
                yield return all[i];
            }

            // The final point only matters if the walk above didn't just emit it.
            if (all.Count > 1 && sinceKept > 0f && kept < TrailMaxSamplesPerSection)
            {
                yield return all[all.Count - 1];
            }
        }

        private string GetCacheFilePath()
            => Path.Combine(this.directoriesManager.GetFullDirectoryPath(ModuleConstants.DataDirectoryName), CacheFileName);

        private MarkerPackIndexCacheFile TryLoadCache(string cacheFilePath)
        {
            if (!File.Exists(cacheFilePath))
            {
                return null;
            }

            try
            {
                var cache = JsonSerializer.Deserialize<MarkerPackIndexCacheFile>(File.ReadAllText(cacheFilePath));
                return cache?.Schema == CacheSchema ? cache : null;
            }
            catch (Exception ex)
            {
                this.logger.Warn(ex, $"MarkerPackIndexService: failed to read {CacheFileName}; rebuilding the index.");
                return null;
            }
        }

        // TmfLib.PackCollection's only constructor is internal -- consumers are expected to supply their
        // own IPackCollection for LoadAllAsync to populate (confirmed against Pathing's own
        // SharedPackCollection in PackInitiator.cs).
        private class MutablePackCollection : IPackCollection
        {
            public PathingCategory Categories { get; } = new PathingCategory(true);

            public IList<PointOfInterest> PointsOfInterest { get; } = new List<PointOfInterest>();
        }

        private static bool PacksMatch(IReadOnlyList<PackCacheEntry> cached, IReadOnlyList<PackCacheEntry> current)
        {
            if (cached.Count != current.Count)
            {
                return false;
            }

            for (var i = 0; i < cached.Count; i++)
            {
                var a = cached[i];
                var b = current[i];

                if (!string.Equals(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase) ||
                    a.Length != b.Length ||
                    a.LastWriteTimeUtc != b.LastWriteTimeUtc)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
