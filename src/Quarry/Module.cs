using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Quarry.Models;
using Quarry.Services;
using Quarry.UserInterface.Views;
using Quarry.UserInterface.Windows;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quarry
{
    [Export(typeof(Blish_HUD.Modules.Module))]
    public class Module : Blish_HUD.Modules.Module
    {
        private static readonly Logger Logger = Logger.GetLogger<Module>();
        private readonly DependencyInjectionContainer dependencyInjectionContainer;
        private readonly Logger logger;
        private AchievementOverviewWindow overviewWindow;
        private AchievementTrackWindow window;
        private CornerIcon cornerIcon;
        private bool purposelyHidden;
        private SettingEntry<bool> autoSave;
        private SettingEntry<bool> limitAchievements;
        private SettingEntry<bool> hereToast;
        private SettingEntry<bool> showCornerIcon;
        private SettingEntry<bool> bitAlignmentValidation;
        private SettingEntry<bool> huntMode;
        private SettingEntry<bool> autoUntrackCompleted;
        private SettingEntry<bool> huntRevertOnUnload;
        private SettingEntry<HereGuidanceFilter> hereGuidanceFilter;
        private SettingEntry<int> hereCap;
        private SettingEntry<KeyBinding> toggleTrackWindowKeyBind;
        private bool suppressNextHereToast = true;
        private static readonly TimeSpan HereToastCooldown = TimeSpan.FromMinutes(10);
        private readonly Dictionary<int, DateTime> lastHereToastByMapId = new Dictionary<int, DateTime>();
        private CancellationTokenSource cts;
        private CancellationTokenSource saveDebounceCts;
        private Stopwatch loadStopwatch;

        #region Service Managers
        internal SettingsManager SettingsManager => this.ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;
        #endregion

        [ImportingConstructor]
        public Module([Import("ModuleParameters")] ModuleParameters moduleParameters)
            : base(moduleParameters)
        {
            this.logger = Logger;
            this.dependencyInjectionContainer = new DependencyInjectionContainer(this.Gw2ApiManager, this.ContentsManager, GameService.Content, this.DirectoriesManager, this.logger, GameService.Graphics);
        }

        protected override void DefineSettings(SettingCollection settings)
        {
            this.autoSave = settings.DefineSetting("AutoSave", false, () => "Auto save every 5 minutes", () => "Auto save tracked achievements, windows and their positions every 5 minutes");

            this.limitAchievements = settings.DefineSetting("LimitAchievements", true, () => "Limit Achievements to 15", () => "This will limit the maximum of achievements to 15. If it's disabled expect performance and usability issues.");

            this.hereToast = settings.DefineSetting("HereToast", true, () => "Notify on map change", () => "Show a short notification when the current map has nearly-complete achievements");

            this.showCornerIcon = settings.DefineSetting("ShowCornerIcon", true, () => "Show corner icon", () => "Show a corner icon to open the Quarry window. Turn off if you only use the keybind or the Target List button.");

            this.toggleTrackWindowKeyBind = settings.DefineSetting("ToggleTrackWindow", new KeyBinding(), () => "Toggle Target List", () => "Shows or hides the Target List.");
            this.toggleTrackWindowKeyBind.Value.BlockSequenceFromGw2 = true;
            this.toggleTrackWindowKeyBind.Value.Enabled = true;

            this.bitAlignmentValidation = settings.DefineSetting("BitAlignmentValidation", false, () => "Validate bit alignment (debug)", () => "One-time startup check: aligns every collection/objective achievement and logs a summary, including a comparison against the old hand-written table. Leave off unless debugging \"what's left\" text.");

            this.huntMode = settings.DefineSetting("HuntMode", false, () => "Hunt mode", () => "Tracking a guided achievement turns its Pathing routes on; untracking (or completing it) turns off only what we turned on. Requires the Pathing module.");

            this.autoUntrackCompleted = settings.DefineSetting("AutoUntrackCompleted", true, () => "Untrack achievements on completion", () => "Automatically untrack an achievement once it's Done, with a short \"Done: \" notification.");

            this.hereGuidanceFilter = settings.DefineSetting("HereGuidanceFilter", HereGuidanceFilter.Everything, () => "Here: minimum guidance", () => "Only list achievements this well guided on the current map. TaggedOnly = marker-pack objectives that disappear as you finish them; CoordinatesOrBetter = those plus wiki coordinates; AnyGuidance = anything with a route or an area; Everything = no filter.");

            this.hereCap = settings.DefineSetting("HereCap", 10, () => "Here: how many to list", () => "How many achievements Here lists for the current map. Smaller is the point.");
            this.hereCap.SetRange(5, 15);

            this.huntRevertOnUnload = settings.DefineSetting("HuntRevertOnUnload", false, () => "Revert hunt routes on disable", () => "Turn off every route this module enabled when the module is disabled. Leave off to keep routes visible in Pathing across a restart.");
        }

        protected override void Initialize()
        {
            this.Gw2ApiManager.SubtokenUpdated += this.Gw2ApiManager_SubtokenUpdated;
        }

        private async void Gw2ApiManager_SubtokenUpdated(object sender, EventArgs args)
        {
            try
            {
                this.logger.Info("Subtoken updated");
                if (this.dependencyInjectionContainer?.AchievementService != null)
                {
                    await this.dependencyInjectionContainer.AchievementService.LoadPlayerAchievements();
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Exception occured handling SubtokenUpdated");
            }
        }

        protected override async Task LoadAsync()
        {
            this.cts = new CancellationTokenSource();
            var token = this.cts.Token;
            this.loadStopwatch = Stopwatch.StartNew();

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), token);
                this.logger.Info($"Startup timing: fixed startup delay took {this.loadStopwatch.ElapsedMilliseconds} ms");
                await this.dependencyInjectionContainer.InitializeAsync(this.autoSave, this.limitAchievements, this.huntMode, this.hereGuidanceFilter, this.hereCap, token);
                this.dependencyInjectionContainer.AchievementTrackerService.AchievementTracked += this.AchievementTrackerService_AchievementTracked;
                this.dependencyInjectionContainer.AchievementTrackerService.AchievementTracked += this.DebounceSave;
                this.dependencyInjectionContainer.AchievementTrackerService.AchievementUntracked += this.DebounceSave;
                this.dependencyInjectionContainer.AchievementTrackerService.AchievementTracked += this.PrefetchBitAlignment;
                this.dependencyInjectionContainer.SessionSummaryService.AchievementCompleted += this.SessionSummaryService_AchievementCompleted;

                if (this.bitAlignmentValidation.Value)
                {
                    _ = this.dependencyInjectionContainer.BitAlignmentService.RunValidationAsync(token);
                }

                // Phase 23 gave alignment two triggers -- "tracked" and "opened in detail" -- but
                // AchievementTrackerService.Load() restores the tracked set straight into its list
                // without raising AchievementTracked, so nothing tracked in a *previous* session was
                // ever aligned. MapRowToBit then falls back to identity and the wrong steps show as
                // done. Found 2026-09-11 on "Character Growth" (6333), where the wiki lists the same
                // 16 objectives in a different order than the API: 13 of its 16 rows map to a bit
                // other than their own index. Anything painted before this lands reads the identity
                // mapping until its next refresh.
                _ = Task.Run(() => this.PrefetchTrackedBitAlignment(token), token);

                // The windows and the corner icon are built in OnModuleLoaded (Phase 56, review item 3):
                // Blish runs this method on a thread-pool thread, and those are controls.

                this.dependencyInjectionContainer.PersistenceService.AutoSave += this.SavePersistentInformation;

                this.dependencyInjectionContainer.CurrentMapService.Changed += this.CurrentMapService_Changed;
                this.CurrentMapService_Changed();
            }
            catch (OperationCanceledException)
            {
                // Module was disabled while LoadAsync was still in flight -- not an error.
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Exception occured during module load");
                throw;
            }

            await base.LoadAsync();
        }

        private void InitializeWindow()
        {
            if (this.window is null)
            {
                this.window = new AchievementTrackWindow(
                    this.ContentsManager,
                    this.dependencyInjectionContainer.AchievementTrackerService,
                    this.dependencyInjectionContainer.AchievementService,
                    this.dependencyInjectionContainer.InspectorWindowManager,
                    this.dependencyInjectionContainer.HuntService,
                    this.dependencyInjectionContainer.PersistenceService,
                    this.dependencyInjectionContainer.SessionSummaryService,
                    this.dependencyInjectionContainer.NearestObjectiveService,
                    this.dependencyInjectionContainer.MarkerPackIndexService,
                    this.dependencyInjectionContainer.CurrentMapService,
                    this.dependencyInjectionContainer.HereService,
                    this.dependencyInjectionContainer.HereExclusionService,
                    this.hereCap,
                    this.logger,
                    this.OpenOverview)
                {
                    Parent = GameService.Graphics.SpriteScreen,
                };

                var savedWindowLocation = this.dependencyInjectionContainer.PersistenceService.Get();

                this.logger.Debug($"SavedWindowLocation -  X:{savedWindowLocation.TrackWindowLocationX} Y:{savedWindowLocation.TrackWindowLocationY}");

                this.window.Location =
                    savedWindowLocation.TrackWindowLocationX == -1 || savedWindowLocation.TrackWindowLocationY == -1 ?
                    (GameService.Graphics.SpriteScreen.Size / new Point(2)) - (new Point(256, 178) / new Point(2)) :
                    new Point(savedWindowLocation.TrackWindowLocationX, savedWindowLocation.TrackWindowLocationY);

                this.logger.Debug($"AchievementTrackWindowLocation -  X:{this.window.Location.X} Y:{this.window.Location.Y}");
            }
        }

        private void OpenOverview()
        {
            this.overviewWindow.Show();
            this.overviewWindow.BringWindowToFront();
        }

        private void CreateCornerIcon()
        {
            if (this.cornerIcon != null)
            {
                return;
            }

            this.cornerIcon = new CornerIcon()
            {
                IconName = "Open Quarry",
                Icon = this.ContentsManager.GetTexture(@"corner_icon_inactive.png"),
                HoverIcon = this.ContentsManager.GetTexture(@"corner_icon_active.png"),
                Priority = int.MinValue,
                Width = 64,
                Height = 64,
            };

            this.cornerIcon.Click += (s, e) => this.overviewWindow.ToggleWindow();
        }

        private void ShowCornerIcon_SettingChanged(object sender, ValueChangedEventArgs<bool> e)
        {
            if (e.NewValue)
            {
                this.CreateCornerIcon();
            }
            else
            {
                this.cornerIcon?.Dispose();
                this.cornerIcon = null;
            }
        }

        private void ToggleTrackWindowKeyBind_Activated(object sender, EventArgs e) => this.ToggleTrackWindow();

        private void ToggleTrackWindow()
        {
            this.InitializeWindow();
            this.window.ToggleWindow();
        }

        private void CurrentMapService_Changed()
        {
            var suppressToast = this.suppressNextHereToast;
            this.suppressNextHereToast = false;
            var token = this.cts?.Token ?? new CancellationToken(canceled: true);

            _ = Task.Run(async () =>
            {
                try
                {
                    var mapService = this.dependencyInjectionContainer.CurrentMapService;

                    // Phase 56 (review item 19): captured before the await -- two quick zones used to label
                    // the old map's candidates with the new map's name.
                    var mapId = mapService.MapId;
                    var mapName = mapService.MapName;
                    var result = await this.dependencyInjectionContainer.HereService.GetCandidatesAsync(this.hereCap.Value);

                    var candidateSummary = string.Join(", ", result.Candidates.Select(c => $"{c.Achievement.Name} ({c.Current}/{c.Max}, {c.AchievementPoints}AP)"));

                    this.logger.Debug($"Here candidates [{result.Reason}] for map '{mapName}' (Id={mapId}): {candidateSummary}");

                    // The module may have been disabled, or the map changed again, while the lookup above
                    // was in flight -- don't queue a toast for a stale map or touch Pathing (already
                    // Detach()'d) after that point.
                    if (token.IsCancellationRequested || mapService.MapId != mapId)
                    {
                        return;
                    }

                    // Skip the very first call (right after load) -- Blish is still coming up and the log line
                    // above is enough; a toast at that moment would just be noise on every startup.
                    if (!suppressToast && this.hereToast.Value && result.Reason == HereResultReason.Ok && result.Candidates.Count > 0)
                    {
                        // Load-test found a 4-line notification (header + 3) clipped at the bottom --
                        // the notification box doesn't grow to fit; dropped to header + 2.
                        var lines = result.Candidates.Take(2).Select(c => $"{c.Achievement.Name}  {c.Current}/{c.Max}");
                        var message = $"{mapName} — {result.Candidates.Count} nearly done\n{string.Join("\n", lines)}";

                        // ScreenNotification is a Control and the cooldown dictionary is plain, so both the
                        // check and the show happen on the main thread (concurrent workers used to write
                        // the dictionary from the pool).
                        GameService.Overlay.QueueMainThreadUpdate(gameTime =>
                        {
                            if (token.IsCancellationRequested)
                            {
                                return;
                            }

                            var now = DateTime.UtcNow;

                            if (this.lastHereToastByMapId.TryGetValue(mapId, out var lastToastUtc) && now - lastToastUtc < HereToastCooldown)
                            {
                                this.logger.Debug($"Here toast: suppressed for map '{mapName}' (Id={mapId}) -- shown {(now - lastToastUtc).TotalMinutes:F1} min ago, cooldown is {HereToastCooldown.TotalMinutes} min.");
                                return;
                            }

                            this.lastHereToastByMapId[mapId] = now;
                            ScreenNotification.ShowNotification(message);
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    // Module was disabled while this map-change was still being processed.
                }
                catch (Exception ex)
                {
                    this.logger.Warn(ex, "Here: the map-change candidates lookup failed.");
                }
            });
        }

        private void AchievementTrackerService_AchievementTracked(int achievement)
        {
            this.InitializeWindow();

            if (!this.window.Visible)
            {
                this.window.Show();
            }
        }

        // The restored-tracked-set sweep the event-driven trigger below can't cover (see LoadAsync).
        // One batched achievements fetch first, so the per-achievement aligns all hit
        // BitAlignmentService's cache instead of making a single-id API call each.
        private async Task PrefetchTrackedBitAlignment(CancellationToken cancellationToken)
        {
            try
            {
                var tracked = this.dependencyInjectionContainer.AchievementTrackerService.ActiveAchievements.ToList();

                if (tracked.Count == 0)
                {
                    return;
                }

                _ = await this.dependencyInjectionContainer.BitAlignmentService.GetAchievementsAsync(tracked, cancellationToken);

                var achievementsById = this.dependencyInjectionContainer.AchievementService.AchievementsById;
                var prefetched = 0;

                foreach (var achievementId in tracked)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (achievementsById != null && achievementsById.TryGetValue(achievementId, out var achievement))
                    {
                        await this.dependencyInjectionContainer.BitAlignmentService.PrefetchAsync(achievementId, achievement, cancellationToken);
                        prefetched++;
                    }
                }

                this.logger.Info($"Bit alignment: prefetched {prefetched} of {tracked.Count} restored tracked achievement(s).");
            }
            catch (OperationCanceledException)
            {
                // Module was disabled while the sweep was in flight.
            }
            catch (Exception ex)
            {
                this.logger.Warn(ex, "Failed to prefetch bit alignment for the restored tracked set; those achievements fall back to identity mapping.");
            }
        }

        // Phase 23's prefetch-on-track trigger (the prefetch-on-open one went with the old detail window in
        // Phase 53; WikiLocationService also prefetches on demand) -- worth aligning even before its detail/Track
        // window control is ever built, since the Track window panel's embedded control needs it too.
        private void PrefetchBitAlignment(int achievementId)
        {
            var achievementsById = this.dependencyInjectionContainer.AchievementService.AchievementsById;

            if (achievementsById != null && achievementsById.TryGetValue(achievementId, out var achievement))
            {
                _ = this.dependencyInjectionContainer.BitAlignmentService.PrefetchAsync(achievementId, achievement, this.cts?.Token ?? default);
            }
        }

        // SessionSummaryService fires this on a background thread (both the baseline sweep and the live
        // diff run off-thread) -- marshal before touching AchievementTrackerService or a Control.
        private void SessionSummaryService_AchievementCompleted(int achievementId)
        {
            GameService.Overlay.QueueMainThreadUpdate(gameTime =>
            {
                // Always, hunt mode on or off -- a finished achievement has nothing left to hunt either way.
                this.dependencyInjectionContainer.HuntService.RevertForCompletion(achievementId);

                if (!this.autoUntrackCompleted.Value || !this.dependencyInjectionContainer.AchievementTrackerService.IsBeingTracked(achievementId))
                {
                    return;
                }

                var name = this.dependencyInjectionContainer.AchievementService.Achievements?.FirstOrDefault(a => a.Id == achievementId)?.Name ?? $"#{achievementId}";
                this.dependencyInjectionContainer.AchievementTrackerService.RemoveAchievement(achievementId);

                ScreenNotification.ShowNotification($"Done: {name}");
            });
        }

        // AutoSave defaults to off, so without this a crash or a killed process loses every
        // track/untrack since the last clean Unload(). Debounced so a burst of tracks/untracks writes
        // once, ~5s after the last one; linked to the module CTS so Unload() cancels a pending save.
        private void DebounceSave(int achievementId)
        {
            // Phase 56 (review item 20): a linked source holds a registration on cts.Token until it is
            // disposed, so the superseded one is disposed here rather than leaked per track/untrack.
            var superseded = this.saveDebounceCts;
            superseded?.Cancel();
            superseded?.Dispose();
            this.saveDebounceCts = CancellationTokenSource.CreateLinkedTokenSource(this.cts.Token);
            var token = this.saveDebounceCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                    this.SavePersistentInformation();
                    this.logger.Debug("Save-on-change: saved tracked achievements after a track/untrack.");
                }
                catch (OperationCanceledException)
                {
                    // Superseded by a newer track/untrack, or the module was disabled.
                }
            });
        }

        // Phase 56 (review item 3): Blish's Module.DoLoad is a Task.Run, so LoadAsync -- and everything
        // it used to build -- ran on a thread-pool thread; OnModuleLoaded is called from the game loop
        // once that task completes (Module.DoUpdate -> CheckForLoaded -> RunState = Loaded, verified
        // against the Blish HUD source 2026-09-14). The windows and the corner icon are controls, so
        // they are constructed here.
        protected override void OnModuleLoaded(EventArgs e)
        {
            // LoadAsync completes normally when the module is disabled mid-load (its
            // OperationCanceledException is swallowed), so this still runs -- with a container that
            // never finished initialising. Nothing to build in that case.
            if (this.cts is null || this.cts.IsCancellationRequested || this.dependencyInjectionContainer.AchievementItemOverviewFactory is null)
            {
                base.OnModuleLoaded(e);
                return;
            }

            this.overviewWindow = new AchievementOverviewWindow(
                this.ContentsManager,
                this.dependencyInjectionContainer.AchievementItemOverviewFactory,
                this.dependencyInjectionContainer.AchievementService,
                this.dependencyInjectionContainer.TextureService,
                this.dependencyInjectionContainer.HereService,
                this.dependencyInjectionContainer.CurrentMapService,
                this.dependencyInjectionContainer.AchievementCardFactory,
                this.dependencyInjectionContainer.HereExclusionService,
                this.dependencyInjectionContainer.AchievementTrackerService,
                this.dependencyInjectionContainer.PersistenceService,
                this.hereCap,
                this.ToggleTrackWindow)
            {
                Parent = GameService.Graphics.SpriteScreen,
                Location = new Point(100, 100),
            };

            if (this.showCornerIcon.Value)
            {
                this.CreateCornerIcon();
            }

            // Measured from LoadAsync's start to here; since Phase 56 that includes the wait for Blish's
            // game loop to notice the load task finished, which is real time the user sees but can also be
            // inflated by the overlay idling while GW2 isn't the active window.
            this.logger.Info($"Startup timing: module load to corner icon visible took {this.loadStopwatch?.ElapsedMilliseconds ?? 0} ms");

            this.showCornerIcon.SettingChanged += this.ShowCornerIcon_SettingChanged;
            this.toggleTrackWindowKeyBind.Value.Activated += this.ToggleTrackWindowKeyBind_Activated;

            if (this.dependencyInjectionContainer.PersistenceService.Get().ShowTrackWindow)
            {
                this.InitializeWindow();
                this.window.Show();
            }

            base.OnModuleLoaded(e);
        }

        protected override void Update(GameTime gameTime)
        {
            this.dependencyInjectionContainer?.InspectorWindowManager?.Update();

            if (GameService.Gw2Mumble.IsAvailable && this.window != null)
            {
                if (!GameService.GameIntegration.Gw2Instance.IsInGame || GameService.Gw2Mumble.UI.IsMapOpen)
                {
                    if (this.window.Visible)
                    {
                        this.purposelyHidden = true;
                        this.window.Hide();
                    }
                }
                else if (this.purposelyHidden)
                {
                    this.window.Show();
                    this.purposelyHidden = false;
                }
            }
        }

        /// <inheritdoc />
        protected override void Unload()
        {
            // Position/visibility at the instant Unload starts, for GitHub issue #4 (the Track window
            // sometimes restarting at top-left). Debug: still wanted, not worth a shared-log line.
            this.logger.Debug($"Unload starting: window={(this.window is null ? "null" : "exists")} Location={this.window?.Location} Visible={this.window?.Visible}");

            // Cancelled first, before anything else, so any LoadAsync/CurrentMapService_Changed work still
            // in flight stops touching state we're about to tear down below.
            this.cts?.Cancel();

            this.Gw2ApiManager.SubtokenUpdated -= this.Gw2ApiManager_SubtokenUpdated;

            if (this.dependencyInjectionContainer.SessionSummaryService != null)
            {
                this.dependencyInjectionContainer.SessionSummaryService.AchievementCompleted -= this.SessionSummaryService_AchievementCompleted;
            }

            this.logger.Info(this.dependencyInjectionContainer.SessionSummaryService?.GetSummaryLine() ?? "This session: nothing completed");
            this.SavePersistentInformation();

            // ShowCornerIcon's SettingEntry and the keybind's KeyBinding value both outlive the module
            // instance (same class as Phase 11's autoSave.SettingChanged leak) -- unsubscribe here.
            this.showCornerIcon.SettingChanged -= this.ShowCornerIcon_SettingChanged;
            this.toggleTrackWindowKeyBind.Value.Enabled = false;
            this.toggleTrackWindowKeyBind.Value.Activated -= this.ToggleTrackWindowKeyBind_Activated;

            this.cornerIcon?.Dispose();
            this.window?.Dispose();
            this.overviewWindow?.Dispose();

            if (this.huntRevertOnUnload.Value)
            {
                this.dependencyInjectionContainer.HuntService?.RevertAllForUnload();
            }

            this.dependencyInjectionContainer.HuntService?.Dispose();

            // CurrentMapService subscribes to the Blish-wide static Gw2Mumble.CurrentMap.MapChanged event;
            // without unsubscribing here, that handler (and everything it closes over -- this Module
            // instance included) keeps running after the module is disabled instead of being cleaned up,
            // which is why toasts/logging/polling kept firing on a "disabled" module.
            if (this.dependencyInjectionContainer.CurrentMapService != null)
            {
                this.dependencyInjectionContainer.CurrentMapService.Changed -= this.CurrentMapService_Changed;
                this.dependencyInjectionContainer.CurrentMapService.Dispose();
            }

            this.dependencyInjectionContainer.InspectorWindowManager?.Dispose();

            this.dependencyInjectionContainer.AchievementService?.Dispose();
            this.dependencyInjectionContainer.PersistenceService?.Dispose();
            this.dependencyInjectionContainer.TextureService?.Dispose();

            // Last, after every task that reads these tokens has been told to stop.
            this.saveDebounceCts?.Dispose();
            this.saveDebounceCts = null;
            this.cts?.Dispose();
        }

        private void SavePersistentInformation()
        {
            var location = this.window?.Location ?? new Point(-1, -1);

            // For GitHub issue #4 (see Unload). The "different window instance" hypothesis this used to
            // log a hash code for is already ruled out there.
            this.logger.Debug($"SavePersistentInformation: window={(this.window is null ? "null" : "exists")} Location=X:{location.X} Y:{location.Y} Visible={this.window?.Visible}");

            this.dependencyInjectionContainer.PersistenceService?.Save(
                location.X,
                location.Y,
                this.window?.Visible ?? false,
                this.window?.CompactSize.X ?? -1,
                this.window?.CompactSize.Y ?? -1,
                this.overviewWindow?.Size.X ?? -1,
                this.overviewWindow?.Size.Y ?? -1);
        }
    }
}
