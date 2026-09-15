using Quarry.WikiData.Achievement;
using Quarry.UserInterface.Views;
using Gw2Sharp.WebApi.V2.Models;
using System.Collections.Generic;

namespace Quarry.Interfaces
{
    public interface IAchievementItemOverviewFactory
    {
        AchievementItemOverview Create(IEnumerable<(AchievementCategory, AchievementTableEntry)> achievements, string title);
    }
}
