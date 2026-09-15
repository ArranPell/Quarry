using Quarry.Interfaces;
using Quarry.Models;
using Quarry.WikiData.Achievement;
using System.Collections.Generic;
using System.Linq;

namespace Quarry.Services
{
    // Extracted from AchievementListItem (Phase 20) with no behaviour change, so the Track window
    // (and the compact rows in a later phase) can share the same progress lookup as the cards.
    public static class AchievementProgress
    {
        private const int MaxRemainingNamesShown = 3;
        private const int MaxNearestShown = 8;

        // nearest (Phase 17): when given (even empty), RemainingText becomes the nearest-first
        // "<name> — 123 m" list instead of the wiki-row-order collection text -- closes the Phase 15
        // "this map first" deferral. The four other call sites pass nothing and keep today's text.
        public static (int Current, int Max, string Text, string RemainingText, double Fraction) Get(IAchievementService achievementService, AchievementTableEntry achievement, IReadOnlyList<RemainingObjective> nearest = null)
        {
            achievementService.PlayerAchievementsById.TryGetValue(achievement.Id, out var playerAchievement);

            int current;
            int max;
            string text;

            if (playerAchievement is null)
            {
                current = 0;
                max = 0;
                text = "Not started";
            }
            else
            {
                current = playerAchievement.Current;
                max = playerAchievement.Max;
                text = max > 0 ? $"{current} / {max}" : null;
            }

            var fraction = max > 0 ? (double)current / max : 0;
            var remainingText = nearest != null
                ? FormatNearestText(nearest)
                : GetRemainingCollectionText(achievementService, achievement);

            return (current, max, text, remainingText, fraction);
        }

        // "<name> — 123 m" per remaining objective, nearest first, at most 8, then "(+n more)". Shared by
        // the compact-row tooltip above (via Get's nearest parameter) and the Track window's full-panel
        // Next label tooltip.
        public static string FormatNearestText(IReadOnlyList<RemainingObjective> nearest)
        {
            if (nearest.Count == 0)
            {
                return null;
            }

            // "(ground)" marks a wiki coordinate: 2-D, so the number is horizontal distance and won't
            // change with altitude. Saying so costs one word and stops a correct number looking wrong.
            // AreaHint (Phase 29) marks a sector centroid specifically: the number is real, but the target
            // is an area, not a point, so the metre count needs a sentence rather than one word or it
            // implies precision the wiki row never had.
            var lines = nearest.Take(MaxNearestShown).Select(o => o.AreaHint != null
                ? $"{o.Name} — {o.DistanceMetres:F0} m (ground). Distance to the centre of {o.AreaHint}; the objective is somewhere in that area."
                : $"{o.Name} — {o.DistanceMetres:F0} m{(o.GroundDistanceOnly ? " (ground)" : string.Empty)}");
            var text = string.Join("\n", lines);

            if (nearest.Count > MaxNearestShown)
            {
                text += $"\n(+{nearest.Count - MaxNearestShown} more)";
            }

            return text;
        }

        // "3 of 7 remaining: Name1, Name2, Name3 (+4 more)" for collection-type achievements, using the
        // same HasFinishedAchievementBit (and its specialSnowflakeCompletedHandling fix-ups) the Inspector's
        // chip grid (InspectorWindow.GetChipState) relies on -- not a new wiki/API bit-order
        // assumption. No location hint: checked achievement_data.json's CollectionDescriptionEntry, it
        // only has DisplayName/ImageUrl/Link/Id, nothing location-shaped.
        private static string GetRemainingCollectionText(IAchievementService achievementService, AchievementTableEntry achievement)
        {
            if (!(achievement.Description is CollectionDescription collectionDescription) || collectionDescription.EntryList.Count == 0)
            {
                return null;
            }

            var entries = collectionDescription.EntryList;
            var remainingNames = new List<string>();
            var remainingCount = 0;

            for (var i = 0; i < entries.Count; i++)
            {
                if (achievementService.HasFinishedAchievementBit(achievement.Id, i))
                {
                    continue;
                }

                remainingCount++;

                if (remainingNames.Count < MaxRemainingNamesShown)
                {
                    remainingNames.Add(entries[i].DisplayName);
                }
            }

            if (remainingCount == 0)
            {
                return null;
            }

            var names = string.Join(", ", remainingNames);

            if (remainingCount > remainingNames.Count)
            {
                names += $" (+{remainingCount - remainingNames.Count} more)";
            }

            return $"{remainingCount} of {entries.Count} remaining: {names}";
        }
    }
}
