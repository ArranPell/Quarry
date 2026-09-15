using System;
using System.Collections.Generic;

namespace Quarry.Models
{
    public class SessionSummary
    {
        public int ApGained { get; set; }

        public IReadOnlyList<string> CompletedAchievementNames { get; set; } = Array.Empty<string>();

        public int BitsTicked { get; set; }
    }
}
