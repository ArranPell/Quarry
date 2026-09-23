using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Modules.Managers;
using Quarry.Interfaces;
using Quarry.Models.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quarry.Services
{
    public class PersistenceService : IPersistenceService
    {
        private const string SAVE_FILE_NAME = "persistanceStorage.json";
        private readonly DirectoriesManager directoriesManager;
        private readonly AchievementTrackerService achievementTrackerService;
        private readonly Logger logger;
        private readonly AchievementService achievementService;
        private readonly IHuntService huntService;
        private readonly IHereExclusionService hereExclusionService;
        private Storage storage;
        private Task autoSaveTask;
        private CancellationTokenSource autoSaveCancellationTokenSource;
        private DateTime? lastKnownFileWriteTimeUtc;
        private readonly Blish_HUD.Settings.SettingEntry<bool> autoSaveSetting;

        public event Action AutoSave;

        public PersistenceService(
            DirectoriesManager directoriesManager,
            AchievementTrackerService achievementTrackerService,
            Logger logger,
            AchievementService achievementService,
            IHuntService huntService,
            IHereExclusionService hereExclusionService,
            Blish_HUD.Settings.SettingEntry<bool> autoSave)
        {
            this.directoriesManager = directoriesManager;
            this.achievementTrackerService = achievementTrackerService;
            this.logger = logger;
            this.achievementService = achievementService;
            this.huntService = huntService;
            this.hereExclusionService = hereExclusionService;
            this.autoSaveSetting = autoSave;
            this.autoSaveSetting.SettingChanged += this.AutoSaveSetting_SettingChanged;

            if (autoSave.Value)
            {
                this.InitializeAutoSaveTask();
            }
        }

        private void AutoSaveSetting_SettingChanged(object sender, Blish_HUD.ValueChangedEventArgs<bool> e)
        {
            if (e.NewValue)
            {
                this.InitializeAutoSaveTask();
            }
            else
            {
                this.ResetAutoSaveTask();
            }
        }

        public void Dispose()
        {
            this.autoSaveSetting.SettingChanged -= this.AutoSaveSetting_SettingChanged;
            this.ResetAutoSaveTask();
        }

        private void ResetAutoSaveTask()
        {
            if (this.autoSaveTask != null)
            {
                this.autoSaveCancellationTokenSource.Cancel();
                this.autoSaveCancellationTokenSource.Dispose();
                this.autoSaveCancellationTokenSource = null;
                this.autoSaveTask = null;
            }
        }

        private void InitializeAutoSaveTask()
        {
            this.ResetAutoSaveTask();

            this.autoSaveCancellationTokenSource = new CancellationTokenSource();
            this.autoSaveTask = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5), this.autoSaveCancellationTokenSource.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        return;
                    }

                    try
                    {
                        this.AutoSave?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        this.logger.Error(ex, "Exception occured during autosave; will retry next cycle");
                    }
                }
            }, this.autoSaveCancellationTokenSource.Token);
        }

        private string GetSaveFilePath()
        {
            var safeFolder = this.directoriesManager.GetFullDirectoryPath(ModuleConstants.DataDirectoryName);
            return System.IO.Path.Combine(safeFolder, SAVE_FILE_NAME);
        }

        public void Save(int achievementTrackWindowLocationX, int achievementTrackWindowLocationY, bool showTrackWindow, int trackWindowCompactWidth, int trackWindowCompactHeight, int overviewWindowWidth = -1, int overviewWindowHeight = -1)
        {
            var file = this.GetSaveFilePath();

            if (System.IO.File.Exists(file) && this.lastKnownFileWriteTimeUtc.HasValue &&
                System.IO.File.GetLastWriteTimeUtc(file) > this.lastKnownFileWriteTimeUtc.Value)
            {
                this.logger.Info($"{SAVE_FILE_NAME} changed on disk since our last save (likely an external edit) -- merging before writing.");
                this.Reload();
            }

            try
            {
                var storage = new Storage();

                // Snapshot before enumerating -- Save() can run on the autosave background thread while
                // the main thread is concurrently mutating these same collections (tracking/untracking
                // achievements).
                var trackedAchievements = this.achievementTrackerService.ActiveAchievements.ToList();

                // Phase 56 (review item 23): a deep copy under the same lock the two AchievementService
                // mutators take -- this runs on the autosave/debounce thread while a tick or the poll
                // can be editing the live dictionary, and a collision threw the whole save away.
                lock (this.achievementService.ManualCompletedSync)
                {
                    storage.ManualCompletedAchievements = this.achievementService.ManualCompletedAchievements
                        .ToDictionary(kv => kv.Key, kv => new List<int>(kv.Value));
                }

                storage.TrackedAchievements.AddRange(trackedAchievements);

                storage.HuntEnabledNamespaces = this.huntService.HuntEnabledNamespaces.ToDictionary(kv => kv.Key, kv => new List<string>(kv.Value));

                // Phase 31: contributed from the service, not carried over from the previous Storage --
                // this method rebuilds Storage from scratch every time.
                storage.HiddenAchievements = this.hereExclusionService.HiddenAchievementIds.ToList();
                storage.SnoozedAchievements = this.hereExclusionService.SnoozedUntilUtc.ToDictionary(kv => kv.Key, kv => kv.Value);

                storage.TrackWindowLocationX = achievementTrackWindowLocationX;
                storage.TrackWindowLocationY = achievementTrackWindowLocationY;
                storage.ShowTrackWindow = showTrackWindow;
                storage.TrackWindowCompactWidth = trackWindowCompactWidth;
                storage.TrackWindowCompactHeight = trackWindowCompactHeight;

                // Phase 42: this method rebuilds Storage from scratch every call, but not every caller
                // knows the Quarry window's current size -- AchievementTrackWindow.PersistWindowState()
                // (the Target List's own "Save now") calls this too, with the sentinel default, and would
                // otherwise wipe the Quarry size back to -1 every time. Preserve the last-known value
                // instead of overwriting it with a caller's "I don't know" default.
                storage.OverviewWindowWidth = overviewWindowWidth > 0 ? overviewWindowWidth : this.Get().OverviewWindowWidth;
                storage.OverviewWindowHeight = overviewWindowHeight > 0 ? overviewWindowHeight : this.Get().OverviewWindowHeight;

                _ = System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));

                System.IO.File.WriteAllText(file, System.Text.Json.JsonSerializer.Serialize(storage));
                this.storage = storage;
                this.lastKnownFileWriteTimeUtc = System.IO.File.GetLastWriteTimeUtc(file);
            }
            catch (UnauthorizedAccessException ex)
            {
                // Phase 56 (review item 12): Controlled Folder Access on a OneDrive-redirected Documents
                // folder is the usual cause -- every track/untrack, hide/snooze and window position was
                // being lost with only a log line to show for it. Blish's dialog de-duplicates by path, so
                // the 5-minute autosave doesn't nag. Warn, not Error (2.0.4): the user's environment, not
                // ours to fix, and the dialog already tells them.
                this.logger.Warn(ex, $"Access denied writing {SAVE_FILE_NAME}; nothing tracked this session will survive a restart.");
                Blish_HUD.Debug.Contingency.NotifyFileSaveAccessDenied(file, "save your tracked achievements");
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Exception occured on saving persistent information");
            }
        }

        public Storage Get()
        {
            try
            {
                if (this.storage is null)
                {
                    this.storage = this.ReadStorageFromDisk();
                }

                return this.storage;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Exception occured on reading persistent information");
                return new Storage();
            }
        }

        // Re-reads persistanceStorage.json from disk (bypassing the Get() cache) and diffs
        // TrackedAchievements into the live AchievementTrackerService, so an external edit --
        // e.g. Sync-Gw2BlishTracker.ps1 -- shows up without restarting Blish.
        public void Reload()
        {
            try
            {
                this.storage = this.ReadStorageFromDisk();

                // Merge, don't replace: add anything newly tracked in the file, but never untrack
                // something the live session already has tracked just because the file doesn't
                // (yet) have it too -- e.g. it was tracked in Blish after the file was last written.
                var mergedCount = 0;
                foreach (var achievementId in this.storage.TrackedAchievements)
                {
                    if (this.achievementService.Achievements != null && !this.achievementService.Achievements.Any(x => x.Id == achievementId))
                    {
                        this.logger.Warn($"{SAVE_FILE_NAME}: rejecting tracked id {achievementId} -- not found in wiki achievement data.");
                        continue;
                    }

                    _ = this.achievementTrackerService.TrackAchievement(achievementId);
                    mergedCount++;
                }

                this.logger.Info($"Reloaded {SAVE_FILE_NAME}: merged {mergedCount} tracked achievement(s) from disk.");
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Exception occured on reloading persistent information");
            }
        }

        private Storage ReadStorageFromDisk()
        {
            var file = this.GetSaveFilePath();

            if (!System.IO.File.Exists(file))
            {
                return new Storage();
            }

            try
            {
                var result = System.Text.Json.JsonSerializer.Deserialize<Storage>(System.IO.File.ReadAllText(file));
                this.lastKnownFileWriteTimeUtc = System.IO.File.GetLastWriteTimeUtc(file);
                return result;
            }
            catch (Exception ex)
            {
                var badFile = file + ".bad";

                try
                {
                    System.IO.File.Copy(file, badFile, overwrite: true);
                }
                catch (Exception copyEx)
                {
                    this.logger.Warn(copyEx, $"Failed to back up corrupt {SAVE_FILE_NAME} to \"{badFile}\"");
                }

                this.logger.Warn(ex, $"{SAVE_FILE_NAME} is corrupt; backed up to \"{badFile}\" and continuing with an empty tracked set.");

                // Phase 56 (review item 24): the next Save() overwrites the original, so say where the
                // backup is while it still matters. This can run on the autosave thread via Reload().
                GameService.Overlay.QueueMainThreadUpdate(gameTime =>
                    ScreenNotification.ShowNotification($"Quarry: saved data was unreadable and was reset. A backup was kept as {SAVE_FILE_NAME}.bad", ScreenNotification.NotificationType.Warning, null, 8));

                this.lastKnownFileWriteTimeUtc = System.IO.File.GetLastWriteTimeUtc(file);
                return new Storage();
            }
        }
    }
}
