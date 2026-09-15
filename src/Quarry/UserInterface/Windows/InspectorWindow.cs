using Quarry.Interfaces;
using Quarry.Models;
using Quarry.Services;
using Quarry.UserInterface.Controls;
using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Modules.Managers;
using Quarry.WikiData.Achievement;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Quarry.WikiData.Achievement.CollectionAchievementTable;

namespace Quarry.UserInterface.Windows
{
    // Phase 45. UI-DESIGN.md §7. The one detail surface: a new click replaces the content instead of
    // opening another window. It replaced AchievementDetailsWindow and SubPageInformationWindow (both
    // deleted by Phase 53) over the same rendering they used (FormattedLabelHtmlService,
    // ExternalImageService, the derived subpage data). The chip grid is its own, built directly against
    // IAchievementService/IBitAlignmentService rather than the old vertical AchievementListControl.
    public class InspectorWindow : WindowBase2
    {
        private const int WindowWidth = 320;
        private const int WindowHeight = 520;
        private const int ChipSize = 24;
        private const int ChipRingSize = 28;
        // Load-test 2026-09-13: text set to the full ContentRegion.Width sat right against (sometimes
        // under) the scrollbar CanScroll adds -- same "the panel doesn't know about its own scrollbar"
        // gap CardGrid.ScrollbarAllowance already covers on the card grids.
        private const int ContentScrollbarAllowance = 14;
        private const int ImagePreviewHeight = 160;
        private const int ImageExpandedHeight = 320;

        private static readonly Logger Logger = Logger.GetLogger<InspectorWindow>();

        private readonly IAchievementService achievementService;
        private readonly IWikiSubpageDataService wikiSubpageDataService;
        private readonly IBitAlignmentService bitAlignmentService;
        private readonly IHuntService huntService;
        private readonly INearestObjectiveService nearestObjectiveService;
        private readonly ICurrentMapService currentMapService;
        private readonly IFormattedLabelHtmlService formattedLabelHtmlService;
        private readonly IExternalImageService externalImageService;
        private readonly IAchievementTrackerService achievementTrackerService;
        private readonly IInspectorWindowManager manager;

        private AchievementTableEntry achievement;
        private int? objectiveIndex;
        private FlowPanel contentPanel;
        private Label pinLabel;
        private bool isPinned;

        public InspectorWindow(
            ContentsManager contentsManager,
            IAchievementService achievementService,
            IWikiSubpageDataService wikiSubpageDataService,
            IBitAlignmentService bitAlignmentService,
            IHuntService huntService,
            INearestObjectiveService nearestObjectiveService,
            ICurrentMapService currentMapService,
            IFormattedLabelHtmlService formattedLabelHtmlService,
            IExternalImageService externalImageService,
            IAchievementTrackerService achievementTrackerService,
            IInspectorWindowManager manager,
            bool isDefault)
        {
            this.achievementService = achievementService;
            this.wikiSubpageDataService = wikiSubpageDataService;
            this.bitAlignmentService = bitAlignmentService;
            this.huntService = huntService;
            this.nearestObjectiveService = nearestObjectiveService;
            this.currentMapService = currentMapService;
            this.formattedLabelHtmlService = formattedLabelHtmlService;
            this.externalImageService = externalImageService;
            this.achievementTrackerService = achievementTrackerService;
            this.manager = manager;

            var texture = contentsManager.GetTexture("window_blank.png");
            this.ConstructWindow(texture, new Rectangle(0, 0, WindowWidth, WindowHeight), new Rectangle(0, 30, WindowWidth, WindowHeight - 30));

            // Only the default (unpinned) instance persists a position -- SavesPosition/Id is Blish's own
            // per-window storage (independent of this module's persistanceStorage.json), and every pinned
            // copy sharing one Id would fight over the same saved spot.
            if (isDefault)
            {
                this.SavesPosition = true;
                this.Id = "Quarry_InspectorWindow";
            }

            this.Emblem = contentsManager.GetTexture("achievement_icon.png");
            // Load-test 2026-09-13: the achievement/objective name here as well as directly under the
            // title bar was redundant, and at the native title font (32px, confirmed against Blish v1.2.0
            // source) it overran the close button before the name even got interesting -- a fixed label
            // says what the window is instead.
            this.Title = "Inspector";

            // UI-DESIGN calls for this at native size left of the close button, inside the title bar
            // itself -- WindowBase2 has no such extension point, so it sits at the top-right of the
            // content area instead. Flagged for ArranPell: same idea, different pixel.
            this.pinLabel = new Label
            {
                Parent = this,
                Text = "Pin",
                Font = UiStyle.BodyFont,
                Width = 40,
                Height = 16,
                HorizontalAlignment = HorizontalAlignment.Right,
                Location = new Point(this.ContentRegion.Width - 44, 2),
                BasicTooltipText = "Freeze this Inspector and open a fresh one on the next click",
            };

            UiStyle.ApplyShadow(this.pinLabel, UiStyle.TextSecondary);

            this.pinLabel.Click += (s, e) => this.TogglePin();
        }

