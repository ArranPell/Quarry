using Blish_HUD;
using Blish_HUD.Settings;
using Quarry.Interfaces;
using Quarry.Models.Markers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quarry.Services
{
    // Phase 16: owns the tracked <-> Pathing category namespace logic. Two dictionaries with the same
    // shape and revert rule -- enabledNamespacesByAchievementId is the persisted, tracked-achievement set
    // (mirrored to Storage.HuntEnabledNamespaces by PersistenceService); peekNamespacesByAchievementId is
    // the in-memory, one-off Here-card "Guided" click, never persisted and reverted on map change or when
    // the achievement it belongs to gets tracked (see ApplyForAchievement's takeover).
    public class HuntService : IHuntService
    {
        private readonly IPathingBridge pathingBridge;
        private readonly IMarkerPackIndexService markerPackIndexService;
        private readonly IAchievementTrackerService achievementTrackerService;
        private readonly ICurrentMapService currentMapService;
        private readonly SettingEntry<bool> huntMode;
        private readonly Logger logger;

        // Both dictionaries are reached from two threads: the UI thread (track/untrack clicks, the Here
        // card's peek, the hunt-mode setting) and a threadpool continuation (OnMapChanged, which
        // CurrentMapService raises after awaiting the maps endpoint). Every entry point below takes this
        // lock -- an unsynchronised Dictionary corrupts rather than throwing, and the revert bookkeeping
        // is the one piece of state that leaves Pathing categories stuck on when it goes wrong.
        private readonly object stateLock = new object();
        private readonly Dictionary<int, List<string>> enabledNamespacesByAchievementId = new Dictionary<int, List<string>>();
        private readonly Dictionary<int, List<string>> peekNamespacesByAchievementId = new Dictionary<int, List<string>>();

        // A snapshot, not the live dictionary: PersistenceService.Save walks this while a map change can
        // be mutating it.
        public IReadOnlyDictionary<int, List<string>> HuntEnabledNamespaces
        {
            get
            {
                lock (this.stateLock)
                {
                    return this.enabledNamespacesByAchievementId.ToDictionary(kv => kv.Key, kv => new List<string>(kv.Value));
                }
            }
        }

        public bool CanPeek => this.huntMode.Value && this.pathingBridge.IsAvailable;

        public HuntService(IPathingBridge pathingBridge, IMarkerPackIndexService markerPackIndexService, IAchievementTrackerService achievementTrackerService, ICurrentMapService currentMapService, SettingEntry<bool> huntMode, Logger logger)
        {
            this.pathingBridge = pathingBridge;
            this.markerPackIndexService = markerPackIndexService;
            this.achievementTrackerService = achievementTrackerService;
            this.currentMapService = currentMapService;
            this.huntMode = huntMode;
            this.logger = logger;

            this.achievementTrackerService.AchievementTracked += this.OnAchievementTracked;
            this.achievementTrackerService.AchievementUntracked += this.OnAchievementUntracked;
            this.currentMapService.Changed += this.OnMapChanged;
            this.huntMode.SettingChanged += this.OnHuntModeChanged;
        }

        // Restores the persisted bookkeeping only -- never touches Pathing here. Pathing persists its own
        // category state, so leaving routes on across a restart is the point (HuntRevertOnUnload default off).
        public void Load(IPersistenceService persistenceService)
        {
            lock (this.stateLock)
            {
                foreach (var entry in persistenceService.Get().HuntEnabledNamespaces)
                {
                    this.enabledNamespacesByAchievementId[entry.Key] = new List<string>(entry.Value);
                }
            }
        }

        public void Peek(int achievementId)
        {
            if (!this.CanPeek)
            {
                return;
            }

            lock (this.stateLock)
            {
                if (this.enabledNamespacesByAchievementId.ContainsKey(achievementId))
                {
                    return;
                }

                this.ApplyNamespaces(achievementId, this.peekNamespacesByAchievementId);
            }
        }

        public void RevertForCompletion(int achievementId)
        {
            lock (this.stateLock)
            {
                this.RevertForAchievement(achievementId, this.enabledNamespacesByAchievementId);
                this.RevertForAchievement(achievementId, this.peekNamespacesByAchievementId);
            }
        }

        public void RevertAllForUnload()
        {
            lock (this.stateLock)
            {
                this.RevertAll();
            }
        }

        private void RevertAll()
        {
            foreach (var id in this.enabledNamespacesByAchievementId.Keys.ToList())
            {
                this.RevertForAchievement(id, this.enabledNamespacesByAchievementId);
            }

            foreach (var id in this.peekNamespacesByAchievementId.Keys.ToList())
            {
                this.RevertForAchievement(id, this.peekNamespacesByAchievementId);
            }
        }

        private void OnAchievementTracked(int achievementId)
        {
            if (!this.huntMode.Value)
            {
                return;
            }

            lock (this.stateLock)
            {
                this.ApplyForAchievement(achievementId);
            }
        }

        private void OnAchievementUntracked(int achievementId)
        {
            lock (this.stateLock)
            {
                this.RevertForAchievement(achievementId, this.enabledNamespacesByAchievementId);
            }
        }

        private void OnHuntModeChanged(object sender, ValueChangedEventArgs<bool> e)
        {
            lock (this.stateLock)
            {
                this.RetryPendingReverts();

                if (e.NewValue)
                {
                    foreach (var id in this.achievementTrackerService.ActiveAchievements.ToList())
                    {
                        this.ApplyForAchievement(id);
                    }
                }
                else
                {
                    this.RevertAll();
                }
            }
        }

        private void OnMapChanged()
        {
            lock (this.stateLock)
            {
                this.RetryPendingReverts();

                foreach (var id in this.peekNamespacesByAchievementId.Keys.ToList())
                {
                    this.RevertForAchievement(id, this.peekNamespacesByAchievementId);
                }
            }
        }

        // Tracking an achievement that's currently peeked takes over the peek's already-enabled
        // namespaces (moves them from the peek dict to the persisted one) instead of re-evaluating from
        // scratch -- avoids a flicker and, more importantly, avoids losing the revert record: a
        // re-evaluation would find those namespaces already active and (correctly, per the rule) not
        // record them, which would leave them stuck on once this achievement is later untracked.
        private void ApplyForAchievement(int achievementId)
        {
            if (!this.pathingBridge.IsAvailable)
            {
                return;
            }

            if (this.peekNamespacesByAchievementId.TryGetValue(achievementId, out var peeked))
            {
                this.enabledNamespacesByAchievementId[achievementId] = peeked;
                _ = this.peekNamespacesByAchievementId.Remove(achievementId);
                return;
            }

            this.ApplyNamespaces(achievementId, this.enabledNamespacesByAchievementId);
        }

        private void ApplyNamespaces(int achievementId, Dictionary<int, List<string>> target)
        {
            if (!this.markerPackIndexService.TryGet(achievementId, out AchievementRoute route))
            {
                return;
            }

            var flipped = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var leafNamespace in route.Objectives.Select(o => o.Namespace).Where(ns => !string.IsNullOrEmpty(ns)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var ns in WalkAncestors(leafNamespace))
                {
                    if (!seen.Add(ns))
                    {
                        continue;
                    }

                    if (this.pathingBridge.TryGetInactive(ns, out var inactive) && inactive && this.pathingBridge.TrySetInactive(ns, false))
                    {
                        flipped.Add(ns);
                    }
                }
            }

            if (flipped.Count == 0)
            {
                return;
            }

            target[achievementId] = flipped;
            this.logger.Debug($"Hunt: on {achievementId} — enabled {flipped.Count} categories ({string.Join(", ", flipped.Take(3))}{(flipped.Count > 3 ? "…" : "")}).");
        }

        // Phase 56 (review item 8): the record used to be dropped before Pathing was asked, so an
        // untrack while Pathing was disabled or not yet ready lost the bookkeeping and left the routes on
        // forever. Now a namespace only leaves the record once Pathing actually turned it off; what's left
        // is retried by RetryPendingReverts on the next map change or hunt-mode toggle.
        private void RevertForAchievement(int achievementId, Dictionary<int, List<string>> source)
        {
            if (!source.TryGetValue(achievementId, out var namespaces))
            {
                return;
            }

            var stillPending = new List<string>();

            foreach (var ns in namespaces)
            {
                if (this.IsNamespaceStillNeeded(ns, achievementId))
                {
                    continue;
                }

                if (!this.pathingBridge.TrySetInactive(ns, true))
                {
                    stillPending.Add(ns);
                }
            }

            if (stillPending.Count == 0)
            {
                _ = source.Remove(achievementId);
            }
            else
            {
                source[achievementId] = stillPending;
                this.logger.Debug($"Hunt: {stillPending.Count} categorie(s) for {achievementId} couldn't be turned off (Pathing unavailable); will retry.");
            }
        }

        // Caller holds stateLock. Enabled-dict entries for achievements that are no longer tracked are
        // reverts that failed earlier (or a tracked set edited outside Blish); peeks are reverted on every
        // map change anyway, so a failed peek revert retries itself.
        private void RetryPendingReverts()
        {
            if (!this.pathingBridge.IsAvailable)
            {
                return;
            }

            foreach (var id in this.enabledNamespacesByAchievementId.Keys.ToList())
            {
                if (!this.achievementTrackerService.IsBeingTracked(id))
                {
                    this.RevertForAchievement(id, this.enabledNamespacesByAchievementId);
                }
            }
        }

        private bool IsNamespaceStillNeeded(string ns, int excludingAchievementId)
        {
            bool ContainsNamespace(Dictionary<int, List<string>> dict)
                => dict.Any(kv => kv.Key != excludingAchievementId && kv.Value.Any(x => string.Equals(x, ns, StringComparison.OrdinalIgnoreCase)));

            return ContainsNamespace(this.enabledNamespacesByAchievementId) || ContainsNamespace(this.peekNamespacesByAchievementId);
        }

        // Namespace segments, "a.b.c" -> "a.b.c", "a.b", "a" -- Pathing treats a category as inactive when
        // it or any ancestor is inactive, so enabling a leaf under an unticked parent shows nothing.
        private static IEnumerable<string> WalkAncestors(string ns)
        {
            var parts = ns.Split('.');
            for (var i = parts.Length; i >= 1; i--)
            {
                yield return string.Join(".", parts.Take(i));
            }
        }

        public void Dispose()
        {
            this.achievementTrackerService.AchievementTracked -= this.OnAchievementTracked;
            this.achievementTrackerService.AchievementUntracked -= this.OnAchievementUntracked;
            this.currentMapService.Changed -= this.OnMapChanged;
            this.huntMode.SettingChanged -= this.OnHuntModeChanged;
        }
    }
}
