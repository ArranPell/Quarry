using Quarry.Interfaces;
using Quarry.WikiData.Achievement;
using Quarry.UserInterface.Views;
using Gw2Sharp.WebApi.V2.Models;
using System.Collections.Generic;

namespace Quarry.Services.Factories
{
    public class AchievementItemOverviewFactory : IAchievementItemOverviewFactory
    {
        private readonly IAchievementCardFactory achievementCardFactory;
        private readonly IAchievementService achievementService;
        private readonly IBitAlignmentService bitAlignmentService;

        public AchievementItemOverviewFactory(IAchievementCardFactory achievementCardFactory, IAchievementService achievementService, IBitAlignmentService bitAlignmentService)
        {
            this.achievementCardFactory = achievementCardFactory;
            this.achievementService = achievementService;
            this.bitAlignmentService = bitAlignmentService;
        }

        public AchievementItemOverview Create(IEnumerable<(AchievementCategory, AchievementTableEntry)> achievements, string title)
            => new AchievementItemOverview(achievements, title, this.achievementService, this.achievementCardFactory, this.bitAlignmentService);
    }
}
