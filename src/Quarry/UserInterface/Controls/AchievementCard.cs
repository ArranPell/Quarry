using Quarry.Interfaces;
using Quarry.Models;
using Quarry.Services;
using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Quarry.WikiData.Achievement;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;

namespace Quarry.UserInterface.Controls
{
    // Phase 40. Replaces AchievementListItem/AchievementButton (a View over a DetailsButton) with one
    // Control that paints itself -- UI-DESIGN.md §3, direction B. Owns at most three children (the hide
    // glyph, the guidance badge, the eye); everything else (icon, title, place row, numerals, pips) is
    // drawn directly so the card can choose its own height instead of working around DetailsButton's
    // fixed 35px bottom band.
    public class AchievementCard : Container
    {
        private const int IconSize = 56;
        private const int Pad = UiStyle.CardPadding;
        private const int TierEdgeHeight = 2;
        private const int HideGlyphSize = 16;
        private const int EyeSize = 22;
        private const int PipHeight = 4;
        private const int PipGap = 2;
        private const int MaxDiscretePips = 40;

        private readonly IAchievementTrackerService achievementTrackerService;
        private readonly IAchievementService achievementService;
        private readonly ITextureService textureService;
        private readonly IHuntService huntService;
        private readonly AchievementTableEntry achievement;
        private readonly GuidanceInfo guidance;
        private readonly IHereCardActions hereCardActions;
        private readonly int? rank;
        private readonly AsyncTexture2D icon;

        private readonly bool isComplete;
        private readonly (int Current, int Max, string Text, string RemainingText, double Fraction) progress;
        private readonly string placeText;
        private readonly bool placeIsBold;
        private readonly string title;

        private GlowButton eye;
        private Label hideLabel;
        private Label badgeLabel;
        private ContextMenuStrip hereMenu;
        private bool hovering;

        public AchievementCard(
            AchievementTableEntry achievement,
            IAchievementTrackerService achievementTrackerService,
            IAchievementService achievementService,
            ITextureService textureService,
            IHuntService huntService,
            INearestObjectiveService nearestObjectiveService,
            ICurrentMapService currentMapService,
            string icon,
            GuidanceInfo guidance,
            IHereCardActions hereCardActions,
            int? rank)
        {
            this.achievement = achievement;
            this.achievementTrackerService = achievementTrackerService;
            this.achievementService = achievementService;
            this.textureService = textureService;
            this.huntService = huntService;
            this.guidance = guidance ?? GuidanceInfo.None;
            this.hereCardActions = hereCardActions;
            this.rank = rank;
            this.icon = this.textureService.GetTexture(icon);

            this.isComplete = this.achievementService.HasFinishedAchievement(achievement.Id);

            // Computed once, at construction -- the same convention AchievementProgress.Get's callers
            // already use for the progress numbers. One INearestObjectiveService lookup per card; Here
            // already rebuilds every card wholesale on any state change that could move this number.
            IReadOnlyList<RemainingObjective> nearest = System.Array.Empty<RemainingObjective>();

            if (nearestObjectiveService.HasAnyObjectives(achievement.Id) && this.achievementService.PlayerAchievements != null)
            {
                var mapId = currentMapService.MapId;
                var player = GameService.Gw2Mumble.PlayerCharacter.Position;
                nearest = nearestObjectiveService.GetRemaining(achievement.Id, mapId, player);
            }

            this.progress = AchievementProgress.Get(achievementService, achievement, nearest);

            if (nearest.Count == 0)
            {
                this.placeText = "no route on this map";
                this.placeIsBold = false;
            }
            else
            {
                var top = nearest[0];
                var distance = $"{top.DistanceMetres:F0} m{(top.GroundDistanceOnly ? " (ground)" : string.Empty)}";

                // AreaHint (Phase 29 tier-2) is the only source of an actual sector/area name on a
                // RemainingObjective -- everything else only carries a distance, not a place. Falling back
                // to the objective's own name keeps the row honest (a real "where") without a service
                // change to thread a world position back out to CurrentMapService's sectors.
                var place = top.AreaHint ?? top.Name;

                this.placeText = $"{place} · {distance}";
                this.placeIsBold = true;
            }

            this.Height = CardGrid.CardHeight;
            this.title = achievement.Name.Trim();

            this.MouseEntered += (s, e) => this.hovering = true;
            this.MouseLeft += (s, e) => this.hovering = false;

            if (!this.isComplete)
            {
                this.Click += this.AchievementCard_Click;
            }

            this.BasicTooltipText = this.progress.RemainingText;

            this.achievementTrackerService.AchievementUntracked += this.Tracker_AchievementUntracked;

            this.BuildChildren();
        }

