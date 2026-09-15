using Quarry.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Quarry.Interfaces
{
    // Phase 48. Replaces AchievementService.Subpages -- the embedded derived_subpages.json instead of
    // the 70 MB subPages.json download, keyed by link (fixes InspectorWindow.FindSubPage's linear scan
    // as a side effect, per DATA-INDEPENDENCE-REVIEW-2026-09-13.md §4.6).
    public interface IWikiSubpageDataService
    {
        IReadOnlyDictionary<string, DerivedSubpage> ByLink { get; }

        Task LoadAsync(CancellationToken cancellationToken = default);
    }
}
