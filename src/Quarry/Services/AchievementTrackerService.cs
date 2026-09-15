using Blish_HUD;
using Blish_HUD.Settings;
using Quarry.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quarry.Services
{
    public class AchievementTrackerService : IAchievementTrackerService
    {
        private const int MaxTrackedAchievements = 15;

        private readonly List<int> activeAchievements;
        private readonly Logger logger;
        private readonly SettingEntry<bool> limitAchievement;

        public event Action<int> AchievementTracked;

        public event Action<int> AchievementUntracked;

        public IReadOnlyList<int> ActiveAchievements => this.activeAchievements.AsReadOnly();

        // Phase 32: how many further TrackAchievement calls will succeed -- int.MaxValue when the
        // cap setting is off. Lets Here label its "Track these (N)" button before anything is clicked.
        public int FreeSlots => this.limitAchievement.Value
            ? Math.Max(0, MaxTrackedAchievements - this.activeAchievements.Count)
            : int.MaxValue;

        public AchievementTrackerService(Logger logger, SettingEntry<bool> limitAchievement)
        {
            this.activeAchievements = new List<int>();
            this.logger = logger;
            this.limitAchievement = limitAchievement;
        }

        public bool TrackAchievement(int achievement)
        {
            if (!this.limitAchievement.Value || this.activeAchievements.Count < MaxTrackedAchievements)
            {
                if (!this.activeAchievements.Contains(achievement))
                {
                    this.activeAchievements.Add(achievement);

                    // PersistenceService.Reload() (Phase 12) can call this from the autosave background
                    // thread; the event builds Track-window panels, so marshal it to the main thread.
                    GameService.Overlay.QueueMainThreadUpdate(gameTime => this.AchievementTracked?.Invoke(achievement));
                }
                return true;
            }

            return false;
        }

        public bool IsBeingTracked(int achievement)
            => this.activeAchievements.Contains(achievement);

        public void RemoveAchievement(int achievement)
        {
            _ = this.activeAchievements.Remove(achievement);
            this.AchievementUntracked?.Invoke(achievement);
        }

        public void Load(IPersistenceService persistenceService)
        {
            try
            {
                foreach (var item in persistenceService.Get().TrackedAchievements.Distinct())
                {
                    this.activeAchievements.Add(item);
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Exception occured on restoring tracked achievements");
            }
        }
    }
}
