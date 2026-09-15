using Blish_HUD;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Quarry.Interfaces;
using Quarry.Models;
using Quarry.Services.Factories;
using System.Threading;
using System.Threading.Tasks;

namespace Quarry.Services
{
    public class DependencyInjectionContainer
    {
        private readonly Gw2ApiManager gw2ApiManager;
        private readonly ContentsManager contentsManager;
        private readonly ContentService contentService;
        private readonly DirectoriesManager directoriesManager;
        private readonly Logger logger;
        private readonly GraphicsService graphicsService;

        public IAchievementTrackerService AchievementTrackerService { get; set; }

        public IAchievementCardFactory AchievementCardFactory { get; set; }

        public IAchievementItemOverviewFactory AchievementItemOverviewFactory { get; set; }

        public IAchievementService AchievementService { get; set; }

        public ITextureService TextureService { get; set; }

        public IPersistenceService PersistenceService { get; private set; }
        
        // Phase 48: replaces SubPageInformationWindowManager -- the embedded derived_subpages.json
        // instead of the 70 MB subPages.json download.
        public IWikiSubpageDataService WikiSubpageDataService { get; set; }

        // Phase 45. Reached from the Target List's row click (ShowAchievement) and from Module.Update /
        // Unload.
        public IInspectorWindowManager InspectorWindowManager { get; set; }

        public IFormattedLabelHtmlService FormattedLabelHtmlService { get; set; }

        public IExternalImageService ExternalImageService { get; set; }

        public ICurrentMapService CurrentMapService { get; set; }

        public IHereService HereService { get; set; }

        public IHereExclusionService HereExclusionService { get; private set; }

        // Phase 33a: the Here list's cap, read by HereView and Module's toast path. Default 10 --
        // the product rule's cap stays the default.
        public SettingEntry<int> HereCap { get; private set; }

        public IPathingBridge PathingBridge { get; private set; }

        public IHuntService HuntService { get; private set; }

        public ISessionSummaryService SessionSummaryService { get; set; }

        public IBitAlignmentService BitAlignmentService { get; private set; }

        public IMarkerPackIndexService MarkerPackIndexService { get; private set; }

        public INearestObjectiveService NearestObjectiveService { get; private set; }

        public IWikiLocationService WikiLocationService { get; private set; }

        public DependencyInjectionContainer(Gw2ApiManager gw2ApiManager, ContentsManager contentsManager, ContentService contentService, DirectoriesManager directoriesManager, Logger logger, GraphicsService graphicsService)
        {
            this.gw2ApiManager = gw2ApiManager;
            this.contentsManager = contentsManager;
            this.contentService = contentService;
            this.directoriesManager = directoriesManager;
            this.logger = logger;
            this.graphicsService = graphicsService;
        }

        public async Task InitializeAsync(SettingEntry<bool> autoSave, SettingEntry<bool> limitAchievement, SettingEntry<bool> huntMode, SettingEntry<HereGuidanceFilter> hereGuidanceFilter, SettingEntry<int> hereCap, CancellationToken cancellationToken = default)
        {
            this.ExternalImageService = new ExternalImageService(this.graphicsService, this.logger);
            this.TextureService = new TextureService(this.contentService, this.contentsManager);
            this.CurrentMapService = new CurrentMapService(this.gw2ApiManager, this.logger);

            var achievementService = new AchievementService(this.contentsManager, this.gw2ApiManager, this.logger, this.directoriesManager, () => this.PersistenceService, this.TextureService, () => this.BitAlignmentService);
            this.AchievementService = achievementService;

            // Constructed right after AchievementService, since it needs the wiki data (AchievementsById)
            // -- and threaded back into AchievementService via the lazy Func above (same shape as
            // PersistenceService's own forward reference) so HasFinishedAchievementBit/
            // ToggleManualCompleteStatus can reach it without a real constructor cycle.
            this.BitAlignmentService = new BitAlignmentService(this.AchievementService, this.gw2ApiManager, this.logger);
            this.MarkerPackIndexService = new MarkerPackIndexService(this.directoriesManager, this.logger);
            this.WikiSubpageDataService = new WikiSubpageDataService(this.contentsManager, this.logger);
            this.WikiLocationService = new WikiLocationService(this.AchievementService, this.WikiSubpageDataService, this.BitAlignmentService, this.CurrentMapService, this.logger);
            this.NearestObjectiveService = new NearestObjectiveService(this.MarkerPackIndexService, this.AchievementService, this.BitAlignmentService, this.WikiLocationService, this.CurrentMapService, this.logger);

            // Phase 31: constructed before HereService, which takes it as a dependency.
            var hereExclusionService = new HereExclusionService(this.logger);
            this.HereExclusionService = hereExclusionService;

            this.HereService = new HereService(this.AchievementService, this.CurrentMapService, this.gw2ApiManager, this.BitAlignmentService, this.MarkerPackIndexService, this.NearestObjectiveService, this.HereExclusionService, hereGuidanceFilter, this.logger, this.contentsManager);
            this.HereCap = hereCap;
            this.PathingBridge = new PathingBridge(this.logger);

            this.FormattedLabelHtmlService = new FormattedLabelHtmlService(this.contentsManager, this.ExternalImageService);
            var achievementTrackerService = new AchievementTrackerService(this.logger, limitAchievement);
            this.AchievementTrackerService = achievementTrackerService;

            // SessionSummaryService needs AchievementTrackerService (Phase 16, to sweep a stale tracked-
            // and-Done set at baseline), so it's constructed after it now rather than alongside HereService.
            this.SessionSummaryService = new SessionSummaryService(this.AchievementService, this.BitAlignmentService, this.AchievementTrackerService, this.logger);

            // Constructed after AchievementTrackerService (Phase 16) -- needs it, MarkerPackIndexService
            // and PathingBridge, all already available above.
            var huntService = new HuntService(this.PathingBridge, this.MarkerPackIndexService, this.AchievementTrackerService, this.CurrentMapService, huntMode, this.logger);
            this.HuntService = huntService;

            this.AchievementCardFactory = new AchievementCardFactory(this.AchievementTrackerService, this.AchievementService, this.TextureService, this.HuntService, this.NearestObjectiveService, this.CurrentMapService);
            this.InspectorWindowManager = new InspectorWindowManager(this.graphicsService, this.contentsManager, this.AchievementService, this.WikiSubpageDataService, this.BitAlignmentService, this.HuntService, this.NearestObjectiveService, this.CurrentMapService, this.FormattedLabelHtmlService, this.ExternalImageService, this.AchievementTrackerService);
            this.AchievementItemOverviewFactory = new AchievementItemOverviewFactory(this.AchievementCardFactory, this.AchievementService, this.BitAlignmentService);
            this.PersistenceService = new PersistenceService(this.directoriesManager, achievementTrackerService, this.logger, achievementService, this.HuntService, this.HereExclusionService, autoSave);

            await this.HereService.LoadAsync(cancellationToken);
            await achievementService.LoadAsync(cancellationToken);
            await this.WikiSubpageDataService.LoadAsync(cancellationToken);

            // Not awaited: Here works without the pack index and picks it up via Changed once this
            // finishes (Ready starts false). Started after the wiki data load rather than in parallel
            // with it so a slow pack parse doesn't compete with achievement data for the module's
            // startup I/O.
            _ = Task.Run(() => this.MarkerPackIndexService.LoadAsync(cancellationToken), cancellationToken);

            achievementTrackerService.Load(this.PersistenceService);
            huntService.Load(this.PersistenceService);
            hereExclusionService.Load(this.PersistenceService);
        }
    }
}
