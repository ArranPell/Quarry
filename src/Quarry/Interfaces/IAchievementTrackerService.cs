using System;
using System.Collections.Generic;

namespace Quarry.Interfaces
{
    public interface IAchievementTrackerService
    {
        IReadOnlyList<int> ActiveAchievements { get; }

        // Phase 32: how many further TrackAchievement calls will succeed -- int.MaxValue when the
        // 15-cap setting is off.
        int FreeSlots { get; }

        event Action<int> AchievementTracked;

        event Action<int> AchievementUntracked;

        void RemoveAchievement(int achievement);

        bool IsBeingTracked(int achievement);
        
        bool TrackAchievement(int achievement);
    }
}