        private void BuildChildren()
        {
            if (this.guidance.Tier != GuidanceTier.None)
            {
                var canPeek = this.huntService.CanPeek;
                var packBacked = this.guidance.Tier == GuidanceTier.Tagged || this.guidance.Tier == GuidanceTier.Route;
                var description = this.guidance.Describe();

                this.badgeLabel = new Label
                {
                    Parent = this,
                    Text = this.guidance.Label,
                    Font = UiStyle.BodyFont,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Width = 90,
                    Height = 16,
                };

                UiStyle.ApplyShadow(this.badgeLabel, this.guidance.Color);

                if (canPeek && packBacked)
                {
                    this.badgeLabel.BasicTooltipText = description + "\n\nClick to show this route in Pathing.";
                    this.badgeLabel.Click += (s, e) => this.huntService.Peek(this.achievement.Id);
                }
                else
                {
                    this.badgeLabel.BasicTooltipText = description;
                }
            }

            if (this.hereCardActions != null)
            {
                this.BuildHereCardActions();
            }

            if (!this.isComplete)
            {
                this.eye = new GlowButton
                {
                    Parent = this,
                    Width = EyeSize,
                    Height = EyeSize,
                    ActiveIcon = this.textureService.GetRefTexture("track_enabled.png"),
                    Icon = this.textureService.GetRefTexture("track_disabled.png"),
                    ToggleGlow = true,
                    Checked = this.achievementTrackerService.IsBeingTracked(this.achievement.Id),
                    BasicTooltipText = this.achievementTrackerService.IsBeingTracked(this.achievement.Id) ? "Drop this" : "Target this",
                };
            }
        }

        private void BuildHereCardActions()
        {
            var isHidden = this.hereCardActions.IsHidden(this.achievement.Id);

            if (isHidden)
            {
                this.Opacity = 0.55f;
                this.BasicTooltipText = this.hereCardActions.DescribeExclusion(this.achievement.Id);
            }

            // Built eagerly because Control.Menu drives the right-click path; disposed in DisposeControl,
            // since a ContextMenuStrip reparents itself to SpriteScreen when shown and nothing else cleans
            // it up (Phase 31).
            this.hereMenu = new ContextMenuStrip();

            if (isHidden)
            {
                var unhide = this.hereMenu.AddMenuItem("Unhide");
                unhide.Click += (s, e) => this.hereCardActions.Unhide(this.achievement.Id);
            }
            else
            {
                var notToday = this.hereMenu.AddMenuItem("Not today");
                notToday.BasicTooltipText = "Hide from Here until the daily reset (00:00 UTC).";
                notToday.Click += (s, e) => this.hereCardActions.SnoozeUntilReset(this.achievement.Id);

                var notInterested = this.hereMenu.AddMenuItem("Not interested");
                notInterested.BasicTooltipText = "Hide from Here until you un-hide it.";
                notInterested.Click += (s, e) => this.hereCardActions.HideIndefinitely(this.achievement.Id);
            }

            this.Menu = this.hereMenu;

            this.hideLabel = new Label
            {
                Parent = this,
                Text = isHidden ? "+" : "x",
                Font = UiStyle.BodyFont,
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = HideGlyphSize,
                Height = HideGlyphSize,
                BasicTooltipText = isHidden ? "Show this achievement in Here again" : "Hide this from Here (also on right-click)",
            };

            UiStyle.ApplyShadow(this.hideLabel, UiStyle.Rank);

            this.hideLabel.Click += (s, e) => this.hereMenu.Show(GameService.Input.Mouse.Position);
        }

