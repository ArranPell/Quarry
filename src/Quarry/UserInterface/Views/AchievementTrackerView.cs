using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Quarry.Interfaces;
using Quarry.WikiData.Achievement;
using Gw2Sharp.WebApi.V2.Models;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quarry.UserInterface.Views
{

    public class AchievementTrackerView : View
    {
        private readonly SemaphoreSlim searchSemaphore = new SemaphoreSlim(1, 1);

        private readonly IAchievementItemOverviewFactory achievementItemOverviewFactory;
        private readonly IAchievementService achievementService;
        private readonly ITextureService textureService;
        private readonly Action openTrackedWindow;
        private readonly IDictionary<MenuItem, AchievementCategory> menuItemCategories;
        private Menu menu;
        private ViewContainer selectedMenuItemView;

        private Task delayTask;
        private CancellationTokenSource delayCancellationToken;
        private TextBox searchBar;
        private Action apiAchievementsLoadedHandler;

        public AchievementTrackerView(
            IAchievementItemOverviewFactory achievementItemOverviewFactory,
            IAchievementService achievementService,
            ITextureService textureService,
            Action openTrackedWindow)
        {
            this.achievementItemOverviewFactory = achievementItemOverviewFactory;
            this.achievementService = achievementService;
            this.textureService = textureService;
            this.openTrackedWindow = openTrackedWindow;
            this.menuItemCategories = new Dictionary<MenuItem, AchievementCategory>();
        }

        protected override void Build(Container buildPanel)
        {
            const int trackedWindowButtonWidth = 110;

            this.searchBar = new TextBox()
            {
                PlaceholderText = "Search...",
                Width = Panel.MenuStandard.Size.X - trackedWindowButtonWidth - 5,
                Parent = buildPanel,
            };

            this.searchBar.TextChanged += this.SearchBar_TextChanged;

            var trackedWindowButton = new StandardButton()
            {
                Text = "Target List",
                Width = trackedWindowButtonWidth,
                Height = this.searchBar.Height,
                Location = new Point(this.searchBar.Width + 5, 0),
                Parent = buildPanel,
            };

            trackedWindowButton.Click += (s, e) => this.openTrackedWindow();

            var menuPanel = new Panel()
            {
                Title = "Achievements",
                ShowBorder = true,
                Width = Panel.MenuStandard.Size.X,
                Height = buildPanel.ContentRegion.Height - this.searchBar.Height - 10,
                Location = new Point(0, this.searchBar.Height + 10),
                Parent = buildPanel,
                CanScroll = true,
            };

            this.menu = new Menu()
            {
                Size = menuPanel.ContentRegion.Size,
                MenuItemHeight = 40,
                CanSelect = true,
                Parent = menuPanel,
            };

            this.selectedMenuItemView = new ViewContainer()
            {
                FadeView = true,
                Parent = buildPanel,
                Size = new Point(buildPanel.ContentRegion.Width - menuPanel.Width, menuPanel.Height),
                Location = new Point(menuPanel.Width, 0),
            };

            var apiErrorLabel = new Label()
            {
                Text = "Weren't able to gather needed information from the API or it is still ongoing. Consult the log for details. Retrying every 5 minutes",
                Parent = buildPanel,
                Font = GameService.Content.DefaultFont18,
                AutoSizeHeight = true,
                Width = 250,
                WrapText = true,
                TextColor = Microsoft.Xna.Framework.Color.Red,
            };

            apiErrorLabel.Visible = false;

            apiErrorLabel.Location = new Point(((this.selectedMenuItemView.Width - apiErrorLabel.Width) / 2) + this.selectedMenuItemView.Location.X, ((this.selectedMenuItemView.Height - apiErrorLabel.Height) / 2) + this.selectedMenuItemView.Location.Y);

            if (this.achievementService.AchievementGroups is null || this.achievementService.AchievementCategories is null)
            {
                this.apiAchievementsLoadedHandler = () =>
                {
                    // Fires on the API background thread; InitializeAchievementElements builds ~355
                    // MenuItems, so marshal to the main thread before touching any controls.
                    GameService.Overlay.QueueMainThreadUpdate(gameTime =>
                    {
                        apiErrorLabel.Visible = false;
                        this.searchBar.Enabled = true;
                        this.InitializeAchievementElements();
                    });
                };

                this.achievementService.ApiAchievementsLoaded += this.apiAchievementsLoadedHandler;

                this.searchBar.Enabled = false;
                apiErrorLabel.Visible = true;
            }
            else
            {
                this.InitializeAchievementElements();
            }
        }

        protected override void Unload()
        {
            if (this.apiAchievementsLoadedHandler != null)
            {
                this.achievementService.ApiAchievementsLoaded -= this.apiAchievementsLoadedHandler;
            }

            this.delayCancellationToken?.Cancel();
            this.delayCancellationToken?.Dispose();
            this.searchSemaphore.Dispose();

            base.Unload();
        }

        private void InitializeAchievementElements()
        {
            this.categories = this.achievementService.AchievementCategories.ToDictionary(x => x.Id, y => y);
            foreach (var group in this.achievementService.AchievementGroups.OrderBy(x => x.Order))
            {
                var menuItem = this.menu.AddMenuItem(group.Name);
                foreach (var category in group.Categories.Select(x => this.categories[x]).OrderBy(x => x.Order))
                {
                    var innerMenuItem = new MenuItem(category.Name, this.textureService.GetTexture(category.Icon))
                    {
                        Parent = menuItem
                    };

                    innerMenuItem.ItemSelected += (sender, e) =>
                    {
                        var menuItemCategory = this.menuItemCategories[(MenuItem)sender];
                        var achievements = this.achievementService.Achievements.Where(x => menuItemCategory.Achievements.Contains(x.Id));
                        this.selectedMenuItemView.Clear();
                        this.selectedMenuItemView.Show(
                            this.achievementItemOverviewFactory.Create(
                                achievements.Select(x => (menuItemCategory, x)),
                                menuItemCategory.Name));
                    };

                    this.menuItemCategories.Add(innerMenuItem, category);

                }
            }
        }

        private void SearchBar_TextChanged(object sender, System.EventArgs e)
        {
            if (!this.searchBar.Enabled)
            {
                return;
            }

            if (this.delayTask != null)
            {
                this.delayCancellationToken.Cancel();
                this.delayCancellationToken.Dispose();
                this.delayTask = null;
                this.delayCancellationToken = null;
            }

            // Phase 56 (review item 5): read the TextBox here, on the main thread, rather than from the
            // worker the search runs on.
            var searchText = this.searchBar.Text;

            this.delayCancellationToken = new CancellationTokenSource();
            this.delayTask = Task.Run(() => this.DelaySeach(searchText, this.delayCancellationToken.Token), this.delayCancellationToken.Token);
        }

        private async Task DelaySeach(string searchText, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(300, cancellationToken);
                await this.SearchAsync(searchText, cancellationToken);
            }
            catch (OperationCanceledException) { }
        }

        private Dictionary<AchievementCategory, IEnumerable<AchievementTableEntry>> achievementCache;
        private Dictionary<int, AchievementCategory> categories;

        private async Task SearchAsync(string searchText, CancellationToken cancellationToken = default)
        {
            await this.searchSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    GameService.Overlay.QueueMainThreadUpdate(gameTime =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            this.selectedMenuItemView.Clear();
                        }
                    });
                    return;
                }

                if (this.achievementCache is null)
                {
                    var achievements = new Dictionary<AchievementCategory, IEnumerable<AchievementTableEntry>>();

                    foreach (var item in this.categories.Values)
                    {
                        if (item.Achievements.Count > 0)
                        {
                            achievements[item] = this.achievementService.Achievements.Where(x => item.Achievements.Contains(x.Id));
                        }
                    }

                    this.achievementCache = achievements;
                }

                var searchedAchievements = new List<(AchievementCategory, AchievementTableEntry)>();

                foreach (var item in this.achievementCache)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (var categoryAchievement in item.Value.Where(x => x.Name.ToUpper().Contains(searchText.ToUpper())))
                    {
                        searchedAchievements.Add((item.Key, categoryAchievement));
                    }
                }

                // Phase 56 (review item 5): the filtering above is fine on the worker; showing the view
                // builds a Dropdown, a CardGrid and every card, which is main-thread work -- same hop the
                // ApiAchievementsLoaded handler in Build already makes.
                GameService.Overlay.QueueMainThreadUpdate(gameTime =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    this.selectedMenuItemView.Clear();
                    this.selectedMenuItemView.Show(this.achievementItemOverviewFactory.Create(searchedAchievements, searchText));
                });
            }
            finally
            {
                _ = this.searchSemaphore.Release();
            }
        }
    }
}