        public override void PaintBeforeChildren(SpriteBatch spriteBatch, Rectangle bounds)
        {
            WindowBodyPainter.PaintBody(spriteBatch, this, this.ContentRegion, showLeftAccent: false);
            base.PaintBeforeChildren(spriteBatch, bounds);
        }

        private void TogglePin()
        {
            this.isPinned = !this.isPinned;
            this.pinLabel.Text = this.isPinned ? "Pinned" : "Pin";
            UiStyle.ApplyShadow(this.pinLabel, this.isPinned ? UiStyle.NearDone : UiStyle.TextSecondary);

            if (this.isPinned)
            {
                this.manager.NotifyPinned(this);
            }
        }

        public void SetAchievement(AchievementTableEntry achievement)
        {
            this.achievement = achievement;
            this.objectiveIndex = null;
            this.RebuildContent();
        }

        public void SetObjective(AchievementTableEntry achievement, int index)
        {
            this.achievement = achievement;
            this.objectiveIndex = index;
            this.RebuildContent();
        }

        private void RebuildContent()
        {
            this.contentPanel?.Dispose();

            this.contentPanel = new FlowPanel
            {
                Parent = this,
                Width = this.ContentRegion.Width,
                Height = this.ContentRegion.Height,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 8),
                OuterControlPadding = new Vector2(10, 10),
                CanScroll = true,
            };

            if (this.objectiveIndex.HasValue)
            {
                this.BuildObjectiveView();
            }
            else
            {
                this.BuildAchievementView();
            }
        }

        private void BuildAchievementView()
        {
            var contentWidth = (this.contentPanel.ContentRegion.Width - ContentScrollbarAllowance);

            var nameLabel = new Label
            {
                Parent = this.contentPanel,
                Text = this.achievement.Name.Trim(),
                Font = UiStyle.NumeralFont,
                WrapText = true,
                AutoSizeHeight = true,
                Width = contentWidth,
            };

            UiStyle.ApplyTextPrimary(nameLabel);

            this.BuildChipRow(null);

            var description = this.achievement.Description;

            if (!string.IsNullOrEmpty(description?.GameText))
            {
                var label = this.formattedLabelHtmlService.CreateLabel(description.GameText).AutoSizeHeight().SetWidth(contentWidth).Wrap().Build();
                label.Parent = this.contentPanel;
            }

            if (!string.IsNullOrEmpty(description?.GameHint))
            {
                var label = this.formattedLabelHtmlService.CreateLabel(description.GameHint).AutoSizeHeight().SetWidth(contentWidth).Wrap().Build();
                label.Parent = this.contentPanel;
            }

            this.BuildNextLine(bitIndex: null);
            this.BuildActions(entryLink: null);
        }