        public override void RecalculateLayout()
        {
            base.RecalculateLayout();

            var width = this.Width;

            if (this.hideLabel != null)
            {
                this.hideLabel.Location = new Point(width - Pad - HideGlyphSize, Pad - 2);
            }

            if (this.eye != null)
            {
                this.eye.Location = new Point(width - Pad - EyeSize, this.Height - Pad - EyeSize);
            }

            if (this.badgeLabel != null)
            {
                var eyeReserve = this.eye != null ? EyeSize + 4 : 0;
                this.badgeLabel.Location = new Point(width - Pad - eyeReserve - this.badgeLabel.Width, this.Height - Pad - 18);
            }
        }

        private void Tracker_AchievementUntracked(int achievementId)
        {
            if (this.achievement.Id == achievementId && this.eye != null)
            {
                this.eye.Checked = false;
                this.eye.BasicTooltipText = "Target this";
            }
        }

        private void AchievementCard_Click(object sender, Blish_HUD.Input.MouseEventArgs e)
        {
            if (this.achievementTrackerService.IsBeingTracked(this.achievement.Id))
            {
                this.achievementTrackerService.RemoveAchievement(this.achievement.Id);

                if (this.eye != null)
                {
                    this.eye.Checked = false;
                    this.eye.BasicTooltipText = "Target this";
                }
            }
            else
            {
                var trackSuccess = this.achievementTrackerService.TrackAchievement(this.achievement.Id);

                if (this.eye != null)
                {
                    this.eye.Checked = trackSuccess;
                    this.eye.BasicTooltipText = trackSuccess ? "Drop this" : "Target this";
                }

                if (!trackSuccess)
                {
                    ScreenNotification.ShowNotification("You can have a maximum of 15 achievements tracked concurrently.\n Untrack one to add a new one.");
                }
            }
        }

        public override void PaintBeforeChildren(SpriteBatch spriteBatch, Rectangle bounds)
        {
            var background = this.isComplete ? UiStyle.Complete : (this.hovering ? UiStyle.CardBackgroundHover : UiStyle.CardBackground);
            spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, new Rectangle(0, TierEdgeHeight, bounds.Width, bounds.Height - TierEdgeHeight), background);

