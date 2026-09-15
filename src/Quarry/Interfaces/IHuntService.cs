using System;
using System.Collections.Generic;

namespace Quarry.Interfaces
{
    /// <summary>
    /// Phase 16: drives Pathing's category state from the tracked set when hunt mode is on -- tracking a
    /// guided achievement turns its routes on, untracking (or completing) turns off only what we turned on.
    /// </summary>
    public interface IHuntService : IDisposable
    {
        // Persisted mirror of the namespaces we've flipped active per tracked achievement id -- what
        // PersistenceService.Save() writes to Storage.HuntEnabledNamespaces.
        IReadOnlyDictionary<int, List<string>> HuntEnabledNamespaces { get; }

        // True when hunt mode is on and Pathing is reachable -- gates the Here card's "Guided" label
        // turning into a peek button.
        bool CanPeek { get; }

        // One-off look at achievementId's routes: enables its namespaces under an in-memory (not
        // persisted) key, reverted on the next map change or when the achievement gets tracked. No-op if
        // CanPeek is false or the achievement has no route.
        void Peek(int achievementId);

        // Reverts achievementId's hunt namespaces (tracked or peek) regardless of current hunt-mode state
        // -- called on completion, since a finished achievement has nothing left to hunt either way.
        void RevertForCompletion(int achievementId);

        // Reverts every namespace we've ever recorded (tracked + peek) -- called from Module.Unload()
        // only when HuntRevertOnUnload is on.
        void RevertAllForUnload();
    }
}
