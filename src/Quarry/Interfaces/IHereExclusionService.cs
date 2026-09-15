using System;
using System.Collections.Generic;

namespace Quarry.Interfaces
{
    /// <summary>
    /// Phase 31: the "don't offer me this" state behind the Here list. Two kinds, both reversible:
    /// hidden (until un-hidden) and snoozed (until the next daily reset at 00:00 UTC, the clock the
    /// game uses).
    ///
    /// The service owns the state rather than <see cref="Models.Persistence.Storage"/> alone, because
    /// PersistenceService.Save() builds a fresh Storage from the services on every save -- a collection
    /// that only lived in Storage would be dropped on the first autosave.
    /// </summary>
    public interface IHereExclusionService
    {
        // Raised after any Hide/Snooze/Unhide so HereService can drop its cached result.
        event Action Changed;

        IReadOnlyCollection<int> HiddenAchievementIds { get; }

        // Achievement id -> the UTC instant its snooze lapses. Expired entries are pruned on read.
        IReadOnlyDictionary<int, DateTime> SnoozedUntilUtc { get; }

        // Hidden + snoozed, for the Here header's "Show hidden (N)" label.
        int TotalExcludedCount { get; }

        void Hide(int achievementId);

        // Hides until the next 00:00 UTC. No custom durations: one clock, one rule.
        void Snooze(int achievementId);

        // Clears both kinds for this id.
        void Unhide(int achievementId);

        bool IsExcluded(int achievementId, DateTime nowUtc);

        // True when the exclusion is a (still-live) snooze rather than a permanent hide -- the card's
        // tooltip says which.
        bool IsSnoozed(int achievementId, DateTime nowUtc);

        void Load(IPersistenceService persistenceService);
    }
}
