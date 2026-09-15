using Blish_HUD;
using Quarry.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quarry.Services
{
    // Phase 31. Reached from two threads -- the UI thread (the card's menu) and a threadpool
    // continuation (HereService.ComputeCandidatesAsync asking IsExcluded, PersistenceService.Save on
    // the autosave task) -- so every entry point takes the lock, the same rule HuntService follows.
    public class HereExclusionService : IHereExclusionService
    {
        private readonly Logger logger;
        private readonly object stateLock = new object();
        private readonly HashSet<int> hidden = new HashSet<int>();
        private readonly Dictionary<int, DateTime> snoozedUntilUtc = new Dictionary<int, DateTime>();

        public event Action Changed;

        public HereExclusionService(Logger logger)
        {
            this.logger = logger;
        }

        // Snapshots, not the live collections: PersistenceService.Save walks these while a click can be
        // mutating them.
        public IReadOnlyCollection<int> HiddenAchievementIds
        {
            get
            {
                lock (this.stateLock)
                {
                    return this.hidden.ToList();
                }
            }
        }

        public IReadOnlyDictionary<int, DateTime> SnoozedUntilUtc
        {
            get
            {
                lock (this.stateLock)
                {
                    this.PruneExpired(DateTime.UtcNow);
                    return this.snoozedUntilUtc.ToDictionary(kv => kv.Key, kv => kv.Value);
                }
            }
        }

        public int TotalExcludedCount
        {
            get
            {
                lock (this.stateLock)
                {
                    this.PruneExpired(DateTime.UtcNow);
                    return this.hidden.Count + this.snoozedUntilUtc.Count;
                }
            }
        }

        public void Hide(int achievementId)
        {
            lock (this.stateLock)
            {
                _ = this.snoozedUntilUtc.Remove(achievementId);

                if (!this.hidden.Add(achievementId))
                {
                    return;
                }
            }

            this.logger.Debug($"Here: hiding achievement {achievementId} (Not interested).");
            this.Changed?.Invoke();
        }

        public void Snooze(int achievementId)
        {
            // The next daily reset: midnight UTC, which is what a GW2 player means by "today".
            var until = DateTime.UtcNow.Date.AddDays(1);

            lock (this.stateLock)
            {
                _ = this.hidden.Remove(achievementId);
                this.snoozedUntilUtc[achievementId] = until;
            }

            this.logger.Debug($"Here: snoozing achievement {achievementId} until {until:o} (Not today).");
            this.Changed?.Invoke();
        }

        public void Unhide(int achievementId)
        {
            lock (this.stateLock)
            {
                var removed = this.hidden.Remove(achievementId);
                removed |= this.snoozedUntilUtc.Remove(achievementId);

                if (!removed)
                {
                    return;
                }
            }

            this.logger.Debug($"Here: un-hiding achievement {achievementId}.");
            this.Changed?.Invoke();
        }

        public bool IsExcluded(int achievementId, DateTime nowUtc)
        {
            lock (this.stateLock)
            {
                if (this.hidden.Contains(achievementId))
                {
                    return true;
                }

                // Lazily, never on a timer: a snooze that lapses mid-session shows up on the next map
                // change or achievements poll, which is soon enough.
                if (this.snoozedUntilUtc.TryGetValue(achievementId, out var until))
                {
                    if (until > nowUtc)
                    {
                        return true;
                    }

                    _ = this.snoozedUntilUtc.Remove(achievementId);
                }

                return false;
            }
        }

        public bool IsSnoozed(int achievementId, DateTime nowUtc)
        {
            lock (this.stateLock)
            {
                return this.snoozedUntilUtc.TryGetValue(achievementId, out var until) && until > nowUtc;
            }
        }

        public void Load(IPersistenceService persistenceService)
        {
            var storage = persistenceService.Get();

            lock (this.stateLock)
            {
                foreach (var achievementId in storage.HiddenAchievements)
                {
                    _ = this.hidden.Add(achievementId);
                }

                foreach (var entry in storage.SnoozedAchievements)
                {
                    this.snoozedUntilUtc[entry.Key] = entry.Value;
                }

                this.PruneExpired(DateTime.UtcNow);

                this.logger.Info($"HereExclusionService: loaded {this.hidden.Count} hidden and {this.snoozedUntilUtc.Count} snoozed achievement(s).");
            }
        }

        // Caller holds stateLock.
        private void PruneExpired(DateTime nowUtc)
        {
            var expired = this.snoozedUntilUtc.Where(kv => kv.Value <= nowUtc).Select(kv => kv.Key).ToList();

            foreach (var achievementId in expired)
            {
                _ = this.snoozedUntilUtc.Remove(achievementId);
            }
        }
    }
}
