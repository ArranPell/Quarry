using Blish_HUD;
using Blish_HUD.Modules.Managers;
using Quarry.Interfaces;
using Quarry.UserInterface.Windows;
using Quarry.WikiData.Achievement;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Quarry.Services
{
    // Phase 45. Owns one "default" (unpinned) InspectorWindow, reused by replacing its content on every
    // Show* call -- a pinned copy stops being the default and lives on its own, closed the normal way.
    public class InspectorWindowManager : IInspectorWindowManager
    {
        private readonly GraphicsService graphicsService;
        private readonly ContentsManager contentsManager;
        private readonly IAchievementService achievementService;
        private readonly IWikiSubpageDataService wikiSubpageDataService;
        private readonly IBitAlignmentService bitAlignmentService;
        private readonly IHuntService huntService;
        private readonly INearestObjectiveService nearestObjectiveService;
        private readonly ICurrentMapService currentMapService;
        private readonly IFormattedLabelHtmlService formattedLabelHtmlService;
        private readonly IExternalImageService externalImageService;
        private readonly IAchievementTrackerService achievementTrackerService;
        private readonly List<InspectorWindow> pinnedWindows = new List<InspectorWindow>();

        // ArranPell, 2026-09-13: windows Update() hid for the world-map-open rule, not by the user closing
        // them -- a window's own Hidden handler checks this before disposing, the same guard
        // the old detail-window manager used for the same reason (retired in Phase 53).
        private readonly HashSet<InspectorWindow> forciblyHidden = new HashSet<InspectorWindow>();

        private InspectorWindow defaultWindow;

        public InspectorWindowManager(
            GraphicsService graphicsService,
            ContentsManager contentsManager,
            IAchievementService achievementService,
            IWikiSubpageDataService wikiSubpageDataService,
            IBitAlignmentService bitAlignmentService,
            IHuntService huntService,
            INearestObjectiveService nearestObjectiveService,
            ICurrentMapService currentMapService,
            IFormattedLabelHtmlService formattedLabelHtmlService,
            IExternalImageService externalImageService,
            IAchievementTrackerService achievementTrackerService)
        {
            this.graphicsService = graphicsService;
            this.contentsManager = contentsManager;
            this.achievementService = achievementService;
            this.wikiSubpageDataService = wikiSubpageDataService;
            this.bitAlignmentService = bitAlignmentService;
            this.huntService = huntService;
            this.nearestObjectiveService = nearestObjectiveService;
            this.currentMapService = currentMapService;
            this.formattedLabelHtmlService = formattedLabelHtmlService;
            this.externalImageService = externalImageService;
            this.achievementTrackerService = achievementTrackerService;
        }

        public void ShowAchievement(AchievementTableEntry achievement)
        {
            var window = this.GetOrCreateDefaultWindow();
            window.SetAchievement(achievement);
            window.Show();
        }

        public void NotifyPinned(InspectorWindow window)
        {
            this.pinnedWindows.Add(window);

            if (this.defaultWindow == window)
            {
                this.defaultWindow = null;
            }

            // Load-test 2026-09-13: pinning was invisible -- the pinned window's Id-based SavesPosition
            // means the *next* fresh default window restores the exact same saved spot and lands right on
            // top of it, so nothing looked different. Move the pinned copy itself, once, so the click has
            // a visible effect regardless of where the next default window ends up.
            window.Location = new Point(window.Location.X + 30, window.Location.Y + 30);

            window.Hidden += (s, e) =>
            {
                if (this.forciblyHidden.Contains(window))
                {
                    return;
                }

                _ = this.pinnedWindows.Remove(window);
                window.Dispose();
            };
        }

        private InspectorWindow GetOrCreateDefaultWindow()
        {
            if (this.defaultWindow != null)
            {
                return this.defaultWindow;
            }

            var window = new InspectorWindow(
                this.contentsManager,
                this.achievementService,
                this.wikiSubpageDataService,
                this.bitAlignmentService,
                this.huntService,
                this.nearestObjectiveService,
                this.currentMapService,
                this.formattedLabelHtmlService,
                this.externalImageService,
                this.achievementTrackerService,
                this,
                isDefault: true)
            {
                Parent = this.graphicsService.SpriteScreen,
            };

            window.Hidden += (s, e) =>
            {
                if (this.forciblyHidden.Contains(window))
                {
                    return;
                }

                if (this.defaultWindow == window)
                {
                    this.defaultWindow = null;
                }

                window.Dispose();
            };

            this.defaultWindow = window;
            return window;
        }

        // ArranPell, 2026-09-13: same rule the Target List already has (Module.cs's own Update loop) --
        // hidden while the world map is open or the player isn't in-game, shown again once it closes.
        // Called from Module.Update().
        public void Update()
        {
            if (!GameService.Gw2Mumble.IsAvailable)
            {
                return;
            }

            var liveWindows = this.defaultWindow != null
                ? this.pinnedWindows.Append(this.defaultWindow)
                : this.pinnedWindows.AsEnumerable();

            if (!GameService.GameIntegration.Gw2Instance.IsInGame || GameService.Gw2Mumble.UI.IsMapOpen)
            {
                foreach (var window in liveWindows)
                {
                    if (window.Visible && this.forciblyHidden.Add(window))
                    {
                        window.Hide();
                    }
                }
            }
            else if (this.forciblyHidden.Count > 0)
            {
                foreach (var window in this.forciblyHidden)
                {
                    window.Show();
                }

                this.forciblyHidden.Clear();
            }
        }

        public void Dispose()
        {
            this.defaultWindow?.Dispose();
            this.defaultWindow = null;

            foreach (var window in this.pinnedWindows)
            {
                window.Dispose();
            }

            this.pinnedWindows.Clear();
        }
    }
}
