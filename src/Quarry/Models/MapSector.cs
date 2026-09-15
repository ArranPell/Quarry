using Microsoft.Xna.Framework;

namespace Quarry.Models
{
    // Phase 29: one map sector, from /v2/continents/.../maps/:id (the same fetch MapWaypoint comes from),
    // positioned the same way. Its Coord is a centroid, not a point an objective sits at -- callers using
    // it as a target must say so, not imply the precision of a real objective coordinate.
    public class MapSector
    {
        public string Name { get; set; }

        public Vector2 World { get; set; }
    }
}
