using System.Collections.Generic;

namespace Quarry.Models
{
    // Phase 48. The shape DerivedSubpageGenerator emits into ref/derived_subpages.json, keyed by
    // normalized link. Carries only what the module actually reads out of the 70 MB subPages.json:
    // WikiLocationService's coordinates/place-names/Title, and the Inspector's Description/ImageUrl for
    // subpages an achievement objective can actually link to. A subpage absent from either need isn't in
    // the file at all.
    public class DerivedSubpage
    {
        public string Coordinates { get; set; }

        // Only set for the true wiki "Location" page type (see WikiLocationService's Phase 29 comment on
        // why a Title is trusted as a place name independent of DescriptionList content).
        public string Title { get; set; }

        public List<DerivedSubpagePlace> Places { get; set; }

        // Pre-flattened to the handful of tags FormattedLabelHtmlService actually renders differently
        // from plain text (DerivedSubpageGenerator's FlattenToVisualTags) -- null when this subpage isn't
        // linked from any achievement objective row.
        public string Description { get; set; }

        public string ImageUrl { get; set; }
    }

    public class DerivedSubpagePlace
    {
        public string Key { get; set; }

        public string Value { get; set; }
    }
}
