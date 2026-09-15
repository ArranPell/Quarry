using Quarry.Models;
using System;

namespace Quarry.Interfaces
{
    public interface ISessionSummaryService
    {
        SessionSummary Summary { get; }

        event Action Changed;

        // Fires once per achievement id the moment it's found Done -- a freshly-finished achievement, or
        // one that was already Done and tracked at session start (swept on the first real load so a stale
        // tracked set cleans itself). Always raised on a background thread -- marshal before touching
        // controls or AchievementTrackerService.
        event Action<int> AchievementCompleted;

        /// <summary>Formats <see cref="Summary"/> as e.g. "This session: +15 AP · 2 completed · 7 steps", omitting zero parts. Null when everything is zero.</summary>
        string GetSummaryLine();
    }
}
