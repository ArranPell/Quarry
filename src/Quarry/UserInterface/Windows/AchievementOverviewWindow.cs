using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Modules.Managers;
using Quarry.Interfaces;
using Quarry.UserInterface.Views;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Quarry.UserInterface.Windows
{
    // Replaces the Phase 0-18 Blish HUD overlay tab: current modules own their windows (Pathing,
    // Timers, Event Table...), and the overlay tab was the likely cause of the "Settings icon
    // selected, our content showing" quirk. Built the way Pathing builds its settings window
    // (the Pathing module's PathingModule.cs, ~L119).
    public class AchievementOverviewWindow : TabbedWindow2
    {
        // Phase 42: now the resize *clamp*, not a one-time fixed size -- CanResize lets the user go past
        // either bound by hand, but a script/settings-file value can't push the window off a usable size.
        private static readonly Point MinWindowSize = new Point(900, 680);
        private static readonly Point MaxWindowSize = new Point(1200, 1100);

        // Slack left around the window so it can't open with its edges off-screen at Module's default
        // (100, 100) location.
        private const int ScreenMarginX = 200;
        private const int ScreenMarginY = 180;

        private bool applyingSizeClamp;

        public AchievementOverviewWindow(
            ContentsManager contentsManager,
            IAchievementItemOverviewFactory achievementItemOverviewFactory,
            IAchievementService achievementService,
            ITextureService textureService,
            IHereService hereService,
            ICurrentMapService currentMapService,
            IAchievementCardFactory achievementCardFactory,
            IHereExclusionService hereExclusionService,
            IAchievementTrackerService achievementTrackerService,
            IPersistenceService persistenceService,
            Blish_HUD.Settings.SettingEntry<int> hereCap,
            Action openTrackedWindow)
            : base(
                contentsManager.GetTexture("window_blank.png"),
                new Rectangle(0, 0, 900, 640),
                new Rectangle(95, 42, 821, 592))
        {
            this.Title = "Quarry";
            this.Emblem = contentsManager.GetTexture("achievement_icon.png");
            this.Id = "Quarry_OverviewWindow";
            this.SavesPosition = true;

            // Phase 42: CardGrid re-lays out in RecalculateLayout, which OnResized triggers -- and the
            // views below size their CardGrid with WidthSizingMode.Fill rather than a fixed pixel Width,
            // so it tracks this window's ContentRegion as it's dragged. Reflow on drag is ArranPell's to
            // confirm; the size clamp and content-height sizing below don't depend on it working.
            this.CanResize = true;

            var achievementsTab = new Tab(
                contentsManager.GetTexture("achievement_icon.png"),
                () => new AchievementTrackerView(achievementItemOverviewFactory, achievementService, textureService, openTrackedWindow),
                "All");

            // Asset 157123: the icon Pathing uses for its own Map Settings tab.
            var hereTab = new Tab(
                AsyncTexture2D.FromAssetId(157123),
                () => new HereView(hereService, currentMapService, achievementCardFactory, hereExclusionService, achievementTrackerService, hereCap, openTrackedWindow),
                "Here");

            this.Tabs.Add(achievementsTab);
            this.Tabs.Add(hereTab);

            var storage = persistenceService.Get();
            var screen = Blish_HUD.GameService.Graphics.SpriteScreen;

            // A saved size (from a previous resize) wins; otherwise the same screen-fraction default as
            // before Phase 42.
            var defaultSize = new Point(
                MathHelper.Clamp(screen.Width - ScreenMarginX, MinWindowSize.X, MaxWindowSize.X),
                MathHelper.Clamp(screen.Height - ScreenMarginY, MinWindowSize.Y, MaxWindowSize.Y));

            this.Size = storage.OverviewWindowWidth > 0 && storage.OverviewWindowHeight > 0
                ? new Point(
                    MathHelper.Clamp(storage.OverviewWindowWidth, MinWindowSize.X, MaxWindowSize.X),
                    MathHelper.Clamp(storage.OverviewWindowHeight, MinWindowSize.Y, MaxWindowSize.Y))
                : defaultSize;
        }

        // WindowBase2 has no MinSize/MaxSize of its own (verified against Blish v1.2.0 source) -- CanResize's
        // drag handle would otherwise let the window go arbitrarily small or large. Guarded against
        // re-entrancy: setting Size here re-enters OnResized once, which must see applyingSizeClamp and
        // return rather than clamp again.
        protected override void OnResized(Blish_HUD.Controls.ResizedEventArgs e)
        {
            base.OnResized(e);

            if (this.applyingSizeClamp)
            {
                return;
            }

            var clamped = new Point(
                MathHelper.Clamp(this.Size.X, MinWindowSize.X, MaxWindowSize.X),
                MathHelper.Clamp(this.Size.Y, MinWindowSize.Y, MaxWindowSize.Y));

            if (clamped != this.Size)
            {
                this.applyingSizeClamp = true;
                this.Size = clamped;
                this.applyingSizeClamp = false;
            }
        }

        // Phase 41: window_blank.png made the base stretch invisible; this is what actually paints the
        // content area now. Painted first, then base.PaintBeforeChildren, so the native sidebar fade and
        // title bar land on top -- TabbedWindow2 doesn't override PaintBeforeChildren itself (verified
        // against Blish v1.2.0 source), so this override is safe to add here.
        public override void PaintBeforeChildren(SpriteBatch spriteBatch, Rectangle bounds)
        {
            WindowBodyPainter.PaintBody(spriteBatch, this, this.ContentRegion, showLeftAccent: true);
            base.PaintBeforeChildren(spriteBatch, bounds);
        }
    }
}
