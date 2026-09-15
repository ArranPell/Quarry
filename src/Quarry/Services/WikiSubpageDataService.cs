using Quarry.Interfaces;
using Quarry.Models;
using Blish_HUD;
using Blish_HUD.Modules.Managers;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Quarry.Services
{
    public class WikiSubpageDataService : IWikiSubpageDataService
    {
        private const string FileName = "derived_subpages.json";

        private readonly ContentsManager contentsManager;
        private readonly Logger logger;

        public IReadOnlyDictionary<string, DerivedSubpage> ByLink { get; private set; } = new Dictionary<string, DerivedSubpage>();

        public WikiSubpageDataService(ContentsManager contentsManager, Logger logger)
        {
            this.contentsManager = contentsManager;
            this.logger = logger;
        }

        // Shipped in the .bhm (ref/derived_subpages.json), not downloaded -- Phase 48's embed decision
        // (DECISIONS.md 2026-09-13/14). Same GetFileStream + DeserializeAsync pattern HereService uses
        // for here_map_categories.json.
        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using (var stream = this.contentsManager.GetFileStream(FileName))
                {
                    var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    var deserialized = await JsonSerializer.DeserializeAsync<Dictionary<string, DerivedSubpage>>(stream, options, cancellationToken);

                    // The rest of the module's link matching (WikiLocationService, historically) has
                    // always been case-insensitive -- System.Text.Json's dictionary keys are ordinal by
                    // default, so rebuild with the same comparer rather than silently tightening matching.
                    this.ByLink = new Dictionary<string, DerivedSubpage>(deserialized ?? new Dictionary<string, DerivedSubpage>(), StringComparer.OrdinalIgnoreCase);
                }

                this.logger.Info($"WikiSubpageDataService: loaded {this.ByLink.Count} derived subpage(s) from {FileName}.");
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, $"Failed to load {FileName}; wiki-sourced locations and Inspector prose/images will be unavailable this session.");
            }
        }
    }
}
