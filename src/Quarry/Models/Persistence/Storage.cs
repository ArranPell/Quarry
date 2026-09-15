using System;
using System.Collections.Generic;

namespace Quarry.Models.Persistence
{
    public class Storage
    {
        // The old AchievementInformation / ItemInformation dictionaries (detail-window positions) were
        // dropped with the legacy detail windows in Batch H Phase 53. System.Text.Json ignores unknown
        // members by default, so a persistanceStorage.json that still carries those keys loads cleanly.

        public Dictionary<int, List<int>> ManualCompletedAchievements { get; set; } = new Dictionary<int, List<int>>();

        public List<int> TrackedAchievements { get; set; } = new List<int>();

        // Phase 31: the Here list's "don't offer me this" state. Owned at runtime by
        // HereExclusionService (PersistenceService.Save() rebuilds Storage from the services), these are
        // only its on-disk shape. Snooze values are UTC instants, serialised ISO-8601 by System.Text.Json.
        public List<int> HiddenAchievements { get; set; } = new List<int>();

        public Dictionary<int, DateTime> SnoozedAchievements { get; set; } = new Dictionary<int, DateTime>();

        // Phase 16: namespaces HuntService flipped active for a tracked achievement's routes -- id -> the
        // namespaces *we* enabled (leaf + inactive ancestors), so untracking reverts only what we touched.
        public Dictionary<int, List<string>> HuntEnabledNamespaces { get; set; } = new Dictionary<int, List<string>>();

        public int TrackWindowLocationX { get; set; } = -1;

        public int TrackWindowLocationY { get; set; } = -1;

        public bool ShowTrackWindow { get; set; } = false;

        public int TrackWindowCompactWidth { get; set; } = -1;

        public int TrackWindowCompactHeight { get; set; } = -1;

        // Phase 42: the Quarry window's own chosen size, once CanResize lets it have one -- -1 means
        // "never resized", so the screen-fraction default in AchievementOverviewWindow still applies.
        public int OverviewWindowWidth { get; set; } = -1;

        public int OverviewWindowHeight { get; set; } = -1;
    }
}
