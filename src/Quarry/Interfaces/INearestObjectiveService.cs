using Quarry.Models;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Quarry.Interfaces
{
    public interface INearestObjectiveService
    {
        // Distinct-bit remaining objectives for achievementId's route on mapId, nearest first. Empty when
        // the achievement has no route, has no objectives on this map, or nothing remains here. Computed
        // on demand -- no cache, cheap for one route's few hundred points.
        IReadOnlyList<RemainingObjective> GetRemaining(int achievementId, int mapId, Vector3 player);

        // How well this achievement's remaining work on this map is guided -- what the Here card's badge
        // shows and what the guided threshold filters on. Same data and same tagged/untagged rule as
        // GetRemaining, which is why it lives on the same service.
        GuidanceInfo GetGuidance(int achievementId, int mapId);

        // Whether this achievement can produce objectives from ANY source -- a marker pack or wiki
        // coordinates. Map-independent on purpose: the Track window asks it once when building a panel,
        // to decide whether the "Next:" line exists at all.
        bool HasAnyObjectives(int achievementId);

        IReadOnlyList<string> Waypoints(int achievementId);

        // The waypoint to take to reach whatever is left nearest on this map -- measured from that
        // objective, not from the player, and chosen from the map's REAL waypoints (the API knows where
        // they are). A marker pack's own waypoint annotations sit on markers hundreds of metres from the
        // waypoint they name, so they're only the fallback when the API hasn't answered. Null when
        // neither source can offer one.
        WaypointSuggestion NearestWaypoint(int achievementId, int mapId, Vector3 player);
    }
}
