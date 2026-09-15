using Microsoft.Xna.Framework;

namespace Quarry.Models
{
    // Phase 26. What the badge measures is **how well we can tell you what's LEFT here** -- deliberately
    // not "how good the route is". That distinction is the whole reason trail-only is neutral rather than
    // red (ArranPell's call, DECISIONS 2026-09-09): a drawn breadcrumb is often the nicest thing to walk, it
    // just can't tell you which parts you've already done.
    //
    // Ordered worst to best so a higher value is always a better answer to "what remains", which is what
    // the Here threshold filter compares against.
    public enum GuidanceTier
    {
        None = 0,

        // Phase 29: a zone or area name from the wiki subpage. Somewhere to go, no distance to count down.
        Area = 1,

        // Untagged pack markers or trails. Visible in the world, but carry no achievement bit, so nothing
        // disappears as you finish it and we can't subtract what's done.
        Route = 2,

        // Phase 28: a wiki coordinate. Attributed to a specific bit through BitAlignmentService, so unlike
        // an untagged pack marker it does respect completion -- just with no in-game icon.
        Coordinate = 3,

        // Bit-tagged pack markers: the icons vanish as you finish each one, and so does our list.
        Tagged = 4,
    }

    // One achievement's guidance on one map, computed over its *remaining* bits -- the same achievement
    // can be Tagged here and Route one map over.
    public class GuidanceInfo
    {
        public static readonly GuidanceInfo None = new GuidanceInfo();

        public GuidanceTier Tier { get; set; }

        // Remaining bit-tagged objectives on this map. Not a marker count: sampled trail points would
        // make that meaningless (see MarkerPackIndexService.TrailSampleCount).
        public int RemainingTagged { get; set; }

        public bool HasRoute { get; set; }

        public bool HasTrail { get; set; }

        // Glyph as well as colour, and a distinct word for each tier: red/green is the worst possible
        // pair for the commonest colour-vision deficiency, and this row already spends colour on the
        // progress fill and the near-done tint. ASCII glyphs only -- Blish's bitmap fonts don't carry the
        // decorative Unicode a nicer symbol would need.
        public string Label
        {
            get
            {
                switch (this.Tier)
                {
                    case GuidanceTier.Tagged: return "* Guided";
                    case GuidanceTier.Coordinate: return "+ Coords";
                    case GuidanceTier.Route: return "~ Route";
                    case GuidanceTier.Area: return "· Area";
                    default: return null;
                }
            }
        }

        public Color Color
        {
            get
            {
                switch (this.Tier)
                {
                    case GuidanceTier.Tagged: return GuidanceStyle.Tagged;
                    case GuidanceTier.Coordinate: return GuidanceStyle.Coordinate;
                    case GuidanceTier.Route: return GuidanceStyle.Route;
                    default: return GuidanceStyle.Area;
                }
            }
        }

        // Says what the tier actually means for hunting, since the label alone can't. Deliberately
        // qualitative about route markers: after Phase 24 a trail is stored as sampled points, so any
        // count we quoted for it would be a number about our sampling, not about the world.
        public string Describe()
        {
            switch (this.Tier)
            {
                case GuidanceTier.Tagged:
                    var objectives = this.RemainingTagged == 1 ? "objective" : "objectives";
                    var tagged = $"{this.RemainingTagged} {objectives} left here, each tracked on its own — the markers disappear as you finish them.";
                    return this.HasRoute ? tagged + "\nA route without completion data also covers this map." : tagged;

                case GuidanceTier.Coordinate:
                    return "Locations come from the wiki, not a marker pack — a distance to count down, but no icon in the world.";

                case GuidanceTier.Route:
                    return this.HasTrail
                        ? "A trail is drawn across this map, but it carries no achievement data — it won't shorten as you finish steps."
                        : "Markers cover this map, but they carry no achievement data — they won't disappear as you finish steps.";

                case GuidanceTier.Area:
                    return "The wiki names an area for this, but no exact spot — no distance to count down.";

                default:
                    return null;
            }
        }
    }

    // Every guidance colour in one place. When the UI-refresh backlog item defines the module's token
    // set, this is the single site it replaces -- which is why the badge was allowed to ship before it
    // (DECISIONS 2026-09-09).
    public static class GuidanceStyle
    {
        public static readonly Color Tagged = new Color(126, 200, 120);

        public static readonly Color Coordinate = new Color(212, 175, 55);

        // Neutral on purpose: a trail is not a failure, it just answers a different question.
        public static readonly Color Route = new Color(150, 175, 205);

        public static readonly Color Area = new Color(155, 155, 155);
    }
}