        private void BuildObjectiveView()
        {
            var index = this.objectiveIndex.Value;
            var entries = this.GetEntries();
            var entry = index < entries.Count ? entries[index] : (DisplayName: string.Empty, Link: (string)null);
            var contentWidth = (this.contentPanel.ContentRegion.Width - ContentScrollbarAllowance);

            var breadcrumb = new Label
            {
                Parent = this.contentPanel,
                Text = $"{this.achievement.Name.Trim()} · objective {index + 1} of {entries.Count}",
                Font = UiStyle.SectionFont,
                WrapText = true,
                AutoSizeHeight = true,
                Width = contentWidth,
                BasicTooltipText = "Back to the achievement",
            };

            UiStyle.ApplyShadow(breadcrumb, UiStyle.TextSecondary);
            breadcrumb.Click += (s, e) => this.SetAchievement(this.achievement);

            // Load-test 2026-09-13: the objective's text (often a full sentence for TEXT_LIST
            // achievements) sitting above the chip row read as if it belonged to the breadcrumb rather
            // than to a specific chip. Chips first, text below -- same order the achievement view uses
            // (name, then chips, then description).
            this.BuildChipRow(index);

            var nameLabel = new Label
            {
                Parent = this.contentPanel,
                Text = entry.DisplayName ?? string.Empty,
                Font = UiStyle.NumeralFont,
                WrapText = true,
                AutoSizeHeight = true,
                Width = contentWidth,
            };

            UiStyle.ApplyTextPrimary(nameLabel);

            var bitIndex = this.bitAlignmentService.MapRowToBit(this.achievement.Id, index);
            var subPage = this.FindSubPage(entry.Link);
            var hasSubPageImage = !string.IsNullOrEmpty(subPage?.ImageUrl);

            if (hasSubPageImage)
            {
                var subPageTexture = this.externalImageService.GetImageFromIndirectLink(subPage.ImageUrl);
                var subPageImage = new ImageSpinner(subPageTexture)
                {
                    Parent = this.contentPanel,
                    Width = contentWidth,
                    Height = ImagePreviewHeight,
                };
                MakeImageExpandable(subPageImage, subPageTexture);
            }
            else
            {
                // Load-test 2026-09-14: a row with no wiki subpage link (like "Mysterious Focus Etching")
                // showed no image at all, which is a real regression against the old ItemDetailWindow --
                // achievement_tables.json carries its own "Map" column with a location screenshot
                // independent of any subpage link, and that window already rendered it
                // (AchievementTableMapEntryFactory). Only a fallback here, not shown alongside a subpage
                // image, to avoid two location pictures stacked for the same objective.
                var mapImagePanel = new FlowPanel
                {
                    Parent = this.contentPanel,
                    Width = contentWidth,
                    HeightSizingMode = SizingMode.AutoSize,
                    FlowDirection = ControlFlowDirection.SingleTopToBottom,
                };
                _ = this.PopulateMapColumnImage(mapImagePanel, this.achievement.Id, index, contentWidth);
            }

            if (!string.IsNullOrEmpty(subPage?.Description))
            {
                var notesLabel = this.formattedLabelHtmlService.CreateLabel(subPage.Description).AutoSizeHeight().SetWidth(contentWidth).Wrap().Build();
                notesLabel.Parent = this.contentPanel;
            }

            // Placeholder created synchronously so it holds its place in the flow -- the wiki Notes
            // column (Phase 47) comes from achievement_tables.json, which is lazy since Phase 46, so
            // filling it in is a real await. Most achievements have no Notes cell at all (coverage is
            // 1,114 of 6,809), so this stays empty and invisible far more often than not.
            var hunterNotesPanel = new FlowPanel
            {
                Parent = this.contentPanel,
                Width = contentWidth,
                HeightSizingMode = SizingMode.AutoSize,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
            };
            _ = this.PopulateHunterNotes(hunterNotesPanel, this.achievement.Id, index, contentWidth);

            this.BuildNextLine(bitIndex);
            this.BuildTickToggle(index, bitIndex);
            this.BuildActions(entry.Link);
        }

