using Quarry.Models.Markers;
using System.Collections.Generic;

namespace Quarry.Interfaces
{
    // Phase 28: wiki-subpage coordinates for achievements a marker pack doesn't tag. See
    // Services/WikiLocationService.cs for why the coverage numbers make this worth having.
    public interface IWikiLocationService
    {
        // True when any row of this achievement links a subpage carrying a coordinate, regardless of map
        // or completion -- the map-independent "could this ever produce a Next line" question the Track
        // window asks once, when it builds a panel.
        bool HasAnyLocations(int achievementId);

        // Unfinished, wiki-located objectives for this achievement on this map, shaped exactly like pack
        // objectives so the nearest-objective code needs no second path. Includes both exact wiki
        // coordinates (Phase 28) and sector-centroid matches (Phase 29). Empty when the achievement has no
        // wiki location data, none of it resolves on this map, or the located steps are already done.
        IReadOnlyList<AchievementObjective> GetRemainingOnMap(int achievementId, int mapId);

        // Phase 29: true when this achievement has a remaining, unfinished row that names this map's own
        // zone but matched no sector -- GuidanceTier.Area, the "somewhere here, no exact spot" case. Only
        // meaningful to call once GetRemainingOnMap is empty: a row that matched a sector already resolved
        // one tier better, through GetRemainingOnMap, and checking again here would double-count it.
        bool HasAreaOnlyRemaining(int achievementId, int mapId);
    }
}
