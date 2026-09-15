namespace Quarry.Models
{
    // Phase 17: one remaining (not-yet-finished) objective for a tracked achievement's guided route on
    // the current map, nearest-first. Bit/Row are -1 for the pooled "untagged objectives" entry.
    public class RemainingObjective
    {
        public int Bit { get; set; }

        public int Row { get; set; }

        public string Name { get; set; }

        public double DistanceMetres { get; set; }

        public string Namespace { get; set; }

        public bool IsTrail { get; set; }

        // The waypoint code tied to this specific objective, if it has one -- null otherwise. Lets the
        // Track window copy the waypoint nearest to the player instead of an arbitrary one.
        public string Waypoint { get; set; }

        // True when this objective's position came from a wiki coordinate, which is 2-D -- so
        // DistanceMetres is measured on the ground plane and doesn't move when you gain altitude.
        // Surfaced rather than hidden: standing on a target and flying 500 m up while the number holds
        // at 1 m reads as broken unless the readout says which distance it is.
        public bool GroundDistanceOnly { get; set; }

        // Phase 29: set when this objective's position is a sector centroid, to the sector's name. The
        // metre count is real but the target is an area, not a point, so the tooltip has to say that
        // rather than let a precise-looking number imply precision it hasn't got.
        public string AreaHint { get; set; }
    }
}