        // Fallback for a row with no wiki subpage image (BuildObjectiveView only calls this when
        // hasSubPageImage is false) -- achievement_tables.json's own "Map" column carries a location
        // screenshot independent of any subpage link, the same one the old detail window rendered
        // before Phase 53 retired it. GetAchievementDetailsAsync is a cached
        // Lazy<Task<>>, so awaiting it again here alongside PopulateHunterNotes doesn't re-parse the file.
        //
        // Phase 56 (review item 6): async Task rather than async void, the whole body under one catch, and
        // the control construction on the main thread -- the lazy parse completes on the pool the first
        // time in a session, and an exception after the await in an async void is fatal to the process.
        private async Task PopulateMapColumnImage(FlowPanel container, int achievementId, int rowIndex, int contentWidth)
        {
            try
            {
                var achievementDetails = await this.achievementService.GetAchievementDetailsAsync();

                if (this.achievement?.Id != achievementId || this.objectiveIndex != rowIndex || container.Parent == null)
                {
                    return;
                }

                var table = achievementDetails.FirstOrDefault(t => t.Id == achievementId);

                if (table == null || rowIndex >= table.Entries.Count)
                {
                    return;
                }

                var mapColumnIndex = Array.FindIndex(table.ColumnNames, c => string.Equals(c, "Map", StringComparison.OrdinalIgnoreCase));
                var row = table.Entries[rowIndex];

                if (mapColumnIndex < 0 || mapColumnIndex >= row.Count ||
                    !(row[mapColumnIndex] is CollectionAchievementTableMapEntry mapEntry) ||
                    string.IsNullOrEmpty(mapEntry.ImageLink))
                {
                    return;
                }

                var imageLink = mapEntry.ImageLink;

                GameService.Overlay.QueueMainThreadUpdate(gameTime =>
                {
                    // The view may have moved on while this was queued.
                    if (this.achievement?.Id != achievementId || this.objectiveIndex != rowIndex || container.Parent == null)
                    {
                        return;
                    }

                    var mapTexture = this.externalImageService.GetImageFromIndirectLink(imageLink);
                    var mapImage = new ImageSpinner(mapTexture)
                    {
                        Parent = container,
                        Width = contentWidth,
                        Height = ImagePreviewHeight,
                    };
                    MakeImageExpandable(mapImage, mapTexture);
                });
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Inspector: couldn't add the Map-column fallback image.");
            }
        }

        // ArranPell, 2026-09-14: a click that opens the browser (matching AchievementTableMapEntryFactory's
        // old behaviour for this same image) meant an alt-tab just to see it bigger -- the texture is
        // already the wiki's full-resolution original (GetImageFromIndirectLink resolves through
        // GetDirectImageLink's "File:" page lookup before downloading), just shown small to fit the
        // preview, so there was nothing left to fetch. Toggling height in place keeps this one detail
        // surface instead of adding a pop-out window, consistent with the rest of the Inspector.
        //
        // A fixed expanded height stretched the image instead of showing it bigger (load-test
        // 2026-09-14) -- Image/ImageSpinner fills exactly the Width/Height given, it doesn't preserve
        // the source aspect ratio itself. Width is fixed (the Inspector doesn't resize), so the correct
        // expanded height is whatever keeps the *texture's own* aspect ratio at that width; falls back
        // to a fixed height only if the texture hasn't finished loading yet by the time of the click.
        private static void MakeImageExpandable(Control image, AsyncTexture2D texture)
        {
            var expanded = false;
            image.BasicTooltipText = "Click to expand";

            image.LeftMouseButtonReleased += (s, e) =>
            {
                expanded = !expanded;

                if (!expanded)
                {
                    image.Height = ImagePreviewHeight;
                }
                else
                {
                    var textureWidth = texture.Texture?.Width ?? 0;
                    var textureHeight = texture.Texture?.Height ?? 0;

                    image.Height = textureWidth > 0 && textureHeight > 0
                        ? (int)Math.Round(image.Width * ((float)textureHeight / textureWidth))
                        : ImageExpandedHeight;
                }

                image.BasicTooltipText = expanded ? "Click to shrink" : "Click to expand";
            };
        }

