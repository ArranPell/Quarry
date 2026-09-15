using System;

namespace Quarry.Interfaces
{
    public interface ICurrentMapService : IDisposable
    {
        int MapId { get; }

        string MapName { get; }

        event Action Changed;

        // Phase 28: continent coordinate -> world position on this map, false when the point isn't on
        // this map at all (which is also how a wiki coordinate's map is decided).
        bool TryContinentToWorld(int mapId, double continentX, double continentY, out Microsoft.Xna.Framework.Vector2 world);

        // Phase 30: the map's real waypoints with real positions, fetched on map change. False until the
        // fetch lands (or if it failed) -- callers fall back to marker-pack codes.
        bool TryGetWaypoints(int mapId, out System.Collections.Generic.IReadOnlyList<Quarry.Models.MapWaypoint> waypoints);

        // Phase 29: the map's sectors, positioned like the waypoints above -- same fetch, same fallback
        // (silent, empty until it lands). A sector's position is its centroid, not a precise objective.
        bool TryGetSectors(int mapId, out System.Collections.Generic.IReadOnlyList<Quarry.Models.MapSector> sectors);

        // Phase 29: the map's own name, for any mapId already seen this session -- not just the current
        // one -- off the same cache UpdateMap already populates. Used to tell "names this map, but no
        // matching sector" apart from "names some other map entirely".
        bool TryGetMapName(int mapId, out string name);
    }
}
