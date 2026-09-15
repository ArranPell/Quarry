using Quarry.WikiData.Achievement;
using Gw2Sharp.WebApi.V2.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Quarry.Interfaces
{
    public interface IAchievementService : IDisposable
    {
        IReadOnlyList<AchievementTableEntry> Achievements { get; }

        // Lazy -- achievement_tables.json (20.7 MB) is only parsed the first time this is awaited.
        // See AchievementService.LoadAsync (Phase 46).
        Task<IReadOnlyList<CollectionAchievementTable>> GetAchievementDetailsAsync();

        // The cached achievement_tables.json's own last-write time, so a UI element sourcing text from
        // it (the Inspector's Notes column, Phase 47) can say how current the guidance is.
        DateTime AchievementTablesSnapshotDate { get; }

        IEnumerable<AchievementGroup> AchievementGroups { get; }

        IEnumerable<AchievementCategory> AchievementCategories { get; }

        IEnumerable<AccountAchievement> PlayerAchievements { get; }

        IReadOnlyDictionary<int, AccountAchievement> PlayerAchievementsById { get; }

        IReadOnlyDictionary<int, AchievementTableEntry> AchievementsById { get; }

        event Action PlayerAchievementsLoaded;

        event Action ApiAchievementsLoaded;

        bool HasFinishedAchievement(int achievementId);

        bool HasFinishedAchievementBit(int achievementId, int positionIndex);

        // Same check as HasFinishedAchievementBit, but bit is already an API bit index (a marker pack's
        // achievementBit, not a wiki row) -- no MapRowToBit translation. Always false for a negative bit.
        bool HasFinishedBitIndex(int achievementId, int bit);

        Task LoadPlayerAchievements(bool forceRefresh = false, CancellationToken cancellationToken = default);
        void ToggleManualCompleteStatus(int achievementId, int bit);
    }
}
