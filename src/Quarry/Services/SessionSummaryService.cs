using Blish_HUD;
using Quarry.Interfaces;
using Quarry.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quarry.Services
{
    public class SessionSummaryService : ISessionSummaryService
    {
        private readonly IAchievementService achievementService;
        private readonly IBitAlignmentService bitAlignmentService;
        private readonly IAchievementTrackerService achievementTrackerService;
        private readonly Logger logger;

        // Accumulated across every poll for the session, not just the immediately-previous one: a poll
        // response that's ever missing an entry (Blish cache hiccup, partial response) must not make the
        // next poll see that id as "new" again and re-count it.
        private readonly HashSet<int> everSeenDoneIds = new HashSet<int>();
        private readonly Dictionary<int, int> maxBitCountById = new Dictionary<int, int>();
        private bool hasBaseline;

        // Phase 56 (review item 22): OnPlayerAchievementsLoaded runs on the main thread from a manual
        // tick and on the pool from the poll, and each one launches a diff task -- so the baseline and
        // sweep are under a lock, and the diffs (which read-modify-write Summary across an API await)
        // run one at a time.
        private readonly object baselineLock = new object();
        private readonly SemaphoreSlim diffGate = new SemaphoreSlim(1, 1);

        public SessionSummary Summary { get; private set; } = new SessionSummary();

        public event Action Changed;

        public event Action<int> AchievementCompleted;

        // Separate from everSeenDoneIds: that one governs session stats (so AP isn't double counted),
        // this one only stops the "Done:" toast repeating for something already dealt with.
        private readonly HashSet<int> completionFiredIds = new HashSet<int>();

        public SessionSummaryService(IAchievementService achievementService, IBitAlignmentService bitAlignmentService, IAchievementTrackerService achievementTrackerService, Logger logger)
        {
            this.achievementService = achievementService;
            this.bitAlignmentService = bitAlignmentService;
            this.achievementTrackerService = achievementTrackerService;
            this.logger = logger;

            this.achievementService.PlayerAchievementsLoaded += this.OnPlayerAchievementsLoaded;
        }

        public string GetSummaryLine()
        {
            var parts = new List<string>();

            if (this.Summary.ApGained > 0)
            {
                parts.Add($"+{this.Summary.ApGained} AP");
            }

            if (this.Summary.CompletedAchievementNames.Count > 0)
            {
                parts.Add($"{this.Summary.CompletedAchievementNames.Count} completed");
            }

            if (this.Summary.BitsTicked > 0)
            {
                parts.Add($"{this.Summary.BitsTicked} steps");
            }

            return parts.Count == 0 ? null : $"This session: {string.Join(" · ", parts)}";
        }

        private void OnPlayerAchievementsLoaded()
        {
            var current = this.BuildSnapshot();
            var fireCompleted = new List<int>();
            var runDiff = false;

            lock (this.baselineLock)
            {
                if (!this.hasBaseline)
                {
                    if (this.achievementService.PlayerAchievements is null)
                    {
                        // ToggleManualCompleteStatus fires this event too, and can do so before the first
                        // real API load ever completes; there's nothing real to baseline against yet.
                        return;
                    }

                    // First real load is the session baseline, not a diff. Seed the ever-seen state from it
                    // so later polls diff against "have we ever seen this done / this many bits" rather than
                    // just the immediately-previous poll.
                    foreach (var entry in current)
                    {
                        if (entry.Value.Done)
                        {
                            _ = this.everSeenDoneIds.Add(entry.Key);

                            // Already Done at load time but still tracked from a previous session -- fire the
                            // completion event now (not counted toward this session's AP/completed stats) so
                            // a stale tracked set cleans itself instead of sitting there forever.
                            if (this.achievementTrackerService.IsBeingTracked(entry.Key))
                            {
                                fireCompleted.Add(entry.Key);
                            }
                        }

                        this.maxBitCountById[entry.Key] = entry.Value.BitCount;
                    }

                    this.hasBaseline = true;
                }
                else
                {
                    // Independently of the stats diff below: anything still tracked that is now finished
                    // should leave the Track window. This can't ride on everSeenDoneIds, which is a
                    // one-way latch for session stats -- if an achievement is first seen done at a moment
                    // when it isn't tracked (or while completion data is briefly incomplete), that latch
                    // is spent and auto-untrack could never fire for it again. Its own latch, keyed on
                    // having actually acted, and only ever consulted for achievements tracked right now.
                    fireCompleted.AddRange(this.SweepFinishedTrackedAchievements());
                    runDiff = true;
                }
            }

            // Subscribers (Module marshals to the main thread) are invoked outside the lock.
            foreach (var id in fireCompleted)
            {
                this.AchievementCompleted?.Invoke(id);
            }

            if (runDiff)
            {
                _ = Task.Run(() => this.DiffAndUpdateAsync(current));
            }
        }

        // Caller holds baselineLock. Returns the ids to raise AchievementCompleted for.
        private List<int> SweepFinishedTrackedAchievements()
        {
            var finished = new List<int>();

            foreach (var id in this.achievementTrackerService.ActiveAchievements.ToList())
            {
                if (this.completionFiredIds.Contains(id) || !this.achievementService.HasFinishedAchievement(id))
                {
                    continue;
                }

                _ = this.completionFiredIds.Add(id);
                finished.Add(id);
            }

            return finished;
        }

        private Dictionary<int, (int Current, bool Done, int BitCount)> BuildSnapshot()
        {
            var result = new Dictionary<int, (int, bool, int)>();

            if (this.achievementService.PlayerAchievements != null)
            {
                foreach (var achievement in this.achievementService.PlayerAchievements)
                {
                    // HasFinishedAchievement, not achievement.Done: a manually completed achievement is
                    // finished for our purposes the moment the last step is ticked, so the "Done:" toast
                    // and auto-untrack fire then rather than minutes later when the API agrees.
                    result[achievement.Id] = (achievement.Current, this.achievementService.HasFinishedAchievement(achievement.Id), achievement.Bits?.Count ?? 0);
                }
            }

            return result;
        }

        private async Task DiffAndUpdateAsync(Dictionary<int, (int Current, bool Done, int BitCount)> current)
        {
            await this.diffGate.WaitAsync();

            try
            {
                var newlyDoneIds = new List<int>();
                var bitsTicked = 0;

                foreach (var entry in current)
                {
                    var previousMaxBitCount = this.maxBitCountById.TryGetValue(entry.Key, out var p) ? p : 0;

                    if (!this.everSeenDoneIds.Contains(entry.Key) && entry.Value.Done)
                    {
                        newlyDoneIds.Add(entry.Key);
                        _ = this.everSeenDoneIds.Add(entry.Key);
                    }

                    if (entry.Value.BitCount > previousMaxBitCount)
                    {
                        bitsTicked += entry.Value.BitCount - previousMaxBitCount;
                        this.maxBitCountById[entry.Key] = entry.Value.BitCount;
                    }
                }

                if (newlyDoneIds.Count == 0 && bitsTicked == 0)
                {
                    return;
                }

                var apGained = 0;
                var completedNames = new List<string>();

                if (newlyDoneIds.Count > 0)
                {
                    var apiAchievements = await this.FetchAchievementPointsAsync(newlyDoneIds);

                    foreach (var id in newlyDoneIds)
                    {
                        if (apiAchievements.TryGetValue(id, out var points))
                        {
                            apGained += points;
                        }

                        var name = this.achievementService.Achievements?.FirstOrDefault(a => a.Id == id)?.Name;
                        completedNames.Add(name ?? $"#{id}");
                    }
                }

                this.Summary = new SessionSummary
                {
                    ApGained = this.Summary.ApGained + apGained,
                    CompletedAchievementNames = this.Summary.CompletedAchievementNames.Concat(completedNames).ToList(),
                    BitsTicked = this.Summary.BitsTicked + bitsTicked,
                };

                this.Changed?.Invoke();

                foreach (var id in newlyDoneIds)
                {
                    this.AchievementCompleted?.Invoke(id);
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Session summary: failed to update.");
            }
            finally
            {
                _ = this.diffGate.Release();
            }
        }

        // Phase 23: shares BitAlignmentService's id->Achievement cache/fetch instead of its own.
        private async Task<Dictionary<int, int>> FetchAchievementPointsAsync(IReadOnlyList<int> ids)
        {
            var achievements = await this.bitAlignmentService.GetAchievementsAsync(ids);
            return achievements.ToDictionary(kv => kv.Key, kv => kv.Value.Tiers?.Sum(t => t.Points) ?? 0);
        }
    }
}