            // The 2px tier-colour top edge, or a hairline card border when there's no guidance to show --
            // colour is never the only carrier of the tier (the word badge always shows too).
            var edgeColor = this.guidance.Tier != GuidanceTier.None ? this.guidance.Color : UiStyle.CardBorder;
            spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, new Rectangle(0, 0, bounds.Width, TierEdgeHeight), edgeColor);
            spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, new Rectangle(0, TierEdgeHeight, 1, bounds.Height - TierEdgeHeight), UiStyle.CardBorder);
            spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, new Rectangle(bounds.Width - 1, TierEdgeHeight, 1, bounds.Height - TierEdgeHeight), UiStyle.CardBorder);
            spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, new Rectangle(0, bounds.Height - 1, bounds.Width, 1), UiStyle.CardBorder);

            // Icon, 56px, never upscaled from the render service's 64px source.
            if (this.icon != null && this.icon.HasTexture)
            {
                spriteBatch.DrawOnCtrl(this, this.icon, new Rectangle(Pad, Pad, IconSize, IconSize));
            }

            var textX = Pad + IconSize + 10;
            var rankWidth = 0;

            if (this.rank.HasValue)
            {
                var rankText = this.rank.Value.ToString();
                rankWidth = (int)UiStyle.BodyFont.MeasureString(rankText).Width + 6;
                spriteBatch.DrawStringOnCtrl(this, rankText, UiStyle.BodyFont, new Rectangle(textX, Pad, rankWidth, 18), UiStyle.Rank);
            }

            var hideReserve = this.hideLabel != null ? HideGlyphSize + 6 : 0;
            var titleWidth = System.Math.Max(40, bounds.Width - textX - rankWidth - Pad - hideReserve);
            var trimmedTitle = StringUtils.TrimNameToWidth(this.title, titleWidth, UiStyle.TitleFont);

            DrawShadowed(spriteBatch, trimmedTitle, UiStyle.TitleFont, new Rectangle(textX + rankWidth, Pad, titleWidth, 20), UiStyle.TextPrimary);

            // Place row.
            var placeColor = this.placeIsBold ? UiStyle.TextSecondary : UiStyle.Rank;
            var placeWidth = bounds.Width - textX - Pad;
            DrawShadowed(spriteBatch, this.placeText, UiStyle.BodyFont, new Rectangle(textX, Pad + 20, placeWidth, 18), placeColor);

            // Numerals / Complete, bottom-left.
            var numeralY = bounds.Height - Pad - 18;

            if (this.isComplete)
            {
                DrawShadowed(spriteBatch, "Complete", UiStyle.NumeralFont, new Rectangle(textX, numeralY, 140, 20), UiStyle.Complete);
            }
            else
            {
                var numeralColor = this.progress.Fraction >= 0.75 ? UiStyle.NearDone : UiStyle.TextPrimary;
                var numeralText = this.progress.Text ?? string.Empty;
                DrawShadowed(spriteBatch, numeralText, UiStyle.NumeralFont, new Rectangle(textX, numeralY, 70, 20), numeralColor);

                if (this.progress.Max > 0)
                {
                    var numeralWidth = (int)UiStyle.NumeralFont.MeasureString(numeralText).Width + 10;
                    var badgeReserve = this.badgeLabel != null ? this.badgeLabel.Width + 6 : 0;
                    var eyeReserve = this.eye != null ? EyeSize + 6 : 0;
                    var pipsX = textX + numeralWidth;
                    var pipsWidth = bounds.Width - pipsX - badgeReserve - eyeReserve - Pad;

                    this.DrawPips(spriteBatch, pipsX, numeralY + 8, pipsWidth);
                }
            }
        }

        // One pip per remaining bit (Current/Max, same semantics the old vignette fill used) when there's
        // room for at least a couple of pixels each; a plain fraction bar above ~40, so a 38-bit
        // achievement (Spiritual Childcare) still reads as pips while a 300-count one doesn't turn to mush.
        private void DrawPips(SpriteBatch spriteBatch, int x, int y, int width)
        {
            if (width <= 0)
            {
                return;
            }

            var max = this.progress.Max;
            var current = System.Math.Min(this.progress.Current, max);
            var pipColor = this.progress.Fraction >= 0.75 ? UiStyle.NearDone : UiStyle.TextPrimary;

            if (max > MaxDiscretePips)
            {
                spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, new Rectangle(x, y, width, PipHeight), UiStyle.PipTodo);
                var filledWidth = (int)(width * this.progress.Fraction);
                if (filledWidth > 0)
                {
                    spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, new Rectangle(x, y, filledWidth, PipHeight), pipColor);
                }

                return;
            }

            var pipWidth = System.Math.Max(1, (width - ((max - 1) * PipGap)) / max);

            for (var i = 0; i < max; i++)
            {
                var pipX = x + (i * (pipWidth + PipGap));
                if (pipX + pipWidth > x + width)
                {
                    break;
                }

                spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, new Rectangle(pipX, y, pipWidth, PipHeight), i < current ? pipColor : UiStyle.PipTodo);
            }
        }

        private void DrawShadowed(SpriteBatch spriteBatch, string text, MonoGame.Extended.BitmapFonts.BitmapFont font, Rectangle destRect, Color color)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var shadowRect = new Rectangle(destRect.X + 1, destRect.Y + 1, destRect.Width, destRect.Height);
            spriteBatch.DrawStringOnCtrl(this, text, font, shadowRect, UiStyle.ShadowColor);
            spriteBatch.DrawStringOnCtrl(this, text, font, destRect, color);
        }

        protected override void DisposeControl()
        {
            this.achievementTrackerService.AchievementUntracked -= this.Tracker_AchievementUntracked;

            this.hereMenu?.Dispose();
            this.hereMenu = null;

            base.DisposeControl();
        }
    }
}
