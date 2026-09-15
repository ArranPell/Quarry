using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Quarry.Interfaces;
using Quarry.Models;
using Quarry.Services;
using UiStyle = Quarry.UserInterface.UiStyle;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quarry.UserInterface.Windows
{
    // Phase 43. UI-DESIGN.md §6: the compact window is now *the* window -- full mode's panels, the
    // compact/full toggle, "Collapse All" and "Close all Subpages" are gone. Every feature they carried
    // survives: wiki text/objectives/notes now live in the Inspector (Phase 45), reached by clicking a
    // row; the wiki/link buttons moved there too. What's left of the old button stack is the "⋯" menu.
    public class AchievementTrackWindow : WindowBase2
    {
        private const int RowHeight = 40;
        // Phase 35: 22 -> 28, to give the now-20px icons (was 16) room to re-centre at y=4.
        private const int NearestStripHeight = 28;
        private const int SummaryRowHeight = 18;
        private const int CompactMinRows = 3;
        // Phase 33c: the session line's tooltip lists what was completed; capped so a long session
        // doesn't produce a tooltip taller than the screen.
        private const int SessionSummaryTooltipLines = 15;
        private const int DefaultWindowWidth = 300;
        private const int DropEyeSize = 14;
        private const int WaypointIconSize = 20;
        private const double NearestRefreshIntervalSeconds = 2.0;
        // Phase 35: the Here strip -- top 3 candidates for the current map, always on (Phase 36's
        // ShowHereStrip toggle was retired before it existed). One 24px header line plus up to three
        // 24px rows, above the session-summary line.
        private const int HereStripCap = 3;
        private const int HereHeaderHeight = 24;
        private const int HereRowHeight = 24;
        private const int HereDividerHeight = 1;
        private const int HereStripPadding = 4;
        // ArranPell, 2026-09-14, after the Phase 35 load-test: the "+" read as too tiny at 16px/SectionFont.
        private const int HereAddButtonWidth = 22;
        // Mirrors WindowBase2's own private STANDARD_TITLEBAR_HEIGHT -- ConstructWindow's 3-arg overload
        // adds this to the windowRegion height to get the resulting Size.Y, and since Size is now set
        // directly (see ApplyWindowFrame) rather than through ConstructWindow, it has to be added here too.
        private const int WindowTitleBarHeight = 40;
        private static readonly Point ConstructRect = new Point(350, 600);

        private readonly ContentsManager contentsManager;
        private readonly IAchievementTrackerService achievementTrackerService;
        private readonly IAchievementService achievementService;
        private readonly IInspectorWindowManager inspectorWindowManager;
        private readonly IHuntService huntService;
        private readonly IPersistenceService persistenceService;
        private readonly ISessionSummaryService sessionSummaryService;
        private readonly INearestObjectiveService nearestObjectiveService;
        private readonly IMarkerPackIndexService markerPackIndexService;
        private readonly ICurrentMapService currentMapService;
        private readonly IHereService hereService;
        private readonly IHereExclusionService hereExclusionService;
        private readonly SettingEntry<int> hereCap;
        private readonly Logger logger;
        private readonly Action openOverview;
        private readonly Texture2D texture;
        private readonly Dictionary<int, Panel> trackedAchievements = new Dictionary<int, Panel>();
        private readonly Dictionary<int, (Panel Row, Label NameLabel, Label ProgressLabel, Label NextLabel, ContextMenuStrip Menu, Image DropEye)> rowControlsById = new Dictionary<int, (Panel, Label, Label, Label, ContextMenuStrip, Image)>();
        private readonly List<(Panel Row, Label NameLabel, Label ProgressLabel, Label AddLabel, ContextMenuStrip Menu)> hereStripRows = new List<(Panel, Label, Label, Label, ContextMenuStrip)>();
        private readonly CancellationTokenSource hereStripCts = new CancellationTokenSource();
        private double nearestRefreshAccumulator;
        private int hereStripRequestSequence;
        private int hereStripDividerY;
        private bool hereStripDividerVisible;

        private FlowPanel flowPanel;
        private Label noAchievementsLabel;
        private Label sessionSummaryLabel;
        private Label nearestStripLabel;
        private Image nearestWaypointIcon;
        private Image openQuarryIcon;
        private Label menuGlyph;
        private Label hereStripHeaderLabel;
        private ContextMenuStrip windowMenu;
        private Point windowSize;
        private EventHandler<Blish_HUD.Input.MouseEventArgs> currentNearestWaypointHandler;

        private volatile bool pendingRebuild;

        // Phase 56 (review item 4): SessionSummaryService.Changed is raised inside a Task.Run; the
        // label is a control, so the handler hops to the main thread and re-lays out (the line was
        // last positioned while invisible, so it would otherwise sit clipped until an unrelated relayout).
        private readonly Action sessionSummaryChangedHandler;

        public Point CompactSize => this.windowSize;

        public AchievementTrackWindow(
            ContentsManager contentsManager,
            IAchievementTrackerService achievementTrackerService,
            IAchievementService achievementService,
            IInspectorWindowManager inspectorWindowManager,
            IHuntService huntService,
            IPersistenceService persistenceService,
            ISessionSummaryService sessionSummaryService,
            INearestObjectiveService nearestObjectiveService,
            IMarkerPackIndexService markerPackIndexService,
            ICurrentMapService currentMapService,
            IHereService hereService,
            IHereExclusionService hereExclusionService,
            SettingEntry<int> hereCap,
            Logger logger,
            Action openOverview)
        {
            this.contentsManager = contentsManager;
            this.achievementTrackerService = achievementTrackerService;
            this.achievementService = achievementService;
            this.inspectorWindowManager = inspectorWindowManager;
            this.huntService = huntService;
            this.persistenceService = persistenceService;
            this.sessionSummaryService = sessionSummaryService;
            this.nearestObjectiveService = nearestObjectiveService;
            this.markerPackIndexService = markerPackIndexService;
            this.currentMapService = currentMapService;
            this.hereService = hereService;
            this.hereExclusionService = hereExclusionService;
            this.hereCap = hereCap;
            this.logger = logger;
            this.openOverview = openOverview;
            // Phase 41: window_blank.png (transparent) replaces background.png -- the stretched draw is
            // now invisible, and WindowBodyPainter paints the real content in PaintBeforeChildren.
            this.texture = this.contentsManager.GetTexture("window_blank.png");
            this.achievementTrackerService.AchievementTracked += this.AchievementTrackerService_AchievementTracked;
            this.achievementTrackerService.AchievementUntracked += this.AchievementTrackerService_AchievementUntracked;
            this.sessionSummaryChangedHandler = () => GameService.Overlay.QueueMainThreadUpdate(gameTime =>
            {
                this.UpdateSessionSummaryLabel();
                this.RelayoutChildren();
            });
            this.sessionSummaryService.Changed += this.sessionSummaryChangedHandler;
            this.achievementService.PlayerAchievementsLoaded += this.AchievementService_PlayerAchievementsLoaded;
            this.currentMapService.Changed += this.CurrentMapService_Changed;
            this.markerPackIndexService.Changed += this.MarkerPackIndexService_Changed;
            this.hereExclusionService.Changed += this.HereExclusionService_Changed;
            // Phase 56 (review item 11): everything that changes the strip's answer without a map change
            // or a hide/snooze -- a poll, a tick, the API categories, an alignment, the pack index.
            this.hereService.CandidatesInvalidated += this.RefreshHereStrip;

            this.BuildWindow();

            // Phase 56 (review item 2): the restored rows are built right here, on the main thread -- the
            // window is only ever constructed from OnModuleLoaded or a click now -- instead of on a
            // Task.Run that raced the draw loop and enumerated the live tracked list unguarded. At most
            // 15 rows, so there is nothing to gain from deferring it.
            foreach (var item in this.achievementTrackerService.ActiveAchievements.ToList())
            {
                this.AchievementTrackerService_AchievementTracked(item);
            }
        }

        public override void PaintBeforeChildren(SpriteBatch spriteBatch, Rectangle bounds)
        {
            WindowBodyPainter.PaintBody(spriteBatch, this, this.ContentRegion, showLeftAccent: false);

            // Phase 35: the Here strip's top edge -- separates "what you're doing" (targets, above) from
            // "what you could add" (the strip, below). Only drawn when the strip has content; y is kept
            // current by RelayoutChildren.
            if (this.hereStripDividerVisible)
            {
                spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, new Rectangle(this.ContentRegion.X, this.hereStripDividerY, this.ContentRegion.Width, HereDividerHeight), UiStyle.CardBorder);
            }

            base.PaintBeforeChildren(spriteBatch, bounds);
        }

        // Same clamp-on-drag approach as AchievementOverviewWindow (Phase 42) -- WindowBase2 has no
        // MinSize of its own. A dragged size also becomes the one PersistWindowState saves, same as a
        // row-count-driven resize already does.
        private const int MinWindowWidth = 220;
        private const int MinWindowHeight = 150;
        private bool applyingSizeClamp;

        protected override void OnResized(Blish_HUD.Controls.ResizedEventArgs e)
        {
            base.OnResized(e);

            if (this.applyingSizeClamp)
            {
                return;
            }

            var clampedWidth = Math.Max(this.Size.X, MinWindowWidth);
            var clampedHeight = Math.Max(this.Size.Y, MinWindowHeight);

            if (clampedWidth != this.Size.X || clampedHeight != this.Size.Y)
            {
                this.applyingSizeClamp = true;
                this.Size = new Point(clampedWidth, clampedHeight);
                this.applyingSizeClamp = false;
            }

            this.windowSize = new Point(this.Size.X, this.Size.Y - WindowTitleBarHeight);
            this.RelayoutChildren();
        }

        private void CreateRow(int achievementId)
        {
            var achievement = this.achievementService.Achievements?.FirstOrDefault(x => x.Id == achievementId);
            if (achievement is null)
            {
                this.logger.Warn($"AchievementTrackWindow: tracked achievement id {achievementId} has no wiki data; skipping its row.");
                return;
            }

            var progress = AchievementProgress.Get(this.achievementService, achievement);

            var row = new Panel()
            {
                Parent = this.flowPanel,
                Width = this.flowPanel.ContentRegion.Width - 16,
                Height = RowHeight,
            };

            var dropEye = new Image()
            {
                Parent = row,
                Width = DropEyeSize,
                Height = DropEyeSize,
                Location = new Point(row.ContentRegion.Width - DropEyeSize, (RowHeight / 2 - DropEyeSize) / 2),
                Texture = this.contentsManager.GetTexture("track_enabled.png"),
                BasicTooltipText = "Drop this",
            };

            dropEye.Click += (s, e) => this.achievementTrackerService.RemoveAchievement(achievementId);

            var progressLabel = new Label()
            {
                Parent = row,
                Text = progress.Text ?? string.Empty,
                Width = 76,
                Height = 16,
                Location = new Point(dropEye.Location.X - 80, 2),
                HorizontalAlignment = HorizontalAlignment.Right,
                Font = UiStyle.BodyFont,
            };

            UiStyle.ApplyShadow(progressLabel, progress.Fraction >= 0.75 ? UiStyle.NearDone : UiStyle.TextPrimary);

            var nameWidth = Math.Max(progressLabel.Location.X - 8, 20);

            var nameLabel = new Label()
            {
                Parent = row,
                Text = StringUtils.TrimNameToWidth(achievement.Name.Trim(), nameWidth),
                Width = nameWidth,
                Height = 16,
                Location = new Point(4, 2),
                Font = UiStyle.BodyFont,
            };

            UiStyle.ApplyTextPrimary(nameLabel);

            var nextLabel = new Label()
            {
                Parent = row,
                Width = row.ContentRegion.Width - 8,
                Height = 16,
                Location = new Point(4, 20),
                Font = UiStyle.BodyFont,
            };

            var menu = this.BuildRowMenu(achievementId);
            row.Menu = menu;

            // Load-test 2026-09-13: wiring Click on the row *and* on dropEye (which sits on top of it)
            // fired both handlers for one click on the eye -- dropped the achievement and then opened it
            // in the Inspector. Rather than depend on exactly how Blish resolves an overlapping click,
            // put the "open" handler only on the labels, which never overlap dropEye's rectangle.
            void OpenInInspector(object s, Blish_HUD.Input.MouseEventArgs e) => this.inspectorWindowManager.ShowAchievement(achievement);
            nameLabel.Click += OpenInInspector;
            progressLabel.Click += OpenInInspector;
            nextLabel.Click += OpenInInspector;

            this.trackedAchievements.Add(achievementId, row);
            this.rowControlsById[achievementId] = (row, nameLabel, progressLabel, nextLabel, menu, dropEye);

            this.RefreshNearestForAchievement(achievementId);
            this.SortTrackedPanels();
        }

        // Right-click (or the row's own Menu, same control) -- "the same menu the Here card has where it
        // applies": Drop, and Show route in Pathing when hunt mode can peek this achievement's route.
        private ContextMenuStrip BuildRowMenu(int achievementId)
        {
            var menu = new ContextMenuStrip();

            var drop = menu.AddMenuItem("Drop");
            drop.Click += (s, e) => this.achievementTrackerService.RemoveAchievement(achievementId);

            if (this.huntService.CanPeek && this.nearestObjectiveService.HasAnyObjectives(achievementId))
            {
                var peek = menu.AddMenuItem("Show route in Pathing");
                peek.Click += (s, e) => this.huntService.Peek(achievementId);
            }

            return menu;
        }

        // Same ordering AchievementItemOverview uses for its "Nearest to done" mode: never-started
        // achievements sort after everything in progress, in-progress ones by fraction descending.
        private void SortTrackedPanels()
        {
            if (this.trackedAchievements.Count == 0)
            {
                return;
            }

            var infoByPanel = new Dictionary<Panel, (bool NeverStarted, double Fraction, string Name)>();

            foreach (var entry in this.trackedAchievements)
            {
                var achievement = this.achievementService.Achievements?.FirstOrDefault(x => x.Id == entry.Key);
                if (achievement is null)
                {
                    continue;
                }

                var progress = AchievementProgress.Get(this.achievementService, achievement);
                infoByPanel[entry.Value] = (progress.Max <= 0, progress.Fraction, achievement.Name);
            }

            this.flowPanel.SortChildren<Panel>((a, b) =>
            {
                if (!infoByPanel.TryGetValue(a, out var infoA) || !infoByPanel.TryGetValue(b, out var infoB))
                {
                    return 0;
                }

                var neverStartedCompare = infoA.NeverStarted.CompareTo(infoB.NeverStarted);
                if (neverStartedCompare != 0)
                {
                    return neverStartedCompare;
                }

                var fractionCompare = infoB.Fraction.CompareTo(infoA.Fraction);
                if (fractionCompare != 0)
                {
                    return fractionCompare;
                }

                return string.Compare(infoA.Name, infoB.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        private void AchievementService_PlayerAchievementsLoaded()
        {
            GameService.Overlay.QueueMainThreadUpdate(gameTime =>
            {
                this.RefreshProgress();
                this.SortTrackedPanels();
                this.RefreshNearestObjectives();
            });

            // The strip itself is refreshed through HereService.CandidatesInvalidated (Phase 56), which
            // this same event feeds -- the load-test 2026-09-14 case of the very first fetch landing before
            // the subtoken resolved (NoPermission, forever) is covered there.
        }

        // The row's progress was rendered once, at build time, and never again -- so a tracked
        // achievement sat at whatever count it had when you tracked it while the Here card beside it
        // moved on. Recomputed here on every player-achievements refresh, which is also what a manual
        // right-click tick (now in the Inspector) raises.
        private void RefreshProgress()
        {
            foreach (var achievementId in this.trackedAchievements.Keys.ToList())
            {
                var achievement = this.achievementService.Achievements?.FirstOrDefault(x => x.Id == achievementId);

                if (achievement is null || !this.rowControlsById.TryGetValue(achievementId, out var rowControls))
                {
                    continue;
                }

                var progress = AchievementProgress.Get(this.achievementService, achievement);
                rowControls.ProgressLabel.Text = progress.Text ?? string.Empty;
                UiStyle.ApplyShadow(rowControls.ProgressLabel, progress.Fraction >= 0.75 ? UiStyle.NearDone : UiStyle.TextPrimary);
            }
        }

        // Phase 17: immediate recompute on map change, in addition to the 2s ticker while visible.
        private void CurrentMapService_Changed()
            => GameService.Overlay.QueueMainThreadUpdate(gameTime =>
            {
                this.RefreshNearestObjectives();
                this.RefreshHereStrip();
            });

        // Hiding something from Quarry's Here tab has to drop it from the strip too, without waiting
        // for a map change. HereService's own cache was already invalidated by the same event, so this
        // refetch is cheap.
        private void HereExclusionService_Changed() => this.RefreshHereStrip();

        // Phase 35: top HereStripCap candidates for the current map, shared with Quarry's Here tab
        // through HereService's (mapId, max, filter) cache -- pass hereCap.Value, not HereStripCap,
        // or a second caller asking for a different max would evict the Here tab's cached result on
        // every map change and vice versa (HereService.cs ~line 157). Sliced down to 3 locally.
        private void RefreshHereStrip()
        {
            var requestId = Interlocked.Increment(ref this.hereStripRequestSequence);
            var token = this.hereStripCts.Token;

            _ = Task.Run(async () =>
            {
                HereResult result;

                try
                {
                    result = await this.hereService.GetCandidatesAsync(this.hereCap.Value, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // Phase 56 (review item 17): was an unobserved task exception that left the strip stale.
                    this.logger.Warn(ex, "Here strip: the candidates lookup failed.");
                    result = null;
                }

                if (token.IsCancellationRequested || requestId != this.hereStripRequestSequence)
                {
                    return;
                }

                GameService.Overlay.QueueMainThreadUpdate(gameTime => this.RenderHereStrip(result));
            }, token);
        }

        // Torn down and rebuilt wholesale on every refresh rather than diffed -- the strip is at most
        // 3 rows, so this is cheap, and it keeps the "which candidate is in which row" bookkeeping simple.
        private void RenderHereStrip(HereResult result)
        {
            foreach (var controls in this.hereStripRows)
            {
                controls.Menu?.Dispose();
                controls.Row.Dispose();
            }

            this.hereStripRows.Clear();

            var headerText = this.BuildHereStripHeaderText(result, out var shown);

            this.hereStripHeaderLabel.Visible = headerText != null;

            if (headerText != null)
            {
                this.hereStripHeaderLabel.Text = headerText;

                foreach (var candidate in shown)
                {
                    this.CreateHereStripRow(candidate);
                }
            }

            this.RelayoutChildren();
        }

        // Product rule: when the data can't answer, say so in one line rather than showing nothing.
        // Returns null only for NotLoaded, which is the one case the strip collapses to no line at all
        // -- every other case still shows the header, worded for what happened.
        private string BuildHereStripHeaderText(HereResult result, out IReadOnlyList<HereCandidate> shown)
        {
            shown = Array.Empty<HereCandidate>();

            if (result is null)
            {
                return "HERE  couldn't load — try again shortly";
            }

            if (result.Reason == HereResultReason.NotLoaded)
            {
                return null;
            }

            var mapName = this.currentMapService.MapName ?? "this map";

            if (result.Reason == HereResultReason.NoCategoryForMap)
            {
                return $"HERE  {mapName}: no guided achievements yet";
            }

            if (result.Reason == HereResultReason.NoPermission)
            {
                return "HERE  achievement permissions not granted";
            }

            // HereService doesn't know about the tracked set -- filter here so a candidate you just
            // targeted leaves the strip immediately rather than waiting for a map change to re-fetch.
            // From the uncapped ranking (Phase 56, review item 18), so tracking the top N refills the
            // strip with the next three rather than emptying it.
            var pool = result.RankedUncapped.Count > 0 ? result.RankedUncapped : result.Candidates;
            shown = pool.Where(c => !this.achievementTrackerService.IsBeingTracked(c.Achievement.Id)).Take(HereStripCap).ToList();

            var text = shown.Count > 0 || result.FilteredByGuidance == 0
                ? $"HERE  {mapName} · {shown.Count}"
                : $"HERE  {mapName} · {result.FilteredByGuidance} filtered by your guidance filter";

            if (result.HiddenCount > 0)
            {
                text += $" · {result.HiddenCount} hidden";
            }

            if (result.Partial)
            {
                text += " · partial";
            }

            return text;
        }

        private void CreateHereStripRow(HereCandidate candidate)
        {
            var achievement = candidate.Achievement;
            var achievementId = achievement.Id;

            var row = new Panel()
            {
                Parent = this,
                Width = this.ContentRegion.Width - 16,
                Height = HereRowHeight,
            };

            var addLabel = new Label()
            {
                Parent = row,
                Text = "+",
                Width = HereAddButtonWidth,
                Height = HereRowHeight,
                Location = new Point(row.ContentRegion.Width - HereAddButtonWidth, 0),
                Font = UiStyle.NumeralFont,
                HorizontalAlignment = HorizontalAlignment.Center,
                BasicTooltipText = "Target this",
            };

            UiStyle.ApplyShadow(addLabel, UiStyle.NearDone);

            addLabel.Click += (s, e) =>
            {
                if (!this.achievementTrackerService.TrackAchievement(achievementId))
                {
                    ScreenNotification.ShowNotification("Target list full");
                }
            };

            var progressLabel = new Label()
            {
                Parent = row,
                Text = $"{candidate.Current}/{candidate.Max}",
                Width = 60,
                Height = HereRowHeight,
                Location = new Point(addLabel.Location.X - 64, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Font = UiStyle.BodyFont,
            };

            UiStyle.ApplyShadow(progressLabel, candidate.Max > 0 && (double)candidate.Current / candidate.Max >= 0.75 ? UiStyle.NearDone : UiStyle.TextPrimary);

            var nameWidth = Math.Max(progressLabel.Location.X - 8, 20);

            var nameLabel = new Label()
            {
                Parent = row,
                Text = StringUtils.TrimNameToWidth(achievement.Name.Trim(), nameWidth),
                Width = nameWidth,
                Height = HereRowHeight,
                Location = new Point(4, 0),
                Font = UiStyle.BodyFont,
            };

            UiStyle.ApplyTextPrimary(nameLabel);

            void OpenInInspector(object s, Blish_HUD.Input.MouseEventArgs e) => this.inspectorWindowManager.ShowAchievement(achievement);
            nameLabel.Click += OpenInInspector;
            progressLabel.Click += OpenInInspector;

            // Same menu the Here card offers where it applies -- Phase 31's exclusion actions.
            var menu = new ContextMenuStrip();
            var notToday = menu.AddMenuItem("Not today");
            notToday.BasicTooltipText = "Hide from Here until the daily reset (00:00 UTC).";
            notToday.Click += (s, e) => this.hereExclusionService.Snooze(achievementId);

            var notInterested = menu.AddMenuItem("Not interested");
            notInterested.BasicTooltipText = "Hide from Here until you un-hide it.";
            notInterested.Click += (s, e) => this.hereExclusionService.Hide(achievementId);

            row.Menu = menu;

            this.hereStripRows.Add((row, nameLabel, progressLabel, addLabel, menu));
        }

        // Height of the Here strip's reserved band -- divider, header and however many rows are showing
        // -- 0 when the header itself is hidden (the NotLoaded case). Called from both RelayoutChildren
        // (every layout pass) and ComputeDefaultContentHeight (the window's very first size).
        private int HereStripContentHeight()
        {
            if (this.hereStripHeaderLabel is null || !this.hereStripHeaderLabel.Visible)
            {
                return 0;
            }

            return HereDividerHeight + HereStripPadding + HereHeaderHeight + (this.hereStripRows.Count * HereRowHeight);
        }

        public override void UpdateContainer(GameTime gameTime)
        {
            base.UpdateContainer(gameTime);

            this.DrainPendingRebuild();

            if (!this.Visible)
            {
                return;
            }

            this.nearestRefreshAccumulator += gameTime.ElapsedGameTime.TotalSeconds;
            if (this.nearestRefreshAccumulator < NearestRefreshIntervalSeconds)
            {
                return;
            }

            this.nearestRefreshAccumulator = 0;
            this.RefreshNearestObjectives();
        }

        // Also finds the single closest remaining objective across every target for the Nearest strip --
        // the same GetRemaining calls each row already needs, so this reuses them rather than asking twice.
        private void RefreshNearestObjectives()
        {
            RemainingObjective bestObjective = null;
            var bestAchievementId = 0;

            foreach (var achievementId in this.trackedAchievements.Keys.ToList())
            {
                var top = this.RefreshNearestForAchievement(achievementId);

                if (top != null && (bestObjective is null || top.DistanceMetres < bestObjective.DistanceMetres))
                {
                    bestObjective = top;
                    bestAchievementId = achievementId;
                }
            }

            this.UpdateNearestStrip(bestObjective, bestAchievementId);
        }

        // No bearing arrow: RemainingObjective carries a distance but no world position to compute one
        // from, and adding one is a NearestObjectiveService change, which Batch F rules out (same
        // limitation as the Inspector's place line, Phase 45). Returns the top remaining objective (or
        // null) so RefreshNearestObjectives can track the account-wide nearest without a second lookup.
        private RemainingObjective RefreshNearestForAchievement(int achievementId)
        {
            if (!this.rowControlsById.TryGetValue(achievementId, out var rowControls))
            {
                return null;
            }

            // Until the account's achievements land, nothing is known to be finished -- so every
            // objective looks outstanding and the answers are confidently wrong. Say we don't know yet.
            if (this.achievementService.PlayerAchievements is null)
            {
                rowControls.NextLabel.Text = "Loading achievement data…";
                rowControls.NextLabel.BasicTooltipText = null;
                return null;
            }

            var mapId = this.currentMapService.MapId;
            var player = GameService.Gw2Mumble.PlayerCharacter.Position;
            var nearest = this.nearestObjectiveService.GetRemaining(achievementId, mapId, player);
            var top = nearest.Count > 0 ? nearest[0] : null;

            if (top is null)
            {
                var guidance = this.nearestObjectiveService.GetGuidance(achievementId, mapId);
                rowControls.NextLabel.Text = guidance.Tier == GuidanceTier.Area ? "somewhere on this map" : "no route on this map";
                UiStyle.ApplyShadow(rowControls.NextLabel, UiStyle.Rank);
                rowControls.NextLabel.BasicTooltipText = null;
            }
            else
            {
                var suffix = top.GroundDistanceOnly ? $" · ~{top.DistanceMetres:F0} m" : $" · {top.DistanceMetres:F0} m";
                var nameWidth = Math.Max(rowControls.NextLabel.Width - (int)UiStyle.BodyFont.MeasureString(suffix).Width - 10, 20);
                rowControls.NextLabel.Text = $"{StringUtils.TrimNameToWidth(top.Name, nameWidth)}{suffix}";
                UiStyle.ApplyTextPrimary(rowControls.NextLabel);
                rowControls.NextLabel.BasicTooltipText = AchievementProgress.FormatNearestText(nearest);
            }

            return top;
        }

        private void UpdateNearestStrip(RemainingObjective objective, int achievementId)
        {
            if (objective is null)
            {
                this.nearestStripLabel.Visible = false;
                this.nearestWaypointIcon.Visible = false;
                return;
            }

            this.nearestStripLabel.Visible = true;
            var suffix = objective.GroundDistanceOnly ? $" · ~{objective.DistanceMetres:F0} m" : $" · {objective.DistanceMetres:F0} m";
            this.nearestStripLabel.Text = $"NEAREST  {objective.Name}{suffix}";

            var mapId = this.currentMapService.MapId;
            var player = GameService.Gw2Mumble.PlayerCharacter.Position;
            var suggestion = this.nearestObjectiveService.NearestWaypoint(achievementId, mapId, player);

            this.nearestWaypointIcon.Visible = suggestion != null;
            this.nearestWaypointIcon.BasicTooltipText = suggestion?.Describe() ?? "No waypoint known for this map yet";

            // Rebound on every refresh rather than kept as a standing subscription: the suggestion (and
            // which achievement it belongs to) can change between refreshes as the player moves.
            this.nearestWaypointIcon.Click -= this.currentNearestWaypointHandler;
            this.currentNearestWaypointHandler = null;

            if (suggestion != null)
            {
                this.currentNearestWaypointHandler = (s, e) =>
                {
                    _ = Blish_HUD.ClipboardUtil.WindowsClipboardService.SetTextAsync(suggestion.Code);
                    ScreenNotification.ShowNotification(suggestion.Name is null ? "Waypoint copied" : $"Copied {suggestion.Name}");
                };

                this.nearestWaypointIcon.Click += this.currentNearestWaypointHandler;
            }
        }

        private void AchievementTrackerService_AchievementUntracked(int achievement)
        {
            if (this.trackedAchievements.TryGetValue(achievement, out var panel))
            {
                _ = this.trackedAchievements.Remove(achievement);
                panel.Dispose();
            }

            if (this.rowControlsById.TryGetValue(achievement, out var rowControls))
            {
                rowControls.Menu?.Dispose();
            }

            _ = this.rowControlsById.Remove(achievement);

            if (this.trackedAchievements.Count == 0)
            {
                this.noAchievementsLabel.Visible = true;
            }

            // Load-test 2026-09-13: this used to call ApplyWindowSize(), which recomputes height from
            // the row count on every track/untrack -- so a window the user had dragged taller (to lose the
            // scrollbar) snapped back to a tight fit the moment anything changed. The window's size is
            // now the user's (or the persisted/default one) until they drag it again; a track/untrack
            // only re-lays out what's inside it, scrolling if there isn't room.
            this.RelayoutChildren();
            this.SortTrackedPanels();
            this.RefreshNearestObjectives();
            this.RefreshHereStrip();
        }

        private void AchievementTrackerService_AchievementTracked(int achievementId)
        {
            if (this.noAchievementsLabel.Visible)
            {
                this.noAchievementsLabel.Visible = false;
            }

            if (this.achievementService.Achievements?.FirstOrDefault(x => x.Id == achievementId) is null)
            {
                this.logger.Warn($"AchievementTrackWindow: tracked achievement id {achievementId} has no wiki data; skipping its row.");
                return;
            }

            if (this.trackedAchievements.ContainsKey(achievementId))
            {
                return;
            }

            this.CreateRow(achievementId);
            this.RelayoutChildren();
            this.RefreshHereStrip();
        }

        private void BuildWindow()
        {
            var storage = this.persistenceService.Get();

            // Only ever auto-sized from the row count once, the first time this window has no saved
            // height yet -- a real drag or a persisted size afterwards is the user's, and track/untrack
            // no longer overrides it (see AchievementTrackerService_AchievementTracked/Untracked).
            var height = storage.TrackWindowCompactHeight > 0 ? storage.TrackWindowCompactHeight : this.ComputeDefaultContentHeight();
            this.windowSize = new Point(storage.TrackWindowCompactWidth > 0 ? storage.TrackWindowCompactWidth : DefaultWindowWidth, height);

            this.Title = "Target List";
            this.Emblem = this.contentsManager.GetTexture("track_enabled.png");

            this.ConstructWindow(this.texture, new Rectangle(0, 0, ConstructRect.X, ConstructRect.Y), new Rectangle(0, 30, ConstructRect.X, ConstructRect.Y - 30));

            // ArranPell, 2026-09-13, after confirming it works well on the Quarry window (Phase 42): the same
            // drag-resize handle here. OnResized re-lays out the rows the same way a track/untrack does.
            this.CanResize = true;

            this.Size = new Point(this.windowSize.X, this.windowSize.Y + WindowTitleBarHeight);

            this.nearestStripLabel = new Label()
            {
                Parent = this,
                Location = new Point(4, 0),
                Width = this.ContentRegion.Width - 96,
                Height = NearestStripHeight,
                Font = UiStyle.SectionFont,
                Visible = false,
            };

            UiStyle.ApplyShadow(this.nearestStripLabel, UiStyle.Accent);

            this.nearestWaypointIcon = new Image()
            {
                Parent = this,
                Width = WaypointIconSize,
                Height = WaypointIconSize,
                Location = new Point(this.ContentRegion.Width - 72, 4),
                Texture = this.contentsManager.GetTexture("link.png"),
                Visible = false,
                BasicTooltipText = "Copy the waypoint nearest the closest objective",
            };

            // ArranPell, 2026-09-13: the "⋯" menu's Open Quarry item wasn't discoverable enough on its own --
            // a dedicated, always-visible icon for the single most-needed action, the same spot the
            // waypoint-copy icon already lives. ArranPell, 2026-09-14: tinted NearDone -- the one coloured
            // thing in an otherwise neutral band, so the escape hatch to the full list is findable
            // without a toggle telling anyone it's there.
            this.openQuarryIcon = new Image()
            {
                Parent = this,
                Width = WaypointIconSize,
                Height = WaypointIconSize,
                Location = new Point(this.ContentRegion.Width - 46, 4),
                Texture = this.contentsManager.GetTexture("achievement_icon.png"),
                Tint = UiStyle.NearDone,
                BasicTooltipText = "Open Quarry",
            };

            this.openQuarryIcon.Click += (s, e) => this.openOverview();

            this.menuGlyph = new Label()
            {
                Parent = this,
                Text = "⋯",
                Font = UiStyle.SectionFont,
                Width = 20,
                Height = NearestStripHeight,
                Location = new Point(this.ContentRegion.Width - 20, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            UiStyle.ApplyShadow(this.menuGlyph, UiStyle.TextSecondary);

            this.windowMenu = new ContextMenuStrip();
            var openQuarry = this.windowMenu.AddMenuItem("Open Quarry");
            openQuarry.Click += (s, e) => this.openOverview();

            var reloadFromFile = this.windowMenu.AddMenuItem("Reload from file");
            reloadFromFile.Click += (s, e) => this.persistenceService.Reload();

            var saveNow = this.windowMenu.AddMenuItem("Save now");
            saveNow.Click += (s, e) =>
            {
                this.PersistWindowState();
                ScreenNotification.ShowNotification("Tracked achievements saved");
            };

            this.menuGlyph.Click += (s, e) => this.windowMenu.Show(GameService.Input.Mouse.Position);

            this.sessionSummaryLabel = new Label()
            {
                Height = SummaryRowHeight,
                Width = this.ContentRegion.Width,
                Location = new Point(0, this.ContentRegion.Height - SummaryRowHeight),
                Parent = this,
                HorizontalAlignment = HorizontalAlignment.Center,
                Font = UiStyle.BodyFont,
                Visible = false,
                BasicTooltipText = "Achievement progress can lag the API by a few minutes, so this may lag behind what you just did.",
            };

            UiStyle.ApplyShadow(this.sessionSummaryLabel, UiStyle.TextMuted);

            this.UpdateSessionSummaryLabel();

            this.hereStripHeaderLabel = new Label()
            {
                Parent = this,
                Font = UiStyle.SectionFont,
                Height = HereHeaderHeight,
                Visible = false,
            };

            UiStyle.ApplyShadow(this.hereStripHeaderLabel, UiStyle.Accent);

            this.flowPanel = new FlowPanel()
            {
                Parent = this,
                CanScroll = true,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 4),
                Location = new Point(0, NearestStripHeight),
                Width = this.ContentRegion.Width,
                Height = this.ContentRegion.Height - NearestStripHeight - SummaryRowHeight,
            };

            // Parented to the window, not flowPanel: FlowPanel.SortChildren<Panel>() casts every child of
            // flowPanel to Panel, and this Label would make that throw the moment anything ever tries to
            // sort while the tracked set is empty.
            this.noAchievementsLabel = new Label()
            {
                Parent = this,
                Text = "No targets yet — open Quarry and target something.",
                Visible = true,
                Location = this.flowPanel.Location,
                Width = this.flowPanel.Width,
                Height = this.flowPanel.Height,
                WrapText = true,
                Font = UiStyle.BodyFont,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Middle,
            };

            UiStyle.ApplyShadow(this.noAchievementsLabel, UiStyle.TextMuted);

            this.RefreshHereStrip();
        }

        // Phase 35: one function over the elements actually showing, rather than the constant-ish shape
        // Phase 36 was going to need a toggle set to keep honest. Used only for the very first size this
        // window ever gets (see BuildWindow); a track/untrack or a strip appearing/disappearing afterwards
        // no longer resizes the window itself, only re-lays out what's inside it (RelayoutChildren).
        private int ComputeDefaultContentHeight()
        {
            // this.achievementTrackerService.ActiveAchievements.Count is known synchronously at
            // construction, before the restore loop's rows exist, so the very first size (0 rows built
            // yet) still fits the real tracked count instead of the 3-row floor.
            var rowCount = Math.Max(this.achievementTrackerService.ActiveAchievements.Count, CompactMinRows);
            var rowsHeight = rowCount * (RowHeight + 4);

            // The Here strip hasn't loaded yet at this point in construction (HereStripContentHeight
            // returns 0 until hereStripHeaderLabel exists) and the session line reads the same underlying
            // service call UpdateSessionSummaryLabel uses -- both read as "not showing yet" here, which is
            // correct: this height is only ever used once, before either has had a chance to appear.
            var hereStripHeight = this.HereStripContentHeight();
            var summaryHeight = this.sessionSummaryService.GetSummaryLine() != null ? SummaryRowHeight : 0;

            return NearestStripHeight + rowsHeight + hereStripHeight + summaryHeight;
        }

        // Everything in BuildWindow that depends on ContentRegion, re-run whenever the window's Size
        // changes so nothing is left sized against a stale (usually smaller) region.
        private void RelayoutChildren()
        {
            if (this.flowPanel is null)
            {
                return;
            }

            this.nearestStripLabel.Width = this.ContentRegion.Width - 96;
            this.nearestWaypointIcon.Location = new Point(this.ContentRegion.Width - 72, 4);
            this.openQuarryIcon.Location = new Point(this.ContentRegion.Width - 46, 4);
            this.menuGlyph.Location = new Point(this.ContentRegion.Width - 20, 0);

            // Phase 35: everything below the top bar is now laid out bottom-up over what's actually
            // showing, rather than the top bar/flowPanel/summary-line's fixed three-way split -- a
            // summary line or a Here strip with nothing to say reserves no height (the dead band Batch
            // F's load-test fix 3 left in place; see ComputeDefaultContentHeight's header comment).
            var bottomY = this.ContentRegion.Height;

            this.sessionSummaryLabel.Width = this.ContentRegion.Width;

            if (this.sessionSummaryLabel.Visible)
            {
                bottomY -= SummaryRowHeight;
            }

            this.sessionSummaryLabel.Location = new Point(0, bottomY);

            var hereStripHeight = this.HereStripContentHeight();

            if (hereStripHeight > 0)
            {
                bottomY -= hereStripHeight;

                this.hereStripDividerY = bottomY;
                this.hereStripDividerVisible = true;

                var y = bottomY + HereDividerHeight + HereStripPadding;

                this.hereStripHeaderLabel.Location = new Point(4, y);
                this.hereStripHeaderLabel.Width = this.ContentRegion.Width - 8;
                y += HereHeaderHeight;

                foreach (var controls in this.hereStripRows)
                {
                    controls.Row.Location = new Point(8, y);
                    controls.Row.Width = this.ContentRegion.Width - 16;
                    controls.AddLabel.Location = new Point(controls.Row.ContentRegion.Width - HereAddButtonWidth, 0);
                    controls.ProgressLabel.Location = new Point(controls.AddLabel.Location.X - 64, 0);
                    var rowNameWidth = Math.Max(controls.ProgressLabel.Location.X - 8, 20);
                    controls.NameLabel.Width = rowNameWidth;
                    y += HereRowHeight;
                }
            }
            else
            {
                this.hereStripDividerVisible = false;
            }

            this.flowPanel.Location = new Point(0, NearestStripHeight);
            this.flowPanel.Width = this.ContentRegion.Width;
            this.flowPanel.Height = Math.Max(bottomY - NearestStripHeight, 0);

            this.noAchievementsLabel.Location = this.flowPanel.Location;
            this.noAchievementsLabel.Width = this.flowPanel.Width;
            this.noAchievementsLabel.Height = this.flowPanel.Height;

            // Load-test 2026-09-13: dropEye's Location was computed once at CreateRow time from the
            // row's *original* width and never moved when the row was later resized here -- on a
            // narrower row it ended up sitting over ground the row itself still claimed for its own
            // Click, so one click on the eye fired both the drop and the row's "open" handler.
            foreach (var controls in this.rowControlsById.Values)
            {
                controls.Row.Width = this.flowPanel.ContentRegion.Width - 16;
                controls.DropEye.Location = new Point(controls.Row.ContentRegion.Width - DropEyeSize, (RowHeight / 2 - DropEyeSize) / 2);
                controls.ProgressLabel.Location = new Point(controls.DropEye.Location.X - 80, 2);

                var nameWidth = Math.Max(controls.ProgressLabel.Location.X - 8, 20);
                controls.NameLabel.Width = nameWidth;
                controls.NextLabel.Width = controls.Row.ContentRegion.Width - 8;
            }
        }

        // Disposes every tracked row and returns the ids that were tracked, so a full rebuild (the pack-
        // index race fix below) can recreate them.
        private List<int> TearDownAllEntries()
        {
            var trackedIds = this.trackedAchievements.Keys.ToList();

            foreach (var entry in this.rowControlsById.Values)
            {
                entry.Menu?.Dispose();
            }

            foreach (var panel in this.trackedAchievements.Values)
            {
                panel.Dispose();
            }

            this.trackedAchievements.Clear();
            this.rowControlsById.Clear();

            return trackedIds;
        }

        private void RecreateEntries(List<int> trackedIds)
        {
            foreach (var id in trackedIds)
            {
                this.AchievementTrackerService_AchievementTracked(id);
            }

            this.noAchievementsLabel.Visible = this.trackedAchievements.Count == 0;
        }

        // Phase 17 fix (2026-09-09): MarkerPackIndexService.LoadAsync runs unawaited from the DI
        // container, so the window's startup restore loop can build a row before the pack index is
        // ready -- that row's hasRoute check reads TryGet as false permanently, so its Next line
        // silently never appears until something rebuilds the row. Rebuilding every tracked entry once
        // the index actually finishes loading (or later reloads) closes that race.
        //
        // Only flags the rebuild: this fires on the index's own loader thread, and DrainPendingRebuild
        // runs it from the main-thread Update. (The restore loop it once had to wait out is synchronous
        // in the constructor since Phase 56.)
        private void MarkerPackIndexService_Changed()
            => this.pendingRebuild = true;

        // Main-thread, post-construction: the only point where a full rebuild is safe.
        private void DrainPendingRebuild()
        {
            if (!this.pendingRebuild || this.flowPanel is null)
            {
                return;
            }

            this.pendingRebuild = false;
            this.RecreateEntries(this.TearDownAllEntries());
        }

        private void PersistWindowState()
            => this.persistenceService.Save(this.Location.X, this.Location.Y, this.Visible, this.windowSize.X, this.windowSize.Y);

        private void UpdateSessionSummaryLabel()
        {
            if (this.sessionSummaryLabel is null)
            {
                return;
            }

            var line = this.sessionSummaryService.GetSummaryLine();

            this.sessionSummaryLabel.Text = line;
            this.sessionSummaryLabel.Visible = line != null;

            // Phase 33c: the summary line says how many; the tooltip says which.
            var names = this.sessionSummaryService.Summary?.CompletedAchievementNames;

            if (names is null || names.Count == 0)
            {
                this.sessionSummaryLabel.BasicTooltipText = null;
                return;
            }

            var shown = names.Take(SessionSummaryTooltipLines).ToList();
            var tooltip = string.Join("\n", shown);

            if (names.Count > shown.Count)
            {
                tooltip += $"\n... and {names.Count - shown.Count} more";
            }

            this.sessionSummaryLabel.BasicTooltipText = tooltip;
        }

        protected override void DisposeControl()
        {
            this.achievementTrackerService.AchievementTracked -= this.AchievementTrackerService_AchievementTracked;
            this.achievementTrackerService.AchievementUntracked -= this.AchievementTrackerService_AchievementUntracked;
            this.sessionSummaryService.Changed -= this.sessionSummaryChangedHandler;
            this.achievementService.PlayerAchievementsLoaded -= this.AchievementService_PlayerAchievementsLoaded;
            this.currentMapService.Changed -= this.CurrentMapService_Changed;
            this.markerPackIndexService.Changed -= this.MarkerPackIndexService_Changed;
            this.hereExclusionService.Changed -= this.HereExclusionService_Changed;
            this.hereService.CandidatesInvalidated -= this.RefreshHereStrip;

            this.hereStripCts.Cancel();
            this.hereStripCts.Dispose();

            foreach (var entry in this.rowControlsById.Values)
            {
                entry.Menu?.Dispose();
            }

            foreach (var entry in this.hereStripRows)
            {
                entry.Menu?.Dispose();
            }

            this.windowMenu?.Dispose();

            foreach (var item in this.trackedAchievements)
            {
                item.Value.Dispose();
            }

            this.trackedAchievements.Clear();
            this.hereStripRows.Clear();

            this.flowPanel.Dispose();

            base.DisposeControl();
        }
    }
}
