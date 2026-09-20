using Blish_HUD;
using Blish_HUD.Modules.Managers;
using Quarry.Interfaces;
using Quarry.Models;
using Quarry.WikiData.Achievement;
using Flurl.Http;
using Gw2Sharp.WebApi.V2.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Quarry.Services
{
    public class AchievementService : IAchievementService, IDisposable
    {
        // Repointed to our own SSRD static branch (bhud-static/ArranPell.Quarry) 2.0.2, 2026-09-20 --
        // byte-identical mirrors of Denrage's files, so cached installs re-verify without redownloading.
        private const string DataVersionUrl = "https://bhm.blishhud.com/ArranPell.Quarry/data/version.json";
        private const string AchievementDataUrl = "https://bhm.blishhud.com/ArranPell.Quarry/data/achievement_data.json";
        private const string AchievementTablesUrl = "https://bhm.blishhud.com/ArranPell.Quarry/data/achievement_tables.json";
        private const string VersionFileName = "version.json";
        private const string AchievementDataFileName = "achievement_data.json";
        private const string AchievementTablesFileName = "achievement_tables.json";

        private readonly ContentsManager contentsManager;
        private readonly Gw2ApiManager gw2ApiManager;
        private readonly Logger logger;
        private readonly DirectoriesManager directoriesManager;
        private readonly Func<IPersistenceService> getPersistenceService;
        private readonly ITextureService textureService;
        private readonly Func<IBitAlignmentService> getBitAlignmentService;
        private Task trackAchievementProgressTask;
        private bool permissionsWarningLogged;
        private bool loadRetryScheduled;
        private CancellationTokenSource trackAchievementProgressCancellationTokenSource;
        private readonly CancellationTokenSource apiAchievementsCancellationTokenSource = new CancellationTokenSource();

        public Dictionary<int, List<int>> ManualCompletedAchievements { get; set; } = new Dictionary<int, List<int>>();

        // Phase 56 (review item 23): held while ManualCompletedAchievements or one of its lists is
        // edited (a manual tick on the main thread, the poll on the pool) and while PersistenceService
        // snapshots it for a save. A separate object because the dictionary itself is replaced on load.
        public object ManualCompletedSync { get; } = new object();

        public IEnumerable<AccountAchievement> PlayerAchievements { get; private set; }

        public IReadOnlyDictionary<int, AccountAchievement> PlayerAchievementsById { get; private set; } = new Dictionary<int, AccountAchievement>();

        public IReadOnlyList<AchievementTableEntry> Achievements { get; private set; }

        public IReadOnlyDictionary<int, AchievementTableEntry> AchievementsById { get; private set; } = new Dictionary<int, AchievementTableEntry>();

        // Lazy: achievement_tables.json is 20.7 MB. Its readers (InspectorWindow's wiki Notes column and
        // Map-column fallback image, Phase 47) are click-driven, so deserializing it eagerly at startup
        // paid for a parse most sessions never
        // use. Set once the download/freshness check has run, same as the other two files.
        private Lazy<Task<IReadOnlyList<CollectionAchievementTable>>> achievementDetailsLazy =
            new Lazy<Task<IReadOnlyList<CollectionAchievementTable>>>(() => Task.FromResult((IReadOnlyList<CollectionAchievementTable>)Array.Empty<CollectionAchievementTable>()));

        public Task<IReadOnlyList<CollectionAchievementTable>> GetAchievementDetailsAsync() => this.achievementDetailsLazy.Value;

        // The cached file's own last-write time -- a real, auto-updating freshness signal for the wiki
        // Notes column (Phase 47) rather than a hand-typed date that goes stale the next time the data
        // refreshes. UTC because DateTime.MinValue (before LoadAsync has run) needs no timezone thought.
        public DateTime AchievementTablesSnapshotDate { get; private set; }

        public IEnumerable<AchievementGroup> AchievementGroups { get; private set; }

        public IEnumerable<AchievementCategory> AchievementCategories { get; private set; }

        public event Action PlayerAchievementsLoaded;

        public event Action ApiAchievementsLoaded;

        public AchievementService(ContentsManager contentsManager, Gw2ApiManager gw2ApiManager, Logger logger, DirectoriesManager directoriesManager, Func<IPersistenceService> getPersistenceService, ITextureService textureService, Func<IBitAlignmentService> getBitAlignmentService)
        {
            this.contentsManager = contentsManager;
            this.gw2ApiManager = gw2ApiManager;
            this.logger = logger;
            this.directoriesManager = directoriesManager;
            this.getPersistenceService = getPersistenceService;
            this.textureService = textureService;
            this.getBitAlignmentService = getBitAlignmentService;
        }

        // The bit parameter is a wiki row index (matches the entries the Inspector iterates),
        // not a raw API bit -- translate through BitAlignmentService before storing, same as
        // HasFinishedAchievementBit below, so ManualCompletedAchievements keeps storing true bit indices.
        public void ToggleManualCompleteStatus(int achievementId, int bit)
        {
            bit = this.getBitAlignmentService().MapRowToBit(achievementId, bit);

            if (bit < 0)
            {
                return;
            }

            if (this.PlayerAchievementsById.TryGetValue(achievementId, out var achievement))
            {
                if (achievement.Done)
                {
                    return;
                }

                if (achievement.Bits?.Contains(bit) ?? false)
                {
                    return;
                }
            }

            lock (this.ManualCompletedSync)
            {
                if (!this.ManualCompletedAchievements.TryGetValue(achievementId, out var achievementBits))
                {
                    achievementBits = new List<int>();
                    this.ManualCompletedAchievements[achievementId] = achievementBits;
                }

                if (achievementBits.Contains(bit))
                {
                    _ = achievementBits.Remove(bit);
                }
                else
                {
                    achievementBits.Add(bit);
                }
            }

            this.PlayerAchievementsLoaded?.Invoke();
        }

        private static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        private bool CheckMd5(string md5ToCheck, string filePath)
        {
            using (var md5 = MD5.Create())
            using (var fileStream = System.IO.File.Open(filePath, FileMode.Open))
            {
                return md5ToCheck.Equals(ByteArrayToString(md5.ComputeHash(fileStream)), StringComparison.OrdinalIgnoreCase);
            }
        }

        // Phase 56 (review item 7): download to a temp name and only replace the cached file once the
        // md5 matches -- a dropped transfer used to overwrite the very copy the "continuing with the
        // cached copy" fallback then relied on, and the unconditional deserialize below threw on it.
        private async Task<bool> DownloadFile(string url, string folder, string fileName, string md5)
        {
            var tempName = fileName + ".tmp";
            var tempPath = Path.Combine(folder, tempName);
            var finalPath = Path.Combine(folder, fileName);

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                _ = await url.DownloadFileAsync(folder, tempName);

                if (System.IO.File.Exists(tempPath) && this.CheckMd5(md5, tempPath))
                {
                    System.IO.File.Copy(tempPath, finalPath, overwrite: true);
                    System.IO.File.Delete(tempPath);
                    return true;
                }
            }

            TryDeleteQuietly(tempPath);
            this.logger.Warn($"Couldn't download {url}: three attempts, none matched the published md5.");
            return false;
        }

        private static void TryDeleteQuietly(string path)
        {
            try
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
            catch (Exception)
            {
                // Best effort; a stray .tmp is harmless.
            }
        }

        // Phase 56 (review item 25): a first run with no cached data and no network used to leave the
        // module half-loaded for the whole session with nothing but a log line. One retry on the same
        // five-minute cadence the API paths already use.
        private void ScheduleLoadRetry(CancellationToken cancellationToken)
        {
            if (this.loadRetryScheduled)
            {
                return;
            }

            this.loadRetryScheduled = true;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                    this.logger.Info("Retrying the achievement data download.");
                    await this.LoadAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Module disabled while waiting.
                }
                catch (Exception ex)
                {
                    this.logger.Warn(ex, "The achievement data download retry failed; not retrying again this session.");
                }
            }, cancellationToken);
        }

        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            var overallStopwatch = Stopwatch.StartNew();
            this.logger.Debug("Reading saved achievement information");
            var serializerOptions = new JsonSerializerOptions()
            {
                Converters = { new RewardConverter(), new AchievementTableEntryDescriptionConverter(), new CollectionAchievementTableEntryConverter() },
            };

            var dataFolder = this.directoriesManager.GetFullDirectoryPath(ModuleConstants.DataDirectoryName);
            _ = Directory.CreateDirectory(dataFolder);

            var hasCachedFiles =
                System.IO.File.Exists(Path.Combine(dataFolder, VersionFileName)) &&
                System.IO.File.Exists(Path.Combine(dataFolder, AchievementDataFileName)) &&
                System.IO.File.Exists(Path.Combine(dataFolder, AchievementTablesFileName));

            var downloadData = !hasCachedFiles;

            if (hasCachedFiles)
            {
                AchievementDataMetadata githubMetadata = null;
                var stageStopwatch = Stopwatch.StartNew();

                try
                {
                    githubMetadata = await DataVersionUrl.GetJsonAsync<AchievementDataMetadata>();
                    this.logger.Debug($"Startup timing: remote version.json GET took {stageStopwatch.ElapsedMilliseconds} ms");
                }
                catch (Exception ex)
                {
                    this.logger.Warn(ex, "Couldn't reach the wiki data server to check for updates; using the cached copy.");
                }

                // Phase 56 (review item 26): the local read/parse used to share the remote GET's catch, so
                // a corrupt version.json read as "server unreachable" and pinned the stale cache forever.
                // Its own failure means "re-download", not "keep".
                if (githubMetadata != null)
                {
                    try
                    {
                        using (var metadata = System.IO.File.Open(Path.Combine(dataFolder, VersionFileName), FileMode.Open))
                        {
                            var localMetadata = await JsonSerializer.DeserializeAsync<AchievementDataMetadata>(metadata, serializerOptions, cancellationToken);

                            if (localMetadata is null)
                            {
                                throw new InvalidDataException("version.json deserialized to null.");
                            }

                            if (localMetadata.Version != -1) // Debug skip
                            {
                                if (localMetadata.Version != githubMetadata.Version)
                                {
                                    downloadData = true;
                                }
                                else
                                {
                                    stageStopwatch.Restart();
                                    var achievementDataFresh = this.CheckMd5(githubMetadata.AchievementDataMd5, Path.Combine(dataFolder, AchievementDataFileName));
                                    this.logger.Debug($"Startup timing: achievement_data.json md5 check took {stageStopwatch.ElapsedMilliseconds} ms");

                                    stageStopwatch.Restart();
                                    var achievementTablesFresh = this.CheckMd5(githubMetadata.AchievementTablesMd5, Path.Combine(dataFolder, AchievementTablesFileName));
                                    this.logger.Debug($"Startup timing: achievement_tables.json md5 check took {stageStopwatch.ElapsedMilliseconds} ms");

                                    if (!achievementDataFresh || !achievementTablesFresh)
                                    {
                                        downloadData = true;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        this.logger.Warn(ex, "The cached version.json is unreadable; re-downloading the wiki data.");
                        downloadData = true;
                    }
                }
            }

            if (downloadData)
            {
                this.logger.Info("Downloading AchievementData");

                try
                {
                    // Phase 56 (review item 7): version.json is fetched to a temp name and only promoted
                    // once both data files have downloaded and verified, so the three cached files can
                    // never disagree with each other after a partial run.
                    var versionTempName = VersionFileName + ".tmp";
                    var versionTempPath = Path.Combine(dataFolder, versionTempName);
                    AchievementDataMetadata localMetadata;

                    _ = await DataVersionUrl.DownloadFileAsync(dataFolder, versionTempName);

                    using (var metadata = System.IO.File.Open(versionTempPath, FileMode.Open))
                    {
                        localMetadata = await JsonSerializer.DeserializeAsync<AchievementDataMetadata>(metadata, serializerOptions, cancellationToken);
                    }

                    var stageStopwatch = Stopwatch.StartNew();
                    var achievementDataOk = await this.DownloadFile(AchievementDataUrl, dataFolder, AchievementDataFileName, localMetadata.AchievementDataMd5);
                    this.logger.Debug($"Startup timing: achievement_data.json download took {stageStopwatch.ElapsedMilliseconds} ms");

                    var achievementTablesOk = achievementDataOk;
                    if (achievementDataOk)
                    {
                        stageStopwatch.Restart();
                        achievementTablesOk = await this.DownloadFile(AchievementTablesUrl, dataFolder, AchievementTablesFileName, localMetadata.AchievementTablesMd5);
                        this.logger.Debug($"Startup timing: achievement_tables.json download took {stageStopwatch.ElapsedMilliseconds} ms");
                    }

                    if (achievementDataOk && achievementTablesOk)
                    {
                        System.IO.File.Copy(versionTempPath, Path.Combine(dataFolder, VersionFileName), overwrite: true);
                        TryDeleteQuietly(versionTempPath);
                    }
                    else
                    {
                        TryDeleteQuietly(versionTempPath);

                        if (!hasCachedFiles)
                        {
                            this.logger.Error("Failed to download achievement data and no cached copy exists; the module cannot load achievement information. Retrying in 5 minutes.");
                            this.ScheduleLoadRetry(cancellationToken);
                            return;
                        }

                        this.logger.Warn("Failed to download fresh achievement data; continuing with the cached copy.");
                    }
                }
                catch (Exception ex)
                {
                    // Phase 56 (review items 12 and 25): tell the user through Blish's own dialog when the
                    // data folder can't be written or the server can't be reached on a first run -- a log
                    // line alone left the module silently half-loaded.
                    if (ex is UnauthorizedAccessException)
                    {
                        Blish_HUD.Debug.Contingency.NotifyFileSaveAccessDenied(dataFolder, "cache Quarry's achievement data");
                    }
                    else if (!hasCachedFiles)
                    {
                        Blish_HUD.Debug.Contingency.NotifyHttpAccessDenied("download Quarry's achievement data from bhm.blishhud.com");
                    }

                    if (!hasCachedFiles)
                    {
                        this.logger.Error(ex, "Failed to download achievement data and no cached copy exists; the module cannot load achievement information. Retrying in 5 minutes.");
                        this.ScheduleLoadRetry(cancellationToken);
                        return;
                    }

                    this.logger.Warn(ex, "Failed to download fresh achievement data; continuing with the cached copy.");
                }
            }

            try
            {
                var stageStopwatch = Stopwatch.StartNew();
                using (var achievements = System.IO.File.Open(Path.Combine(dataFolder, AchievementDataFileName), FileMode.Open))
                {
                    this.Achievements = (await JsonSerializer.DeserializeAsync<List<AchievementTableEntry>>(achievements, serializerOptions, cancellationToken)).AsReadOnly();
                }
                this.logger.Debug($"Startup timing: achievement_data.json deserialize took {stageStopwatch.ElapsedMilliseconds} ms");

                // The wiki data has occasional duplicate ids; last-one-wins rather than throwing like
                // ToDictionary would.
                var achievementsById = new Dictionary<int, AchievementTableEntry>();
                foreach (var achievement in this.Achievements)
                {
                    achievementsById[achievement.Id] = achievement;
                }

                this.AchievementsById = achievementsById;

                // Phase 46: deserialized on first access instead of here. The download and md5 checks
                // above already ran (or the cached copy is known-good), so all this defers is the parse.
                var achievementTablesPath = Path.Combine(dataFolder, AchievementTablesFileName);
                this.AchievementTablesSnapshotDate = System.IO.File.GetLastWriteTimeUtc(achievementTablesPath);
                this.achievementDetailsLazy = new Lazy<Task<IReadOnlyList<CollectionAchievementTable>>>(async () =>
                {
                    var lazyStopwatch = Stopwatch.StartNew();
                    using (var achievementDetails = System.IO.File.Open(achievementTablesPath, FileMode.Open))
                    {
                        var result = (await JsonSerializer.DeserializeAsync<List<CollectionAchievementTable>>(achievementDetails, serializerOptions)).AsReadOnly();
                        this.logger.Debug($"Startup timing: achievement_tables.json deserialize took {lazyStopwatch.ElapsedMilliseconds} ms (deferred)");
                        return (IReadOnlyList<CollectionAchievementTable>)result;
                    }
                });
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Exception occured on deserializing cached achievement data!");
                throw;
            }

            // The one startup line kept at Info (Phase 56, review item 27); the per-stage timings above and
            // below are Debug.
            this.logger.Info($"Startup timing: achievement data ready in {overallStopwatch.ElapsedMilliseconds} ms ({(downloadData ? "downloaded" : "cached")}); {this.Achievements.Count} achievements.");

            this.ManualCompletedAchievements = this.getPersistenceService().Get().ManualCompletedAchievements;

            var apiStopwatch = Stopwatch.StartNew();
            _ = Task.Run(async () =>
            {
                await this.InitializeApiAchievements(this.apiAchievementsCancellationTokenSource.Token);
                this.logger.Debug($"Startup timing: AchievementCategories/AchievementGroups took {apiStopwatch.ElapsedMilliseconds} ms");
            });

            // Don't block module load (and the corner icon/tab appearing) on this account API round-trip;
            // every consumer already handles PlayerAchievements being null and reacts to
            // PlayerAchievementsLoaded once it completes.
            var accountStopwatch = Stopwatch.StartNew();
            _ = this.LoadPlayerAchievements(cancellationToken: cancellationToken).ContinueWith(_ =>
                this.logger.Debug($"Startup timing: Account.Achievements took {accountStopwatch.ElapsedMilliseconds} ms"), cancellationToken);
        }

        private async Task InitializeApiAchievements(CancellationToken cancellationToken = default)
        {
            this.logger.Debug("Getting achievement data from api");

            try
            {
                this.AchievementGroups = await this.gw2ApiManager.Gw2ApiClient.V2.Achievements.Groups.AllAsync(cancellationToken);
                this.AchievementCategories = await this.gw2ApiManager.Gw2ApiClient.V2.Achievements.Categories.AllAsync(cancellationToken);

                foreach (var category in this.AchievementCategories)
                {
                    //Store texture
                    _ = this.textureService.GetTexture(category.Icon);
                }

                this.logger.Debug("Finished getting achievement data from api");

                this.ApiAchievementsLoaded?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // Module was disabled while waiting on the API or the retry delay below.
            }
            catch (Exception ex)
            {
                this.logger.Warn(ex, "Failed getting api achievements. Retrying in 5 minutes");

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                _ = Task.Run(async () => await this.InitializeApiAchievements(cancellationToken), cancellationToken);
            }
        }

        // The API is the authority when it says Done. But Blish's account cache lags by minutes, which is
        // the entire reason right-click ticking exists -- so an achievement whose every bit is accounted
        // for (API bits plus manual ticks) counts as finished now rather than whenever the poll catches
        // up. Without this, ticking the last step left the card on screen, the achievement in the Track
        // window, and no "Done:" toast, until the API agreed (load-test, 2026-09-10).
        public bool HasFinishedAchievement(int achievementId)
        {
            if (this.PlayerAchievementsById.TryGetValue(achievementId, out var achievement) && achievement.Done)
            {
                return true;
            }

            return this.IsCompleteFromBits(achievementId, achievement);
        }

        private bool IsCompleteFromBits(int achievementId, AccountAchievement achievement)
        {
            if (!this.ManualCompletedAchievements.TryGetValue(achievementId, out var manualBits) || manualBits.Count == 0)
            {
                return false;
            }

            // The total bit count only comes from the API's achievement record; with no record we can't
            // know how many steps there are, so we can't claim it's finished.
            if (!this.getBitAlignmentService().TryGetCachedAchievement(achievementId, out var apiAchievement) ||
                apiAchievement.Bits is null ||
                apiAchievement.Bits.Count == 0)
            {
                return false;
            }

            for (var bit = 0; bit < apiAchievement.Bits.Count; bit++)
            {
                var doneOnAccount = achievement?.Bits?.Contains(bit) ?? false;

                if (!doneOnAccount && !manualBits.Contains(bit))
                {
                    return false;
                }
            }

            return true;
        }

        // positionIndex arrives as a wiki row index; BitAlignmentService (Phase 23) translates it to the
        // API's actual bit index (or leaves it unchanged if no alignment has been computed for this id
        // yet, or -1 if alignment ran but this row couldn't be resolved). Replaces the old hand-written
        // specialSnowflakeCompletedHandling table, which only covered ten ids.
        public bool HasFinishedAchievementBit(int achievementId, int positionIndex)
        {
            var bitIndex = this.getBitAlignmentService().MapRowToBit(achievementId, positionIndex);

            if (bitIndex < 0)
            {
                return false;
            }

            if (this.ManualCompletedAchievements.TryGetValue(achievementId, out var manualAchievement))
            {
                if (manualAchievement.Contains(bitIndex))
                {
                    return true;
                }
            }

            return this.PlayerAchievementsById.TryGetValue(achievementId, out var achievement) && (achievement.Bits?.Contains(bitIndex) ?? false);
        }

        // Phase 17: bit is already an API bit index (a marker-pack objective's achievementBit), so unlike
        // HasFinishedAchievementBit above there's no MapRowToBit translation -- same lookup otherwise.
        public bool HasFinishedBitIndex(int achievementId, int bit)
        {
            if (bit < 0)
            {
                return false;
            }

            if (this.ManualCompletedAchievements.TryGetValue(achievementId, out var manualAchievement) && manualAchievement.Contains(bit))
            {
                return true;
            }

            return this.PlayerAchievementsById.TryGetValue(achievementId, out var achievement) && (achievement.Bits?.Contains(bit) ?? false);
        }

        public async Task LoadPlayerAchievements(bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            if (forceRefresh || this.PlayerAchievements == null)
            {
                if (this.gw2ApiManager.HasPermissions(new[] { TokenPermission.Account, TokenPermission.Progression }))
                {
                    this.logger.Debug("Refreshing Player Achievements");
                    try
                    {
                        this.PlayerAchievements = await this.gw2ApiManager.Gw2ApiClient.V2.Account.Achievements.GetAsync(cancellationToken);
                        this.PlayerAchievementsById = this.PlayerAchievements.ToDictionary(a => a.Id, a => a);

                        lock (this.ManualCompletedSync)
                        {
                            foreach (var item in this.PlayerAchievements)
                            {
                                if (this.ManualCompletedAchievements.TryGetValue(item.Id, out var achievementBits))
                                {
                                    if (item.Done)
                                    {
                                        _ = this.ManualCompletedAchievements.Remove(item.Id);
                                    }
                                    else
                                    {
                                        foreach (var bit in item.Bits ?? Array.Empty<int>())
                                        {
                                            if (achievementBits.Contains(bit))
                                            {
                                                _ = achievementBits.Remove(bit);
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        _ = Task.Run(() =>
                        {
                            try
                            {
                                this.PlayerAchievementsLoaded?.Invoke();
                            }
                            catch (Exception ex)
                            {
                                this.logger.Error(ex, "Exception occured in a PlayerAchievementsLoaded subscriber.");
                            }
                        }, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        this.logger.Warn(ex, "Exception occured during refresh of player achievements. Skipping this time.");
                    }

                    this.TrackAchievementProgress();
                }
                else if (!this.permissionsWarningLogged)
                {
                    // Phase 56 (review item 15): this means the whole hunter has no account progress to
                    // work with, so it is a Warn -- but it also fires transiently at startup before Blish's
                    // subtoken lands, so once per session and worded for both cases.
                    this.permissionsWarningLogged = true;
                    this.logger.Warn("API key permissions 'account' and 'progression' not granted (yet): achievement progress is unavailable until they are. Normal for a moment at startup, before the subtoken arrives; a problem if it persists.");
                }
            }
        }

        private void TrackAchievementProgress()
        {
            if (this.trackAchievementProgressTask != null)
            {
                return;
            }

            this.trackAchievementProgressCancellationTokenSource = new CancellationTokenSource();
            this.trackAchievementProgressTask = Task.Run(this.TrackAchievementProgressMethod);
        }

        private async Task TrackAchievementProgressMethod()
        {
            try
            {
                while (true)
                {
                    this.trackAchievementProgressCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    await Task.Delay(TimeSpan.FromMinutes(5), this.trackAchievementProgressCancellationTokenSource.Token);
                    await this.LoadPlayerAchievements(true, this.trackAchievementProgressCancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            { /* NOOP */ }
        }

        public void Dispose()
        {
            // Null when TrackAchievementProgress() was never started (e.g. permissions not granted).
            this.trackAchievementProgressCancellationTokenSource?.Cancel();
            this.trackAchievementProgressCancellationTokenSource?.Dispose();
            this.apiAchievementsCancellationTokenSource.Cancel();
            this.apiAchievementsCancellationTokenSource.Dispose();
        }
    }
}
