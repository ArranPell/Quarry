using System.Collections.Generic;
using System.Diagnostics;

namespace Quarry.WikiData.Achievement
{
    [DebuggerDisplay("{Tiers[0].DisplayName}")]
    public class MultiTierReward : Reward
    {
        public List<TierReward> Tiers { get; set; } = new List<TierReward>();

        public class TierReward : ItemReward
        {
            public int Tier { get; set; } = default;
        }
    }
}
