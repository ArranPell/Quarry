using Quarry.UserInterface.Windows;
using Quarry.WikiData.Achievement;
using System;

namespace Quarry.Interfaces
{
    // Phase 45. Replaces AchievementDetailsWindow/SubPageInformationWindow as the default detail
    // surface: one window that swaps its content on the next click instead of one window per thing
    // opened. Reached from the Target List's row click (ShowAchievement) and Module.Update / Unload.
    public interface IInspectorWindowManager : IDisposable
    {
        // Opens (or replaces the content of) the default Inspector on the achievement view. A pinned
        // Inspector is untouched -- this always targets the one live "default" instance.
        void ShowAchievement(AchievementTableEntry achievement);

        // Called by an InspectorWindow's own pin toggle: it stops being the default (so the next Show*
        // opens a fresh one) and keeps living until closed on its own.
        void NotifyPinned(InspectorWindow window);

        // ArranPell, 2026-09-13: the Target List already hides itself while the world map is open (Module.cs's
        // own Update loop) -- same rule extended to every live Inspector (default and pinned). Called
        // from Module.Update().
        void Update();
    }
}
