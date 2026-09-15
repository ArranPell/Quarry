using Quarry.WikiData.Achievement;
using Gw2Sharp.WebApi.V2.Models;

namespace Quarry.Models
{
    public class HereCandidate
    {
        public AchievementTableEntry Achievement { get; set; }

        public AchievementCategory Category { get; set; }

        public int Current { get; set; }

        public int Max { get; set; }

        public int AchievementPoints { get; set; }

        // True when the marker-pack index (Phase 15) has a guided route for this achievement on the
        // current map -- either instead of or alongside the category link.
        public bool Guided { get; set; }

        // Phase 26: which *kind* of guidance, computed for this map over remaining bits only. Guided
        // above stays as the plain "a pack covers this" fact the sort bonus uses.
        public GuidanceInfo Guidance { get; set; } = GuidanceInfo.None;

        // Phase 34, narrowed after the 2026-09-11 load-test: nothing can place this on the current map
        // -- no bits to enumerate *and* no guidance of any tier. The original "no bits alone" rule swept
        // up every single-objective achievement, marker-pack routes and all. Still structural, not a
        // keyword guess: keyword matching catches collections that merely mention defeating something.
        public bool Opportunistic { get; set; }
    }
}
