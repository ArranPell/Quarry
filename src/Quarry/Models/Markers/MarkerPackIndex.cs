using System;
using System.Collections.Generic;

namespace Quarry.Models.Markers
{
    // Cache root for markerPackIndex.json (Services/MarkerPackIndexService.cs). Schema is bumped whenever
    // this shape changes, so an old cache file is rebuilt instead of failing to deserialize.
    public class MarkerPackIndexCacheFile
    {
        public int Schema { get; set; }

        public List<PackCacheEntry> Packs { get; set; } = new List<PackCacheEntry>();

        public Dictionary<int, AchievementRoute> AchievementsById { get; set; } = new Dictionary<int, AchievementRoute>();
    }

    // Identifies a pack file for cache invalidation -- rebuild only when one of these changes.
    public class PackCacheEntry
    {
        public string FileName { get; set; }

        public long Length { get; set; }

        public DateTime LastWriteTimeUtc { get; set; }
    }

    // One achievement's guided-route data, unioned across every installed pack that tags it.
    public class AchievementRoute
    {
        public HashSet<int> MapIds { get; set; } = new HashSet<int>();

        public List<AchievementObjective> Objectives { get; set; } = new List<AchievementObjective>();

        public HashSet<string> Waypoints { get; set; } = new HashSet<string>();

        public HashSet<string> Packs { get; set; } = new HashSet<string>();
    }

    // One marker or trail tagged (directly or via an ancestor category) with this achievement's id.
    // Xyz is the pack's raw coordinate space -- Pathing's (x, z, y) Vector3 transpose is Phase 17's job,
    // not the index's.
    public class AchievementObjective
    {
        public string Namespace { get; set; }

        public int Bit { get; set; }

        public int MapId { get; set; }

        public float X { get; set; }

        public float Y { get; set; }

        public float Z { get; set; }

        public bool IsTrail { get; set; }

        // This objective's own copy/copy-message waypoint code, if the POI had one -- lets a consumer
        // copy the waypoint tied to a *specific* objective rather than an arbitrary one from
        // AchievementRoute.Waypoints (Phase 17 fix: the copy icon was always copying the same code
        // regardless of which objective was actually nearest).
        public string Waypoint { get; set; }

        // Phase 28: true for a wiki coordinate, which is 2-D -- distance is measured in the horizontal
        // plane only rather than inventing a height. Named for the *unknown* case so a cached objective
        // written before this existed (bool default false) keeps meaning "height known", which is right
        // for every pack marker.
        public bool HeightUnknown { get; set; }

        // Phase 29: set only when this objective's position is a sector centroid rather than an exact
        // wiki coordinate -- an area, not a point. Null for everything else (pack markers, exact
        // InteractiveMap coordinates), including old cache entries, which is the right default: they ARE
        // exact. Carried through to RemainingObjective.AreaHint so the tooltip can say so.
        public string SectorName { get; set; }

        // Pack (default, and what the cache holds) or Wiki, which is computed at runtime and never cached.
        public ObjectiveSource Source { get; set; }
    }

    public enum ObjectiveSource
    {
        Pack = 0,

        Wiki = 1,
    }
}
