using Quarry.Models;
using Quarry.UserInterface.Controls;
using Quarry.WikiData.Achievement;

namespace Quarry.Interfaces
{
    // Phase 40. Replaces IAchievementListItemFactory -- deleted in Phase 44 along with
    // AchievementListItem/AchievementButton, the View/DetailsButton path this supersedes.
    public interface IAchievementCardFactory
    {
        AchievementCard Create(AchievementTableEntry achievement, string icon, GuidanceInfo guidance = null, IHereCardActions hereCardActions = null, int? rank = null);
    }
}
