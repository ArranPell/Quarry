namespace Quarry.Models
{
    // Phase 27: how much guidance a Here candidate must have to be worth one of the bounded slots.
    // ArranPell's call was a threshold rather than a boolean (DECISIONS 2026-09-09), because once the badge
    // distinguishes tiers, "guided or not" is the least interesting cut through them.
    //
    // Each value IS the minimum GuidanceTier that passes, so the comparison needs no lookup table --
    // Everything maps to None, which every candidate clears.
    public enum HereGuidanceFilter
    {
        Everything = GuidanceTier.None,

        AnyGuidance = GuidanceTier.Area,

        CoordinatesOrBetter = GuidanceTier.Coordinate,

        TaggedOnly = GuidanceTier.Tagged,
    }
}
