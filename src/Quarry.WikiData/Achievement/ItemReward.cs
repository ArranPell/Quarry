using System.Diagnostics;

namespace Quarry.WikiData.Achievement
{
    [DebuggerDisplay("{DisplayName}")]
    public class ItemReward : Reward
    {
        public string ImageUrl { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string ItemUrl { get; set; } = string.Empty;

        public int Id { get; set; } = default;
    }
}
