using System;
using System.Collections.Generic;

namespace Quarry.Models
{
    public class HereResult
    {
        public HereResultReason Reason { get; set; }

        public IReadOnlyList<HereCandidate> Candidates { get; set; } = Array.Empty<HereCandidate>();

        // Phase 56 (review item 18): the same ranking before the cap. The Here tab shows Candidates (the
        // bounded list is the product); the Target List's strip takes its three from here after dropping
        // what's already tracked, so tracking the top N doesn't empty it.
        public IReadOnlyList<HereCandidate> RankedUncapped { get; set; } = Array.Empty<HereCandidate>();

        // True when at least one achievement-details batch failed to fetch -- Candidates is a partial
        // (or empty) answer for this pass, not "there's genuinely nothing here."
        public bool Partial { get; set; }

        // False when the current map has no category link at all (e.g. core Tyria) and every candidate
        // came from the pack index -- HereView uses this to pick "guided achievement(s) here" over the
        // usual "close to done" header. Meaningless when Reason != Ok.
        public bool CategorySupported { get; set; } = true;

        // Phase 27: how many candidates the guidance threshold removed on this pass. Non-zero with an
        // empty Candidates list is the difference between "nothing here" and "nothing here that clears
        // your filter" -- the product rule says say which.
        public int FilteredByGuidance { get; set; }

        // Phase 31: how many of *this map's* candidates were withheld because they're hidden or snoozed.
        // Feeds the header ("... 2 hidden") -- the product rule says say what was withheld rather than
        // just showing less. The cards themselves come from IHereService.GetHiddenAsync instead, which is
        // account-wide: the load-test found that a map-scoped hidden list left something hidden from the
        // Anywhere block with no way to ever reach it again.
        public int HiddenCount { get; set; }

        // Phase 34: achievements with no bits, pulled out of the ranked list *before* the cap so a slot
        // refills, and surfaced as a single "While you're here" line instead. Ordered by progress
        // fraction; the view names the first few and puts the rest in the tooltip.
        public IReadOnlyList<HereCandidate> Opportunistic { get; set; } = Array.Empty<HereCandidate>();

        // True when the marker-pack index has finished its first build. Only meaningful when
        // Reason == NoCategoryForMap -- lets HereView soften "not supported yet" once guided coverage
        // exists, even on a run with 0 guided candidates on this particular map.
        public bool IndexReady { get; set; }
    }
}
