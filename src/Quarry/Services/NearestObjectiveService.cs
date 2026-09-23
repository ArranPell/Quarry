using Quarry.Interfaces;
using Quarry.Models;
using Quarry.Models.Markers;
using Quarry.WikiData.Achievement;
using Blish_HUD;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Quarry.Services
{
    // Phase 17: "what's left, nearest first, and the waypoint to take" for one tracked achievement on the
    // current map. No cache -- one route's objectives is a few hundred points at most, cheap to walk.
    public class NearestObjectiveService : INearestObjectiveService
    {
        // How much closer a waypoint has to land you before it's worth suggesting at all.
        private const float WaypointWorthTakingMarginMetres = 150f;

        private readonly IMarkerPackIndexService markerPackIndexService;
        private readonly IAchievementService achievementService;
        private readonly IBitAlignmentService bitAlignmentService;
        private readonly IWikiLocationService wikiLocationService;
        private readonly ICurrentMapService currentMapService;
        private readonly Logger logger;

        public NearestObjectiveService(IMarkerPackIndexService markerPackIndexService, IAchievementService achievementService, IBitAlignmentService bitAlignmentService, IWikiLocationService wikiLocationService, ICurrentMapService currentMapService, Logger logger)
        {
            this.currentMapService = currentMapService;
            this.logger = logger;
            this.markerPackIndexService = markerPackIndexService;
            this.achievementService = achievementService;
            this.bitAlignmentService = bitAlignmentService;
            this.wikiLocationService = wikiLocationService;
        }

        public IReadOnlyList<RemainingObjective> GetRemaining(int achievementId, int mapId, Vector3 player)
        {
            // No pack route is no longer the end of the story -- Phase 28's wiki coordinates can cover an
            // achievement no pack tags, which is the whole point of them.
            _ = this.markerPackIndexService.TryGet(achievementId, out var route);

            var nearestByBit = new Dictionary<int, RemainingObjective>();
            RemainingObjective nearestUntagged = null;

            foreach (var objective in this.RemainingOnMap(route, achievementId, mapId))
            {
                var distance = ObjectiveGeometry.DistanceMetres(player, objective);

                // Untagged (no achievementbit) objectives pool into one entry named after the achievement
                // itself, rather than one per marker.
                if (objective.Bit < 0)
                {
                    if (nearestUntagged is null || distance < nearestUntagged.DistanceMetres)
                    {
                        nearestUntagged = new RemainingObjective
                        {
                            Bit = -1,
                            Row = -1,
                            Name = this.GetAchievementName(achievementId),
                            DistanceMetres = distance,
                            Namespace = objective.Namespace,
                            IsTrail = objective.IsTrail,
                            Waypoint = objective.Waypoint,
                            GroundDistanceOnly = objective.HeightUnknown,
                            AreaHint = objective.SectorName,
                        };
                    }

                    continue;
                }

                if (nearestByBit.TryGetValue(objective.Bit, out var existing) && existing.DistanceMetres <= distance)
                {
                    continue;
                }

                var row = this.bitAlignmentService.MapBitToRow(achievementId, objective.Bit);
                var rowDisplayName = this.GetRowDisplayName(achievementId, row);

                // The achievement panel lists these steps in the same row order -- prefixing the number
                // (1-based, matching how the panel reads) is what actually lets you match "the step I
                // just did" against the tooltip's list of similar-sounding remaining steps.
                var name = rowDisplayName != null ? $"{row + 1}. {rowDisplayName}" : $"objective #{objective.Bit}";

                nearestByBit[objective.Bit] = new RemainingObjective
                {
                    Bit = objective.Bit,
                    Row = row,
                    Name = name,
                    DistanceMetres = distance,
                    Namespace = objective.Namespace,
                    IsTrail = objective.IsTrail,
                    Waypoint = objective.Waypoint,
                    GroundDistanceOnly = objective.HeightUnknown,
                    AreaHint = objective.SectorName,
                };
            }

            var result = nearestByBit.Values.ToList();
            if (nearestUntagged != null)
            {
                result.Add(nearestUntagged);
            }

            return result.OrderBy(r => r.DistanceMetres).ToList();
        }

        // Phase 26. Lives here rather than in a service of its own because it answers the same question
        // from the same data as GetRemaining -- "what's left on this map, and how well do we know it" --
        // and splitting them would put the tagged/untagged rule back in two places.
        public GuidanceInfo GetGuidance(int achievementId, int mapId)
        {
            var remainingTagged = 0;
            var hasRoute = false;
            var hasTrail = false;

            if (this.markerPackIndexService.TryGet(achievementId, out var route))
            {
                foreach (var objective in route.Objectives)
                {
                    if (objective.MapId != mapId)
                    {
                        continue;
                    }

                    if (objective.Bit < 0)
                    {
                        hasRoute = true;
                        hasTrail |= objective.IsTrail;
                        continue;
                    }

                    if (!this.achievementService.HasFinishedBitIndex(achievementId, objective.Bit))
                    {
                        remainingTagged++;
                    }
                }
            }

            // Tagged wins whenever anything tagged is actually left; once it's all done, an untagged
            // route on the same map is still worth saying so, because there may be more to walk.
            if (remainingTagged > 0)
            {
                return new GuidanceInfo { Tier = GuidanceTier.Tagged, RemainingTagged = remainingTagged, HasRoute = hasRoute, HasTrail = hasTrail };
            }

            // Phase 28: a wiki coordinate outranks an untagged route, because it knows which step it
            // belongs to and therefore goes away when that step is done. Phase 29 extends this to a
            // sector-centroid match -- GetRemainingOnMap covers both, so this check is unchanged.
            if (this.wikiLocationService.GetRemainingOnMap(achievementId, mapId).Count > 0)
            {
                return new GuidanceInfo { Tier = GuidanceTier.Coordinate, HasRoute = hasRoute, HasTrail = hasTrail };
            }

            if (hasRoute)
            {
                return new GuidanceInfo { Tier = GuidanceTier.Route, HasRoute = true, HasTrail = hasTrail };
            }

            // Phase 29: nothing resolved to a coordinate, but a remaining row still names this map's own
            // zone -- somewhere to go, no distance to count down. Checked last: Route (an actual drawn
            // trail or markers) outranks a bare area name, per GuidanceTier's ordering.
            return this.wikiLocationService.HasAreaOnlyRemaining(achievementId, mapId)
                ? new GuidanceInfo { Tier = GuidanceTier.Area }
                : GuidanceInfo.None;
        }

        // Phase 28 gave objectives a second source but left the Track window still asking the marker-pack
        // index directly, so a wiki-only achievement (Master of Ceremonies on Grothmar Valley) got the
        // guidance badge but no "Next:" line at all -- the panel simply never built one.
        public bool HasAnyObjectives(int achievementId)
            => this.markerPackIndexService.TryGet(achievementId, out _) || this.wikiLocationService.HasAnyLocations(achievementId);

        public IReadOnlyList<string> Waypoints(int achievementId)
            => this.markerPackIndexService.TryGet(achievementId, out var route) ? (IReadOnlyList<string>)route.Waypoints.ToList() : System.Array.Empty<string>();

        // "Which waypoint actually helps?" -- NOT "which waypoint is nearest the next objective", which is
        // what this did first and what made it look erratic in the load-test. Anchoring on one objective
        // means that in a cluster of remaining work the anchor flips between neighbours as you move, and
        // each neighbour has a different closest waypoint, so the answer jitters between waypoints that
        // are all individually correct. Worse, it happily told you to port somewhere when you were already
        // standing next to the thing.
        //
        // So: score every waypoint by how close it puts you to the NEAREST REMAINING objective, and only
        // offer one if porting there actually beats walking from where you stand. Nothing to gain is a
        // real answer, and saying so is more useful than naming a waypoint you shouldn't take.
        public WaypointSuggestion NearestWaypoint(int achievementId, int mapId, Vector3 player)
        {
            _ = this.markerPackIndexService.TryGet(achievementId, out var route);

            var remaining = this.RemainingOnMap(route, achievementId, mapId);

            if (remaining.Count == 0)
            {
                return null;
            }

            var onFoot = remaining.Min(o => ObjectiveGeometry.DistanceMetres(player, o));

            if (this.currentMapService.TryGetWaypoints(mapId, out var mapWaypoints))
            {
                MapWaypoint best = null;
                var bestDistance = float.MaxValue;

                foreach (var waypoint in mapWaypoints)
                {
                    var from = new Vector3(waypoint.World.X, waypoint.World.Y, 0f);
                    var distance = remaining.Min(o => GroundDistance(from, o));

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = waypoint;
                    }
                }

                if (best != null)
                {
                    return new WaypointSuggestion
                    {
                        Code = best.ChatLink,
                        Name = best.Name,
                        MetresFromObjective = bestDistance,
                        // A waypoint is worth taking only if it lands you meaningfully closer than you
                        // already are. The margin stops it flickering on and off as you walk.
                        WorthTaking = bestDistance + WaypointWorthTakingMarginMetres < onFoot,
                        MetresOnFoot = onFoot,
                    };
                }
            }

            // No API answer for this map yet -- fall back to the pack's own annotations. They name a
            // waypoint per marker rather than positioning one, so there's no distance to report.
            if (route is null)
            {
                return null;
            }

            string nearestCode = null;
            var nearestPackDistance = float.MaxValue;
            var anchor = this.NearestRemainingPosition(route, achievementId, mapId, player) ?? player;

            foreach (var objective in route.Objectives)
            {
                if (objective.MapId != mapId || string.IsNullOrEmpty(objective.Waypoint))
                {
                    continue;
                }

                var distance = ObjectiveGeometry.DistanceMetres(anchor, objective);
                if (distance < nearestPackDistance)
                {
                    nearestPackDistance = distance;
                    nearestCode = objective.Waypoint;
                }
            }

            return nearestCode is null ? null : new WaypointSuggestion { Code = nearestCode, WorthTaking = true };
        }

        // Waypoints and wiki coordinates have no height, so every comparison against one is on the
        // ground plane.
        private static float GroundDistance(Vector3 from, AchievementObjective objective)
        {
            var world = ObjectiveGeometry.ToWorld(objective);
            var dx = from.X - world.X;
            var dy = from.Y - world.Y;
            return (float)System.Math.Sqrt((dx * dx) + (dy * dy));
        }

        // The objectives on this map that still count as "left to do", under one rule shared by the
        // Next line, the waypoint anchor and the guidance badge so they can never disagree.
        //
        // Precedence, best evidence first (DECISIONS 2026-09-09, extended in Phase 28):
        //   1. bit-tagged pack markers  -- a real position AND completion data
        //   2. wiki coordinates         -- completion data (via the row->bit map) but no in-world icon
        //   3. untagged pack markers    -- a position, but nothing ever disappears as you finish
        //
        // Each level speaks only when the ones above it have nothing left to say. An untagged marker has
        // no bit to check against, so it can never disappear once done and will happily claim to be the
        // "nearest" thing left in an area you have already cleared -- found in the load-test, where an
        // untagged pack's trail sent the player back over finished ground while tagged objectives showed
        // what remained elsewhere on the map.
        private List<AchievementObjective> RemainingOnMap(AchievementRoute route, int achievementId, int mapId)
        {
            // A finished achievement has nothing left anywhere, and must never fall through to the
            // untagged fallback below -- doing so made a completed collection keep a live "Next: … 7 m"
            // line off 738 breadcrumb markers, so the panel looked like work remained (load-test
            // 2026-09-10).
            if (this.achievementService.HasFinishedAchievement(achievementId))
            {
                return new List<AchievementObjective>();
            }

            var tagged = new List<AchievementObjective>();
            var untagged = new List<AchievementObjective>();

            if (route != null)
            {
                foreach (var objective in route.Objectives)
                {
                    if (objective.MapId != mapId)
                    {
                        continue;
                    }

                    if (objective.Bit < 0)
                    {
                        untagged.Add(objective);
                    }
                    else if (!this.achievementService.HasFinishedBitIndex(achievementId, objective.Bit))
                    {
                        tagged.Add(objective);
                    }
                }
            }

            var remaining = new List<AchievementObjective>(tagged);

            // A wiki coordinate for a bit a pack already tags adds nothing but a second dot on the same
            // step, so the pack wins per-bit rather than per-map.
            var taggedBits = new HashSet<int>(tagged.Select(o => o.Bit));

            foreach (var wikiObjective in this.wikiLocationService.GetRemainingOnMap(achievementId, mapId))
            {
                if (!taggedBits.Contains(wikiObjective.Bit))
                {
                    remaining.Add(wikiObjective);
                }
            }

            // Last resort only. Note this is per *remaining* work, not per map: once everything tagged
            // here is finished, an untagged route on the same map is worth showing again, because it may
            // well cover steps the pack never tagged.
            if (remaining.Count == 0)
            {
                remaining.AddRange(untagged);
            }

            return remaining;
        }

        private Vector3? NearestRemainingPosition(AchievementRoute route, int achievementId, int mapId, Vector3 player)
        {
            Vector3? nearest = null;
            var nearestDistance = float.MaxValue;

            foreach (var objective in this.RemainingOnMap(route, achievementId, mapId))
            {
                var distance = ObjectiveGeometry.DistanceMetres(player, objective);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = ObjectiveGeometry.ToWorld(objective);
                }
            }

            return nearest;
        }

        private string GetAchievementName(int achievementId)
            => this.achievementService.AchievementsById.TryGetValue(achievementId, out var achievement) ? achievement.Name : $"#{achievementId}";

        // Same Collection/Objective description entry lists AchievementProgress.GetRemainingCollectionText
        // walks for the row-order case -- row < 0 (unresolved bit) falls back to the "objective #<bit>" caller.
        private string GetRowDisplayName(int achievementId, int row)
        {
            if (row < 0 || !this.achievementService.AchievementsById.TryGetValue(achievementId, out var achievement))
            {
                return null;
            }

            switch (achievement.Description)
            {
                case CollectionDescription collection when row < collection.EntryList.Count:
                    return collection.EntryList[row].DisplayName;
                case ObjectivesDescription objectives when row < objectives.EntryList.Count:
                    return objectives.EntryList[row].DisplayName;
                default:
                    return null;
            }
        }
    }
}
