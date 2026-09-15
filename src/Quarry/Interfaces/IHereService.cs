using Quarry.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Quarry.Interfaces
{
    public interface IHereService
    {
        // Phase 56 (review item 11): raised, coalesced and on a background thread, when the candidate
        // results may have changed for a reason the views don't already watch themselves -- a
        // player-achievements poll or manual tick, the API groups/categories arriving, a row->bit
        // alignment landing, the marker-pack index finishing. Map changes and hide/snooze are not in
        // here: every view subscribes to those directly. Marshal before touching controls.
        event Action CandidatesInvalidated;

        Task LoadAsync(CancellationToken cancellationToken = default);

        Task<HereResult> GetCandidatesAsync(int max, CancellationToken cancellationToken = default);

        // Phase 33b: closest-to-done across the whole account, not map-bound. Same exclusions and
        // hide rules as the map list, same cap, no guidance badge.
        Task<IReadOnlyList<HereCandidate>> GetNearlyDoneAnywhereAsync(int max, CancellationToken cancellationToken = default);

        // Everything currently hidden or snoozed, account-wide and unfiltered -- what the Here
        // header's "Show hidden" mode lists so anything hidden can always be reached again.
        Task<IReadOnlyList<HereCandidate>> GetHiddenAsync(int max, CancellationToken cancellationToken = default);
    }
}