        // Coverage is 1,114 of 6,809 achievements (achievement_tables.json's Notes column, median 89
        // characters of hand-written hunter guidance) -- render nothing rather than an empty box for the
        // rest. Fire-and-forget: the container was already placed in the flow synchronously, so filling
        // it in late doesn't reorder anything, and most calls return at the "no Notes column" check
        // below without ever touching achievement_tables.json at all.
        //
        // Phase 56 (review item 6): async Task, one catch around everything, and the labels built on the
        // main thread -- see PopulateMapColumnImage.
        private async Task PopulateHunterNotes(FlowPanel container, int achievementId, int rowIndex, int contentWidth)
        {
            try
            {
                await this.PopulateHunterNotesCore(container, achievementId, rowIndex, contentWidth);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Inspector: couldn't add the wiki Notes column.");
            }
        }

        private async Task PopulateHunterNotesCore(FlowPanel container, int achievementId, int rowIndex, int contentWidth)
        {
            var achievementDetails = await this.achievementService.GetAchievementDetailsAsync();

            // The view may have moved to a different achievement or objective while this awaited.
            if (this.achievement?.Id != achievementId || this.objectiveIndex != rowIndex || container.Parent == null)
            {
                return;
            }

            var table = achievementDetails.FirstOrDefault(t => t.Id == achievementId);
            if (table == null)
            {
                return;
            }

            var notesColumnIndex = Array.FindIndex(table.ColumnNames, c => string.Equals(c, "Notes", StringComparison.OrdinalIgnoreCase) || string.Equals(c, "Note", StringComparison.OrdinalIgnoreCase));
            if (notesColumnIndex < 0)
            {
                // A column that contains "note" but isn't "Notes"/"Note" is worth knowing about at Debug: if
                // one shows up repeatedly, widen the match above rather than special-casing it.
                foreach (var columnName in table.ColumnNames)
                {
                    if (columnName.IndexOf("note", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Logger.Debug($"Achievement {achievementId} has a column named \"{columnName}\" that the Notes match missed.");
                    }
                }

                return;
            }

            // Both this Inspector's objectiveIndex and achievement_tables.json's row order are wiki row
            // order -- a different axis from the wiki-row-to-API-bit permutation BitAlignmentService
            // exists for (Phase 23). Bounds-check rather than assume they line up for every achievement.
            if (rowIndex >= table.Entries.Count)
            {
                Logger.Debug($"Achievement {achievementId}'s table has {table.Entries.Count} rows, fewer than objective index {rowIndex} -- row indices may not line up for this achievement.");
                return;
            }

            var row = table.Entries[rowIndex];
            if (notesColumnIndex >= row.Count || !(row[notesColumnIndex] is CollectionAchievementTableStringEntry stringEntry) || string.IsNullOrEmpty(stringEntry.Text))
            {
                return;
            }

            var notesHtml = stringEntry.Text;
            var snapshotDate = this.achievementService.AchievementTablesSnapshotDate;

            GameService.Overlay.QueueMainThreadUpdate(gameTime =>
            {
                if (this.achievement?.Id != achievementId || this.objectiveIndex != rowIndex || container.Parent == null)
                {
                    return;
                }

                var headerLabel = new Label
                {
                    Parent = container,
                    Text = $"Hunter notes — wiki data as of {snapshotDate:yyyy-MM-dd}",
                    Font = UiStyle.SectionFont,
                    WrapText = true,
                    AutoSizeHeight = true,
                    Width = contentWidth,
                };
                UiStyle.ApplyShadow(headerLabel, UiStyle.TextSecondary);

                // Load-test 2026-09-14: a height cap plus a tooltip for the overflow (two different ways of
                // detecting "did it actually clip") never reliably worked -- Blish's AutoSizeHeight
                // measurement doesn't land on any tick this code can read synchronously. ArranPell's call:
                // drop the cap and let it flow like every other block in this view (the name label, the
                // subpage description above it) -- this.contentPanel is already scrollable (CanScroll =
                // true), so a long note just makes the objective view taller to scroll through, the same
                // as it already does for a long subpage description.
                var notesTextLabel = this.formattedLabelHtmlService.CreateLabel(notesHtml)
                    .AutoSizeHeight()
                    .SetWidth(contentWidth)
                    .Wrap()
                    .Build();
                notesTextLabel.Parent = container;
            });
        }

        // Phase 48: a dictionary lookup (the derived file is already keyed by link) instead of a linear
        // scan over ~23,700 subpages on every objective click (DATA-INDEPENDENCE-REVIEW-2026-09-13 §4.6).
        private DerivedSubpage FindSubPage(string link)
        {
            if (string.IsNullOrEmpty(link))
            {
                return null;
            }

            var fullLink = link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? link : "https://wiki.guildwars2.com" + link;
            return this.wikiSubpageDataService.ByLink.TryGetValue(fullLink, out var subPage) ? subPage : null;
        }

        // Row-number chips -- UI-DESIGN §7's "chips row", shown in both views. Colour is never the only
        // carrier of state: confirmed/not-done differ in fill, ticked-by-you differs in border style too.
        private void BuildChipRow(int? selectedIndex)
        {
            var entries = this.GetEntries();

            if (entries.Count == 0)
            {
                return;
            }

            var chipsPanel = new FlowPanel
            {
                Parent = this.contentPanel,
                Width = (this.contentPanel.ContentRegion.Width - ContentScrollbarAllowance),
                HeightSizingMode = SizingMode.AutoSize,
                FlowDirection = ControlFlowDirection.LeftToRight,
                ControlPadding = new Vector2(3, 3),
            };

            for (var i = 0; i < entries.Count; i++)
            {
                var index = i;
                var state = this.GetChipState(index);
                var selected = selectedIndex == index;

                var ring = new Panel
                {
                    Parent = chipsPanel,
                    Width = ChipRingSize,
                    Height = ChipRingSize,
                    BackgroundColor = selected ? UiStyle.NearDone : Color.Transparent,
                };

                // Load-test 2026-09-13: (120, 200, 120) at full opacity read as neon against the window
                // body -- darkened to sit closer to the muted palette everything else here uses. The
                // ticked-by-you fill was fully transparent, relying only on ShowBorder's default (dark,
                // subtle) outline to read as "different" -- too dark to tell apart from not-done at a
                // glance. A first attempt at a "cream tint" used UiStyle.ManualDone * 0.30f, which is
                // still too dark: Color * float scales R/G/B down *and* alpha down together, so a light
                // colour darkens before it even gets translucent. FromNonPremultiplied keeps ManualDone's
                // real RGB and only touches alpha, so the tint actually reads as cream.
                var fill = state == ChipState.Confirmed ? new Color(60, 110, 65)
                    : state == ChipState.TickedByYou ? Color.FromNonPremultiplied(UiStyle.ManualDone.R, UiStyle.ManualDone.G, UiStyle.ManualDone.B, 90)
                    : (Color)UiStyle.PipTodo;

                var chip = new Panel
                {
                    Parent = ring,
                    Width = ChipSize,
                    Height = ChipSize,
                    Location = new Point((ChipRingSize - ChipSize) / 2, (ChipRingSize - ChipSize) / 2),
                    BackgroundColor = fill,
                    ShowBorder = state == ChipState.TickedByYou,
                };

                var numberLabel = new Label
                {
                    Parent = chip,
                    Text = (index + 1).ToString(),
                    Font = UiStyle.BodyFont,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Middle,
                    Width = ChipSize,
                    Height = ChipSize,
                    TextColor = state == ChipState.TickedByYou ? UiStyle.ManualDone : UiStyle.TextSecondary,
                };

                var tooltip = state == ChipState.Confirmed ? "Confirmed by the API"
                    : state == ChipState.TickedByYou ? "Ticked by you -- right-click to unmark"
                    : "Not done -- right-click to mark done";

                chip.BasicTooltipText = tooltip;
                numberLabel.BasicTooltipText = tooltip;

                // Load-test 2026-09-13: clicking the already-selected chip did nothing -- it should go
                // back to the achievement view, same as clicking the breadcrumb.
                void NavigateOrDeselect(object s, Blish_HUD.Input.MouseEventArgs e)
                {
                    if (selected)
                    {
                        this.SetAchievement(this.achievement);
                    }
                    else
                    {
                        this.SetObjective(this.achievement, index);
                    }
                }

                chip.Click += NavigateOrDeselect;
                numberLabel.Click += NavigateOrDeselect;

                if (state != ChipState.Confirmed)
                {
                    chip.RightMouseButtonReleased += (s, e) => this.ToggleAndRefresh(index);
                    numberLabel.RightMouseButtonReleased += (s, e) => this.ToggleAndRefresh(index);
                }
            }
        }

        private void ToggleAndRefresh(int index)
        {
            this.achievementService.ToggleManualCompleteStatus(this.achievement.Id, index);
            this.RebuildContent();
        }

        private enum ChipState
        {
            NotDone,
            TickedByYou,
            Confirmed,
        }

        private ChipState GetChipState(int rowIndex)
        {
            var bitIndex = this.bitAlignmentService.MapRowToBit(this.achievement.Id, rowIndex);

            if (bitIndex < 0)
            {
                return ChipState.NotDone;
            }

            if (this.achievementService.PlayerAchievementsById.TryGetValue(this.achievement.Id, out var playerAchievement)
                && (playerAchievement.Bits?.Contains(bitIndex) ?? false))
            {
                return ChipState.Confirmed;
            }

            // Not API-confirmed but HasFinishedAchievementBit says done -- the only other way that's true
            // is the manual set, since it ORs manual with the API bits (AchievementService). Reads the
            // same reconciled state without IAchievementService needing to expose the raw manual dictionary.
            return this.achievementService.HasFinishedAchievementBit(this.achievement.Id, rowIndex)
                ? ChipState.TickedByYou
                : ChipState.NotDone;
        }

        private IReadOnlyList<(string DisplayName, string Link)> GetEntries()
        {
            switch (this.achievement.Description)
            {
                case CollectionDescription collection:
                    return collection.EntryList.Select(e => (e.DisplayName, e.Link)).ToList();
                case ObjectivesDescription objectives:
                    return objectives.EntryList.Select(e => (e.DisplayName, e.Link)).ToList();
                default:
                    return System.Array.Empty<(string, string)>();
            }
        }

        // No bearing arrow: Blish 1.2.0 does expose the avatar's facing vector (GameService.Gw2Mumble.
        // PlayerCharacter.Forward, confirmed against source), but RemainingObjective/NearestObjectiveService
        // carry only a distance, not a world position to compute a bearing from -- adding one is a
        // NearestObjectiveService change, and Batch F rules those out. Distance alone still works.
        private void BuildNextLine(int? bitIndex)
        {
            if (this.achievementService.PlayerAchievements is null || !this.nearestObjectiveService.HasAnyObjectives(this.achievement.Id))
            {
                return;
            }

            var mapId = this.currentMapService.MapId;
            var player = GameService.Gw2Mumble.PlayerCharacter.Position;
            var nearest = this.nearestObjectiveService.GetRemaining(this.achievement.Id, mapId, player);

            // Phase 56 (review item 21): a wiki row the aligner couldn't resolve comes through as -1, which
            // used to match NearestObjectiveService's pooled untagged-route entry (also Bit = -1) and
            // present the route's nearest breadcrumb as this step. Unknown is unknown.
            var unresolved = bitIndex.HasValue && bitIndex.Value < 0;

            RemainingObjective target = unresolved
                ? null
                : bitIndex.HasValue
                    ? nearest.FirstOrDefault(o => o.Bit == bitIndex.Value)
                    : nearest.FirstOrDefault();

            var contentWidth = (this.contentPanel.ContentRegion.Width - ContentScrollbarAllowance);

            var text = unresolved
                ? "step not identified — this wiki row couldn't be matched to an API step"
                : target is null ? "no route on this map" : $"{target.Name} · {target.DistanceMetres:F0} m{(target.GroundDistanceOnly ? " (ground)" : string.Empty)}";

            var label = new Label
            {
                Parent = this.contentPanel,
                Text = text,
                Font = UiStyle.BodyFont,
                WrapText = true,
                AutoSizeHeight = true,
                Width = contentWidth,
            };

            UiStyle.ApplyShadow(label, target is null ? UiStyle.Rank : UiStyle.TextPrimary);
        }

        private void BuildTickToggle(int rowIndex, int bitIndex)
        {
            var state = this.GetChipState(rowIndex);

            if (state == ChipState.Confirmed)
            {
                return;
            }

            var checkbox = new Checkbox
            {
                Parent = this.contentPanel,
                Text = "Ticked by you",
                Checked = state == ChipState.TickedByYou,
                Width = (this.contentPanel.ContentRegion.Width - ContentScrollbarAllowance),
                BasicTooltipText = "Marks this done for you only, until the API confirms it or you unmark it.",
            };

            checkbox.CheckedChanged += (s, e) => this.ToggleAndRefresh(rowIndex);
        }

        private void BuildActions(string entryLink)
        {
            var actionsPanel = new FlowPanel
            {
                Parent = this.contentPanel,
                Width = (this.contentPanel.ContentRegion.Width - ContentScrollbarAllowance),
                HeightSizingMode = SizingMode.AutoSize,
                FlowDirection = ControlFlowDirection.LeftToRight,
                ControlPadding = new Vector2(4, 4),
            };

            var guidance = this.nearestObjectiveService.GetGuidance(this.achievement.Id, this.currentMapService.MapId);
            var packBacked = guidance.Tier == GuidanceTier.Tagged || guidance.Tier == GuidanceTier.Route;

            // Load-test 2026-09-13: every achievement the Inspector opens from the Target List is
            // already tracked, and HuntService.Peek is a deliberate no-op for an already-tracked
            // achievement (its routes are already on if hunt mode is -- Peek would just re-enable
            // something that's already enabled). Showing the button there produced "nothing happens" on
            // click, correctly, but with no way to tell why. Only offer it for something not tracked yet.
            var alreadyTracked = this.achievementTrackerService.IsBeingTracked(this.achievement.Id);

            if (this.huntService.CanPeek && packBacked && !alreadyTracked)
            {
                var peekButton = new StandardButton
                {
                    Parent = actionsPanel,
                    Text = "Show route in Pathing",
                    Width = 150,
                    Height = 26,
                };

                peekButton.Click += (s, e) => this.huntService.Peek(this.achievement.Id);
            }

            if (this.nearestObjectiveService.HasAnyObjectives(this.achievement.Id))
            {
                var copyWaypointButton = new StandardButton
                {
                    Parent = actionsPanel,
                    Text = "Copy waypoint",
                    Width = 110,
                    Height = 26,
                };

                copyWaypointButton.Click += (s, e) =>
                {
                    var mapId = this.currentMapService.MapId;
                    var player = GameService.Gw2Mumble.PlayerCharacter.Position;
                    var suggestion = this.nearestObjectiveService.NearestWaypoint(this.achievement.Id, mapId, player);

                    if (suggestion is null)
                    {
                        ScreenNotification.ShowNotification("No waypoint known for this map yet");
                        return;
                    }

                    _ = Blish_HUD.ClipboardUtil.WindowsClipboardService.SetTextAsync(suggestion.Code);
                    ScreenNotification.ShowNotification(suggestion.Name is null ? "Waypoint copied" : $"Copied {suggestion.Name}");
                };
            }

            var wikiLink = entryLink ?? this.achievement.Link;

            if (!string.IsNullOrEmpty(wikiLink))
            {
                var wikiButton = new StandardButton
                {
                    Parent = actionsPanel,
                    Text = "Wiki",
                    Width = 60,
                    Height = 26,
                };

                wikiButton.Click += (s, e) => _ = System.Diagnostics.Process.Start("https://wiki.guildwars2.com" + wikiLink);
            }
        }
    }
}
