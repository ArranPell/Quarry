using Blish_HUD;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Quarry.Interfaces;
using Quarry.Models;
using Quarry.WikiData.Achievement;
using Gw2Sharp.WebApi.V2.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Quarry.Services
{
    public class HereService : IHereService
    {
        // Keep in sync with Get-Gw2NearlyDone.ps1 in the parent project.
        private static readonly string[] ExcludedGroupNames = { "World vs. World", "Player vs. Player" };
        private const string ExcludedCategoryName = "Slayer";
        private const string LivingWorldMapCategoriesFileName = "here_map_categories.json";

        private readonly IAchievementService achievementService;
        private readonly ICurrentMapService currentMapService;
        private readonly Gw2ApiManager gw2ApiManager;
        private readonly IBitAlignmentService bitAlignmentService;
        private readonly IMarkerPackIndexService markerPackIndexService;
        private readonly INearestObjectiveService nearestObjectiveService;
        private readonly IHereExclusionService hereExclusionService;
        private readonly SettingEntry<HereGuidanceFilter> guidanceFilter;
        private readonly Logger logger;
        private readonly ContentsManager contentsManager;
        private IReadOnlyDictionary<string, IReadOnlyList<string>> livingWorldMapCategories = new Dictionary<string, IReadOnlyList<string>>();

        // Module and HereView each ask for the current map's candidates on every map change; this shares
        // one in-flight/completed result across both until something that could change the answer
        // happens (map change, a fresh player-achievements poll).
        private readonly object cacheLock = new object();
        private int cachedMapId = int.MinValue;
        private int cachedMax = -1;
        // Part of the cache key rather than a SettingChanged subscription: a SettingEntry is Blish-owned
        // and outlives the module instance, so subscribing here would keep this service (and everything it
        // references) alive across a disable/enable -- the same leak Phase 11 fixed for autoSave.
        private HereGuidanceFilter cachedFilter = (HereGuidanceFilter)(-1);
        private Task<HereResult> cachedTask;

        // Phase 33b: the map-independent "Anywhere: closest to done" list has its own cache, invalidated
        // only by a fresh player-achievements poll or a hide/snooze -- deliberately *not* by map change,
        // which is the whole point of it.
        private readonly object anywhereCacheLock = new object();
        private int anywhereCachedMax = -1;
        private Task<IReadOnlyList<HereCandidate>> anywhereCachedTask;

        // Phase 56 (review item 11): the startup alignment sweep raises AlignmentLoaded once per
        // restored tracked achievement in quick succession; the cache goes immediately on each, the
        // views are told once, after the burst settles.
        private static readonly TimeSpan InvalidateNotifyDelay = TimeSpan.FromMilliseconds(400);
        private int notifySequence;

        public event Action CandidatesInvalidated;

        public HereService(IAchievementService achievementService, ICurrentMapService currentMapService, Gw2ApiManager gw2ApiManager, IBitAlignmentService bitAlignmentService, IMarkerPackIndexService markerPackIndexService, INearestObjectiveService nearestObjectiveService, IHereExclusionService hereExclusionService, SettingEntry<HereGuidanceFilter> guidanceFilter, Logger logger, ContentsManager contentsManager)
        {
            this.achievementService = achievementService;
            this.currentMapService = currentMapService;
            this.gw2ApiManager = gw2ApiManager;
            this.bitAlignmentService = bitAlignmentService;
            this.markerPackIndexService = markerPackIndexService;
            this.nearestObjectiveService = nearestObjectiveService;
            this.hereExclusionService = hereExclusionService;
            this.guidanceFilter = guidanceFilter;
            this.logger = logger;
            this.contentsManager = contentsManager;

            this.achievementService.ApiAchievementsLoaded += this.ValidateLivingWorldMapCategories;
            // Phase 56 (review item 11): a NotLoaded result computed before the API categories arrived
            // used to stay cached until the next map change; and nothing invalidated on alignment, so
            // the first result on a map kept MapRowToBit's identity fallback for the ~10 % of
            // achievements whose wiki row order differs from the API's.
            this.achievementService.ApiAchievementsLoaded += this.InvalidateCacheAndNotify;
            this.achievementService.PlayerAchievementsLoaded += this.InvalidateCacheAndNotify;
            this.bitAlignmentService.AlignmentLoaded += this.OnAlignmentLoaded;
            this.currentMapService.Changed += this.InvalidateCache;
            this.markerPackIndexService.Changed += this.InvalidateCacheAndNotify;
            this.hereExclusionService.Changed += this.InvalidateCache;

            this.achievementService.PlayerAchievementsLoaded += this.InvalidateAnywhereCache;
            this.hereExclusionService.Changed += this.InvalidateAnywhereCache;
        }

        private void InvalidateCache()
        {
            lock (this.cacheLock)
            {
                this.cachedTask = null;
            }
        }

        private void OnAlignmentLoaded(int achievementId) => this.InvalidateCacheAndNotify();

        private void InvalidateCacheAndNotify()
        {
            this.InvalidateCache();
            var ticket = Interlocked.Increment(ref this.notifySequence);

            _ = Task.Run(async () =>
            {
                await Task.Delay(InvalidateNotifyDelay);

                if (ticket != Volatile.Read(ref this.notifySequence))
                {
                    return;
                }

                try
                {
                    this.CandidatesInvalidated?.Invoke();
                }
                catch (Exception ex)
                {
                    this.logger.Warn(ex, "HereService: a CandidatesInvalidated subscriber threw.");
                }
            });
        }

        private void InvalidateAnywhereCache()
        {
            lock (this.anywhereCacheLock)
            {
                this.anywhereCachedTask = null;
            }
        }

        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            this.livingWorldMapCategories = await LoadLivingWorldMapCategoriesAsync(this.contentsManager, this.logger, cancellationToken);
            this.logger.Info($"HereService: loaded {LivingWorldMapCategoriesFileName} ({this.livingWorldMapCategories.Count} map entries)");
        }

        // The synchronous JsonSerializer.Deserialize(Stream, ...) overload deadlocked when JITted from this
        // background-thread call path (unlike AchievementService's existing DeserializeAsync usage, which is
        // fine) -- use DeserializeAsync here too rather than digging further into why.
        private static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> LoadLivingWorldMapCategoriesAsync(ContentsManager contentsManager, Logger logger, CancellationToken cancellationToken)
        {
            try
            {
                using (var stream = contentsManager.GetFileStream(LivingWorldMapCategoriesFileName))
                {
                    // Allows a `//` comment line in the JSON (see docs/PLAN.md's Phase 8 section) pointing
                    // at why some category names look odd (whitespace-normalized API names).
                    var options = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip };
                    var raw = await JsonSerializer.DeserializeAsync<Dictionary<string, List<string>>>(stream, options, cancellationToken);
                    return raw.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to load {LivingWorldMapCategoriesFileName}; Living World maps will fall back to name-matching (which doesn't work for them) until this is fixed.");
                return new Dictionary<string, IReadOnlyList<string>>();
            }
        }

        // Some category names contain a literal newline in the API (e.g. "The Dragon's Reach,\nPart 1"),
        // which we'd rather not put in the JSON. Collapse any run of whitespace to a single space before
        // comparing, on both sides, here and in the table lookup below.
        internal static string NormalizeCategoryName(string name)
            => name is null ? null : Regex.Replace(name, @"\s+", " ").Trim();

        // Validates the hand-maintained table against real category names once the API data is in.
        // Bad strings (typos, renamed categories) show up here instead of silently returning nothing in-game.
        private void ValidateLivingWorldMapCategories()
        {
            var knownCategoryNames = new HashSet<string>(
                this.achievementService.AchievementCategories.Select(c => NormalizeCategoryName(c.Name)),
                StringComparer.OrdinalIgnoreCase);

            foreach (var mapEntry in this.livingWorldMapCategories)
            {
                foreach (var categoryName in mapEntry.Value)
                {
                    if (!knownCategoryNames.Contains(NormalizeCategoryName(categoryName)))
                    {
                        this.logger.Warn($"{LivingWorldMapCategoriesFileName}: map '{mapEntry.Key}' references category '{categoryName}', which doesn't exist in AchievementCategories. Fix the string.");
                    }
                }
            }
        }

        public Task<HereResult> GetCandidatesAsync(int max, CancellationToken cancellationToken = default)
        {
            var mapId = this.currentMapService.MapId;
            var filter = this.guidanceFilter.Value;

            lock (this.cacheLock)
            {
                // Phase 56 (review item 17): a faulted or cancelled task is a miss, not a result to keep
                // handing out until the next map change.
                if (this.cachedTask != null && !this.cachedTask.IsFaulted && !this.cachedTask.IsCanceled && this.cachedMapId == mapId && this.cachedMax == max && this.cachedFilter == filter)
                {
                    this.logger.Debug($"HereService: cache hit for map id {mapId}.");
                    return this.cachedTask;
                }

                var task = this.ComputeCandidatesAsync(max, cancellationToken);
                this.cachedTask = task;
                this.cachedMapId = mapId;
                this.cachedMax = max;
                this.cachedFilter = filter;
                return task;
            }
        }

        // Every hidden or snoozed achievement, wherever it lives -- this is a management surface, not a
        // bounded suggestion list, so the product rule's small cap doesn't apply and the usual
        // PassesHereRules filtering deliberately doesn't either: something hidden while it was still
        // startable must stay un-hideable even once it's Done or its prerequisite lapsed.
        public async Task<IReadOnlyList<HereCandidate>> GetHiddenAsync(int max, CancellationToken cancellationToken = default)
        {
            var hiddenIds = new List<int>(this.hereExclusionService.HiddenAchievementIds);
            hiddenIds.AddRange(this.hereExclusionService.SnoozedUntilUtc.Keys);

            if (hiddenIds.Count == 0 || this.achievementService.AchievementCategories is null)
            {
                return Array.Empty<HereCandidate>();
            }

            var categoryByAchievementId = new Dictionary<int, AchievementCategory>();

            foreach (var category in this.achievementService.AchievementCategories)
            {
                foreach (var achievementId in category.Achievements)
                {
                    categoryByAchievementId[achievementId] = category;
                }
            }

            var apiAchievements = await this.bitAlignmentService.GetAchievementsAsync(hiddenIds, cancellationToken);
            var result = new List<HereCandidate>();

            foreach (var id in hiddenIds)
            {
                if (!this.achievementService.AchievementsById.TryGetValue(id, out var wikiAchievement) ||
                    !categoryByAchievementId.TryGetValue(id, out var category))
                {
                    continue;
                }

                apiAchievements.TryGetValue(id, out var apiAchievement);
                this.achievementService.PlayerAchievementsById.TryGetValue(id, out var playerAchievement);

                result.Add(new HereCandidate
                {
                    Achievement = wikiAchievement,
                    Category = category,
                    Current = playerAchievement?.Current ?? 0,
                    Max = playerAchievement?.Max ?? apiAchievement?.Tiers?.LastOrDefault()?.Count ?? 0,
                    AchievementPoints = apiAchievement?.Tiers?.Sum(t => t.Points) ?? 0,
                });
            }

            return result.OrderBy(c => c.Achievement.Name).Take(max).ToList();
        }

        public Task<IReadOnlyList<HereCandidate>> GetNearlyDoneAnywhereAsync(int max, CancellationToken cancellationToken = default)
        {
            lock (this.anywhereCacheLock)
            {
                if (this.anywhereCachedTask != null && this.anywhereCachedMax == max)
                {
                    return this.anywhereCachedTask;
                }

                var task = this.ComputeNearlyDoneAnywhereAsync(max, cancellationToken);
                this.anywhereCachedTask = task;
                this.anywhereCachedMax = max;
                return task;
            }
        }

        // Cheap because of what it *doesn't* look at: an achievement with no account record has zero
        // progress and so can't be "nearly done", which narrows the set from every table achievement to
        // the few hundred the account has actually touched. No guidance badge -- this list is
        // map-independent by definition, and per-map guidance for off-map achievements would be exactly
        // the "Not on this map" noise Phase 17 avoided.
        private async Task<IReadOnlyList<HereCandidate>> ComputeNearlyDoneAnywhereAsync(int max, CancellationToken cancellationToken)
        {
            if (this.achievementService.PlayerAchievementsById is null ||
                this.achievementService.AchievementCategories is null ||
                this.achievementService.AchievementGroups is null ||
                this.achievementService.Achievements is null)
            {
                return Array.Empty<HereCandidate>();
            }

            // Phase 56 (review item 10): HasFinishedAchievement, not Done -- a manually ticked last step is
            // finished for us the moment it's ticked (SessionSummaryService untracks on the same test), so
            // it must not be re-offered here until the API catches up.
            var startedIds = this.achievementService.PlayerAchievementsById
                .Where(kv => kv.Value.Current > 0 && !this.achievementService.HasFinishedAchievement(kv.Key))
                .Select(kv => kv.Key)
                .ToList();

            if (startedIds.Count == 0)
            {
                return Array.Empty<HereCandidate>();
            }

            var excludedCategoryIds = new HashSet<int>(
                this.achievementService.AchievementGroups
                    .Where(g => ExcludedGroupNames.Contains(g.Name))
                    .SelectMany(g => g.Categories));

            // The card needs a category for its icon, and the same group/category exclusions apply here as
            // on the map list -- an id with no allowed category is dropped rather than shown iconless.
            var categoryByAchievementId = new Dictionary<int, AchievementCategory>();

            foreach (var category in this.achievementService.AchievementCategories
                .Where(c => c.Name != ExcludedCategoryName && !excludedCategoryIds.Contains(c.Id)))
            {
                foreach (var achievementId in category.Achievements)
                {
                    categoryByAchievementId[achievementId] = category;
                }
            }

            var apiAchievements = await this.bitAlignmentService.GetAchievementsAsync(startedIds, cancellationToken);
            var nowUtc = DateTime.UtcNow;
            var candidates = new List<HereCandidate>();

            foreach (var id in startedIds)
            {
                if (!apiAchievements.TryGetValue(id, out var apiAchievement) ||
                    !categoryByAchievementId.TryGetValue(id, out var category) ||
                    this.hereExclusionService.IsExcluded(id, nowUtc))
                {
                    continue;
                }

                if (!this.PassesHereRules(apiAchievement, out var wikiAchievement, out var current, out var progressMax))
                {
                    continue;
                }

                candidates.Add(new HereCandidate
                {
                    Achievement = wikiAchievement,
                    Category = category,
                    Current = current,
                    Max = progressMax,
                    AchievementPoints = apiAchievement.Tiers?.Sum(t => t.Points) ?? 0,
                });
            }

            var ranked = candidates
                .OrderByDescending(c => (double)c.Current / c.Max)
                .ThenByDescending(c => c.AchievementPoints)
                .Take(max)
                .ToList();

            this.logger.Debug($"HereService: Anywhere list built from {startedIds.Count} started achievement(s) -> {candidates.Count} eligible, showing {ranked.Count}.");

            return ranked;
        }

        private async Task<HereResult> ComputeCandidatesAsync(int max, CancellationToken cancellationToken)
        {
            var mapId = this.currentMapService.MapId;
            var mapName = this.currentMapService.MapName;

            if (string.IsNullOrEmpty(mapName) ||
                this.achievementService.AchievementCategories is null ||
                this.achievementService.AchievementGroups is null ||
                this.achievementService.Achievements is null)
            {
                return new HereResult { Reason = HereResultReason.NotLoaded };
            }

            if (this.achievementService.PlayerAchievements is null)
            {
                return new HereResult
                {
                    Reason = this.gw2ApiManager.HasPermissions(new[] { TokenPermission.Account, TokenPermission.Progression })
                        ? HereResultReason.NotLoaded
                        : HereResultReason.NoPermission,
                };
            }

            var excludedCategoryIds = new HashSet<int>(
                this.achievementService.AchievementGroups
                    .Where(g => ExcludedGroupNames.Contains(g.Name))
                    .SelectMany(g => g.Categories));

            bool IsAllowed(AchievementCategory c) => c.Name != ExcludedCategoryName && !excludedCategoryIds.Contains(c.Id);

            List<AchievementCategory> matchedCategories;

            if (this.livingWorldMapCategories.TryGetValue(mapName, out var configuredCategoryNames))
            {
                var configuredSet = new HashSet<string>(
                    configuredCategoryNames.Select(NormalizeCategoryName),
                    StringComparer.OrdinalIgnoreCase);
                matchedCategories = this.achievementService.AchievementCategories
                    .Where(c => configuredSet.Contains(NormalizeCategoryName(c.Name)) && IsAllowed(c))
                    .ToList();
            }
            else
            {
                matchedCategories = this.achievementService.AchievementCategories
                    .Where(c => c.Name == mapName && IsAllowed(c))
                    .ToList();
            }

            var categoryByAchievementId = new Dictionary<int, AchievementCategory>();
            foreach (var category in matchedCategories)
            {
                foreach (var achievementId in category.Achievements)
                {
                    categoryByAchievementId[achievementId] = category;
                }
            }

            // Phase 15: union with the pack index's achievements for this map -- the multi-map fix and
            // the first real core-Tyria coverage. An id already reached via the category link keeps that
            // category (unaffected by the index); an index-only id needs its real category looked up
            // since HereView groups cards by Category.Id. An index id with no resolvable (allowed)
            // category can't carry one, so it's dropped rather than shown without a group header.
            var candidateIds = new HashSet<int>(categoryByAchievementId.Keys);
            var categoryLinkedIds = new HashSet<int>(categoryByAchievementId.Keys);
            var guidedIds = new HashSet<int>();

            if (this.markerPackIndexService.Ready)
            {
                Dictionary<int, AchievementCategory> categoryByAnyAchievementId = null;

                foreach (var id in this.markerPackIndexService.AchievementsOnMap(mapId))
                {
                    if (!categoryByAchievementId.ContainsKey(id))
                    {
                        if (categoryByAnyAchievementId is null)
                        {
                            categoryByAnyAchievementId = new Dictionary<int, AchievementCategory>();
                            foreach (var anyCategory in this.achievementService.AchievementCategories.Where(IsAllowed))
                            {
                                foreach (var achievementId in anyCategory.Achievements)
                                {
                                    categoryByAnyAchievementId[achievementId] = anyCategory;
                                }
                            }
                        }

                        if (!categoryByAnyAchievementId.TryGetValue(id, out var category))
                        {
                            continue;
                        }

                        categoryByAchievementId[id] = category;
                        _ = candidateIds.Add(id);
                    }

                    _ = guidedIds.Add(id);
                }
            }

            if (candidateIds.Count == 0)
            {
                return new HereResult { Reason = HereResultReason.NoCategoryForMap, IndexReady = this.markerPackIndexService.Ready };
            }

            var categorySupported = matchedCategories.Count > 0;

            var (apiAchievements, partial) = await this.FetchAchievementsAsync(candidateIds.ToList(), cancellationToken);

            var candidates = new List<HereCandidate>();

            foreach (var id in candidateIds)
            {
                if (!apiAchievements.TryGetValue(id, out var apiAchievement))
                {
                    // Fetch failed or the id no longer exists; skip rather than show incomplete data.
                    continue;
                }

                if (!this.PassesHereRules(apiAchievement, out var wikiAchievement, out var current, out var progressMax))
                {
                    continue;
                }

                var guidance = this.nearestObjectiveService.GetGuidance(id, mapId);

                // Phase 59: AchievementsOnMap lists every achievement with a marker on this map, done or
                // not. For one that's here only because of the index, that marker is the whole claim, so
                // once nothing on this map is left (no remaining tagged bit, wiki location or route) a
                // multi-map achievement stops showing up where you've already finished your part of it.
                // A category-linked one stays: the category says it belongs here whatever the index says.
                if (!categoryLinkedIds.Contains(id) && guidance.Tier == GuidanceTier.None)
                {
                    continue;
                }

                var hasBits = apiAchievement.Bits != null && apiAchievement.Bits.Count > 0;

                candidates.Add(new HereCandidate
                {
                    Achievement = wikiAchievement,
                    Category = categoryByAchievementId[id],
                    Current = current,
                    Max = progressMax,
                    AchievementPoints = apiAchievement.Tiers?.Sum(t => t.Points) ?? 0,
                    Guided = guidedIds.Contains(id),
                    Guidance = guidance,
                    // Phase 34's rule was "no bits", which the 2026-09-11 load-test showed is far too
                    // broad: on Seitung Province it swept up all 13 single-objective achievements in the
                    // category -- adventures, weeklies, an emote -- and 7 of those 13 had real marker-pack
                    // routes waiting for them. "No bits" only means nothing to *enumerate*; it says
                    // nothing about whether the thing has a place. The honest test is that nothing can
                    // place it at all: no bits to tick off *and* no guidance of any tier on this map.
                    Opportunistic = !hasBits && guidance.Tier == GuidanceTier.None,
                });
            }

            // Phase 31: hidden and snoozed candidates come out *before* the guidance filter and the cap,
            // for the same reason as Phase 27 -- the list refills rather than showing gaps. They're kept
            // (ranked the same way) so the "Show hidden" toggle has cards to render and the header can say
            // how many were withheld.
            var nowUtc = DateTime.UtcNow;
            var excludedCandidates = candidates.Where(c => this.hereExclusionService.IsExcluded(c.Achievement.Id, nowUtc)).ToList();

            if (excludedCandidates.Count > 0)
            {
                candidates = candidates.Where(c => !this.hereExclusionService.IsExcluded(c.Achievement.Id, nowUtc)).ToList();
            }

            // Phase 34: no bits means nothing to route to, so these shouldn't compete for bounded slots
            // and a "no guidance" badge on them would be misleading -- they don't want guidance. Out
            // before the cap (a slot refills) and before the guidance filter, which would otherwise wipe
            // them out for exactly the reason they're being separated.
            var opportunistic = candidates.Where(c => c.Opportunistic).ToList();

            if (opportunistic.Count > 0)
            {
                candidates = candidates.Where(c => !c.Opportunistic).ToList();
            }

            // Phase 27: the threshold is applied *before* the cap, so filtering doesn't just blank slots
            // -- the list refills with things that clear it.
            var minimumTier = (GuidanceTier)this.guidanceFilter.Value;
            var beforeFilter = candidates.Count;

            if (minimumTier > GuidanceTier.None)
            {
                candidates = candidates.Where(c => c.Guidance.Tier >= minimumTier).ToList();
            }

            // Guidance is a small bonus after the existing tiers, not a primary sort key -- an achievement
            // already close to done should still outrank a better-guided one that's barely started. Phase 26
            // made this the tier rather than a bool, so a tagged route edges out a trail-only one.
            // Phase 56 (review item 18): the strip needs the ranking past the cap so "Target these" doesn't
            // leave it at "· 0" while eligible untracked candidates remain.
            var rankedUncapped = Rank(candidates).ToList();
            var ranked = rankedUncapped.Take(max).ToList();

            return new HereResult
            {
                Reason = HereResultReason.Ok,
                Candidates = ranked,
                RankedUncapped = rankedUncapped,
                Partial = partial,
                CategorySupported = categorySupported,
                FilteredByGuidance = beforeFilter - candidates.Count,
                HiddenCount = excludedCandidates.Count,
                Opportunistic = opportunistic.OrderByDescending(c => (double)c.Current / c.Max).ToList(),
            };
        }

        private static IOrderedEnumerable<HereCandidate> Rank(IEnumerable<HereCandidate> candidates)
            => candidates
                .OrderByDescending(c => (double)c.Current / c.Max)
                .ThenByDescending(c => c.AchievementPoints)
                .ThenByDescending(c => c.Guidance.Tier);

        // Phase 33b: the "would I ever suggest this at all" rules, extracted so the map list and the
        // Anywhere list can't drift apart. Everything here is map-independent by construction.
        private bool PassesHereRules(Achievement apiAchievement, out AchievementTableEntry wikiAchievement, out int current, out int progressMax)
        {
            wikiAchievement = null;
            current = 0;
            progressMax = 0;

            var id = apiAchievement.Id;
            var flags = apiAchievement.Flags.Select(f => f.Value).ToList();

            // IgnoreNearlyComplete is excluded outright rather than demoted: these are bounded lists, not
            // browse surfaces -- Phase 25's demotion rule is for the category view.
            if (flags.Contains(AchievementFlag.IgnoreNearlyComplete) ||
                flags.Contains(AchievementFlag.CategoryDisplay) ||
                flags.Contains(AchievementFlag.Repeatable) ||
                flags.Contains(AchievementFlag.Pvp))
            {
                return false;
            }

            this.achievementService.PlayerAchievementsById.TryGetValue(id, out var playerAchievement);

            // Finished means nothing's left to do -- by the API's Done or by the last step ticked manually
            // (Phase 56, review item 10; the same test SessionSummaryService untracks on).
            if (this.achievementService.HasFinishedAchievement(id))
            {
                return false;
            }

            // Phase 56 (review item 9): Gw2Sharp's Unlocked is bool? and null means "not lockable", so the
            // old `Unlocked != true` dropped every Hidden achievement that doesn't use RequiresUnlock --
            // eight in-progress ones on the test account. Excluded only with no account record at all, or
            // an explicit Unlocked == false.
            if (flags.Contains(AchievementFlag.Hidden) && (playerAchievement is null || playerAchievement.Unlocked == false))
            {
                return false;
            }

            // Phase 25 -- two "don't suggest what you can't actually start" rules, both measured on
            // 2026-09-09 over the 1,112 table achievements: 244 are RequiresUnlock and 137 carry
            // prerequisites. Bounded lists make a wasted slot expensive, and being sent to a map for
            // something that turns out to be locked is worse than not being told about it.
            if (flags.Contains(AchievementFlag.RequiresUnlock) && playerAchievement?.Unlocked != true)
            {
                return false;
            }

            if (this.HasUnmetPrerequisite(apiAchievement))
            {
                return false;
            }

            if (!this.achievementService.AchievementsById.TryGetValue(id, out wikiAchievement))
            {
                return false;
            }

            // No account record means 0 progress -- an achievement the player could start here. Max still
            // needs to come from the API's tier data since there's no account record for it.
            current = playerAchievement?.Current ?? 0;
            progressMax = playerAchievement?.Max ?? apiAchievement.Tiers?.LastOrDefault()?.Count ?? 0;

            return progressMax > 0;
        }

        // A prerequisite the account hasn't finished means this achievement can't be worked on yet. An id
        // with no account record at all has never been started, so it counts as unmet -- the API only
        // returns a record once there's progress.
        private bool HasUnmetPrerequisite(Achievement apiAchievement)
        {
            if (apiAchievement.Prerequisites is null)
            {
                return false;
            }

            foreach (var prerequisiteId in apiAchievement.Prerequisites)
            {
                if (!this.achievementService.PlayerAchievementsById.TryGetValue(prerequisiteId, out var prerequisite) || !prerequisite.Done)
                {
                    return true;
                }
            }

            return false;
        }

        // Phase 23: the id->Achievement fetch/cache is shared with BitAlignmentService now (it and
        // SessionSummaryService each used to fetch this independently). BitAlignmentService already logs
        // and continues on a failed batch; a result short of what was asked for is this pass' "partial".
        private async Task<(IReadOnlyDictionary<int, Achievement> Results, bool Partial)> FetchAchievementsAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken)
        {
            var result = await this.bitAlignmentService.GetAchievementsAsync(ids, cancellationToken);
            return (result, result.Count < ids.Count);
        }
    }
}
