using Quarry.Models.Markers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Quarry.Interfaces
{
    public interface IMarkerPackIndexService
    {
        bool Ready { get; }

        // Raised on a background thread (a pack file changed and the index rebuilt) -- consumers marshal
        // before touching controls, same as BitAlignmentService.AlignmentLoaded.
        event Action Changed;

        Task LoadAsync(CancellationToken cancellationToken = default);

        bool TryGet(int achievementId, out AchievementRoute route);

        IReadOnlyCollection<int> AchievementsOnMap(int mapId);
    }
}
