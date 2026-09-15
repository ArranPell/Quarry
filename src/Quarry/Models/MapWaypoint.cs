using Microsoft.Xna.Framework;

namespace Quarry.Models
{
    // Phase 30: one real waypoint on a map, from /v2/continents/.../maps/:id, with its continent
    // coordinate already converted to the world space the player and marker packs live in.
    public class MapWaypoint
    {
        public string Name { get; set; }

        // The [&B…] chat link the API hands us -- exactly what the player needs to paste.
        public string ChatLink { get; set; }

        public Vector2 World { get; set; }
    }
}
