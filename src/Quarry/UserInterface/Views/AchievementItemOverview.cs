using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Quarry.UserInterface.Controls;
using Quarry.Interfaces;
using Quarry.WikiData.Achievement;
using Gw2Sharp.WebApi.V2.Models;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Quarry.UserInterface.Views
{
    public class AchievementItemOverview : View
    {
        private const string SortByName = "Name";
        private const string SortByNearestToDone = "Nearest to done";

        // Static so the choice persists for the Blish session across every category/search opened -- not saved to Storage.
        private static string currentSortMode = SortByName;

        private readonly IAchievementService achievementService;
        private readonly IEnumerable<(AchievementCategory Category, AchievementTableEntry Achievement)> achievements;
        private readonly IAchievementCardFactory achievementCardFactory;
        private readonly IBitAlignmentService bitAlignmentService;
        private readonly string title;

        private CardGrid panel;
        private bool apiDataRequested;
        // Blish's Control exposes Disposed as an event, not a flag -- this is the flag.
        private bool panelDisposed;

        public AchievementItemOverview(
            IEnumerable<(AchievementCategory, AchievementTableEntry)> achievements,
            string title,
            IAchievementService achievementService,
            IAchievementCardFactory achievementCardFactory,
            IBitAlignmentService bitAlignmentService)
        {
            this.achievements = achievements;
            this.title = title;
            this.achievementService = achievementService;
            this.achievementCardFactory = achievementCardFactory;
            this.bitAlignmentService = bitAlignmentService;
        }

        protected override void Build(Container buildPanel)
        {
            var sortDropdown = new Dropdown()
            {
                Parent = buildPanel,
                Width = 160,
                Height = 30,
            };

            sortDropdown.Items.Add(SortByName);
            sortDropdown.Items.Add(SortByNearestToDone);
            sortDropdown.SelectedItem = currentSortMode;

            this.panel = new CardGrid()
            {
                Title = this.title,
                ShowBorder = true,
                Parent = buildPanel,
                Location = new Point(0, sortDropdown.Height),
                // Phase 42: Fill instead of a fixed pixel Size, so the grid tracks the window's
                // ContentRegion when the user drags the Quarry window's corner (CanResize, Phase 42).
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.Fill,
                CanScroll = true,
            };

            this.panel.Disposed += (s, e) => this.panelDisposed = true;

            this.Populate();
            this.EnsureApiDataForSort();

            sortDropdown.ValueChanged += (s, e) =>
            {
                currentSortMode = e.CurrentValue;
                this.Populate();
                this.EnsureApiDataForSort();
            };
        }

        private void Populate()
        {
            this.panel.ClearItems();

            foreach (var achievement in this.GetOrderedAchievements())
            {
                var card = this.achievementCardFactory.Create(achievement.Achievement, achievement.Category.Icon);
                card.Size = this.panel.CardSize;
                this.panel.Add(card);
            }
        }

        // Completion-first grouping stays fixed; the dropdown only changes how incomplete achievements
        // are ordered within that group. Phase 25 gave this view the API data it previously lacked
        // (BitAlignmentService's shared id->Achievement cache), so "Nearest to done" now matches the Here
        // ranking: IgnoreNearlyComplete demoted, then progress, then achievement points as the tie-break.
        private IEnumerable<(AchievementCategory Category, AchievementTableEntry Achievement)> GetOrderedAchievements()
        {
            var withCompletion = this.achievements.Select(x => (Done: this.achievementService.HasFinishedAchievement(x.Achievement.Id), x.Category, x.Achievement));

            if (currentSortMode == SortByNearestToDone)
            {
                return withCompletion
                    .OrderBy(x => x.Done)
                    .ThenBy(x => this.IgnoresNearlyComplete(x.Achievement.Id))
                    .ThenByDescending(x => this.GetProgressRatio(x.Achievement.Id))
                    .ThenByDescending(x => this.GetAchievementPoints(x.Achievement.Id))
                    .ThenBy(x => x.Achievement.Name)
                    .Select(x => (x.Category, x.Achievement));
            }

            return withCompletion
                .OrderBy(x => x.Done)
                .ThenBy(x => x.Category.Name)
                .ThenBy(x => x.Achievement.Name)
                .Select(x => (x.Category, x.Achievement));
        }

        // ANet's own "don't show this as nearly done" hint (424 of the 1,112 table achievements carry it,
        // measured 2026-09-09) -- repeatable dailies and the like, whose progress number says nothing
        // about being close to finished. Demoted rather than hidden: unlike Here, a category listing is a
        // browse surface, and hiding an achievement the user navigated to would be wrong.
        private bool IgnoresNearlyComplete(int achievementId)
            => this.bitAlignmentService.TryGetCachedAchievement(achievementId, out var apiAchievement)
                && apiAchievement.Flags.Select(f => f.Value).Contains(AchievementFlag.IgnoreNearlyComplete);

        // AP for an achievement is the sum of its tiers' points -- NOT tiers[].count, which is the
        // progress threshold.
        private int GetAchievementPoints(int achievementId)
            => this.bitAlignmentService.TryGetCachedAchievement(achievementId, out var apiAchievement)
                ? apiAchievement.Tiers?.Sum(t => t.Points) ?? 0
                : 0;

        // Both of the above read a cache only, so the first paint of a category can order without API
        // data rather than blocking on it. This fills the cache in the background and re-sorts once, and
        // only when the sort that actually uses it is selected.
        private void EnsureApiDataForSort()
        {
            if (this.apiDataRequested || currentSortMode != SortByNearestToDone)
            {
                return;
            }

            this.apiDataRequested = true;
            var ids = this.achievements.Select(x => x.Achievement.Id).Distinct().ToList();

            _ = Task.Run(async () =>
            {
                _ = await this.bitAlignmentService.GetAchievementsAsync(ids);
                Blish_HUD.GameService.Overlay.QueueMainThreadUpdate(gameTime =>
                {
                    if (this.panel != null && !this.panelDisposed)
                    {
                        this.Populate();
                    }
                });
            });
        }

        private double GetProgressRatio(int achievementId)
        {
            this.achievementService.PlayerAchievementsById.TryGetValue(achievementId, out var playerAchievement);

            if (playerAchievement is null || playerAchievement.Max <= 0)
            {
                return 0;
            }

            return (double)playerAchievement.Current / playerAchievement.Max;
        }
    }
}
