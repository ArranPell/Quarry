using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Settings;
using Quarry.Interfaces;
using Quarry.Models;
using Quarry.UserInterface.Controls;
using UiStyle = Quarry.UserInterface.UiStyle;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quarry.UserInterface.Views
{
    public class HereView : View, IHereCardActions
    {
        private static readonly Logger Logger = Logger.GetLogger<HereView>();

        // Width the header row reserves at its right edge for "Target List", "Target these" and "Show hidden".
        private const int HeaderControlsWidth = 360;

        // Phase 34: how many roaming achievements the "While you're here" line names before it
        // falls back to "(+N more)"; the full list is in its tooltip.
        private const int OpportunisticNamesShown = 5;

        // A full-width line: a category name, the "Also on this map" line, the "Anywhere:" header.
        private const int SectionLabelHeight = 24;

        // "Show hidden" is a management surface, not a bounded suggestion list, so the product rule's
        // small cap doesn't apply -- but something has to stop a pathological set from building
        // thousands of cards.
        private const int HiddenListCap = 60;

        private readonly IHereService hereService;
        private readonly ICurrentMapService currentMapService;
        private readonly IAchievementCardFactory achievementCardFactory;
        private readonly IHereExclusionService hereExclusionService;
        private readonly IAchievementTrackerService achievementTrackerService;
        private readonly SettingEntry<int> hereCap;
        private readonly System.Action openTargetList;

        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private int requestSequence;

        private Label headerLabel;
        private CardGrid flowPanel;

        // Phase 31: un-hide lives in Here rather than in settings -- a Blish settings panel can't show a
        // list. Load-test 2026-09-11 made it a *mode* rather than a block appended under everything
        // else: buried at the bottom of a scrolling list it was effectively unreachable, and being
        // map-scoped it could never reach something hidden from the Anywhere block at all.
        private Checkbox showHiddenCheckbox;
        private bool showHidden;

        // Phase 32: the last rendered ranked list, kept so the "Track these (N)" label can be
        // recomputed on a track/untrack without re-running the candidates lookup.
        private IReadOnlyList<HereCandidate> lastCandidates = System.Array.Empty<HereCandidate>();
        private StandardButton trackTheseButton;
        // Shown in the header for exactly one refresh after a "Track these" click.
        private string pendingTrackFeedback;

        // Phase 33b, revised after the load-test: the Anywhere block starts collapsed every time, so
        // Here always opens on this map alone. Expanding sticks for the life of the view.
        private bool anywhereExpanded;

        public HereView(IHereService hereService, ICurrentMapService currentMapService, IAchievementCardFactory achievementCardFactory, IHereExclusionService hereExclusionService, IAchievementTrackerService achievementTrackerService, SettingEntry<int> hereCap, System.Action openTargetList)
        {
            this.hereService = hereService;
            this.currentMapService = currentMapService;
            this.achievementCardFactory = achievementCardFactory;
            this.hereExclusionService = hereExclusionService;
            this.achievementTrackerService = achievementTrackerService;
            this.hereCap = hereCap;
            this.openTargetList = openTargetList;
        }

        #region IHereCardActions

        bool IHereCardActions.IsHidden(int achievementId) => this.hereExclusionService.IsExcluded(achievementId, System.DateTime.UtcNow);

        void IHereCardActions.SnoozeUntilReset(int achievementId) => this.hereExclusionService.Snooze(achievementId);

        void IHereCardActions.HideIndefinitely(int achievementId) => this.hereExclusionService.Hide(achievementId);

        void IHereCardActions.Unhide(int achievementId) => this.hereExclusionService.Unhide(achievementId);

        string IHereCardActions.DescribeExclusion(int achievementId)
            => this.hereExclusionService.IsSnoozed(achievementId, System.DateTime.UtcNow)
                ? "Hidden until the daily reset (00:00 UTC)."
                : "Hidden until you un-hide it.";

        #endregion

        protected override void Build(Container buildPanel)
        {
            this.headerLabel = new Label()
            {
                Text = $"Loading achievements for {this.currentMapService.MapName ?? "the current map"}...",
                Parent = buildPanel,
                Font = UiStyle.HeaderFont,
                // Leaves room for the Phase 32 button and the Phase 31 toggle at the right of the row.
                Width = buildPanel.ContentRegion.Width - HeaderControlsWidth,
                Height = 30,
                WrapText = true,
            };

            UiStyle.ApplyTextPrimary(this.headerLabel);

            this.flowPanel = new CardGrid()
            {
                ShowBorder = true,
                Parent = buildPanel,
                Location = new Point(0, this.headerLabel.Height),
                // Phase 42: Fill instead of a fixed pixel Size, so the grid tracks the window's
                // ContentRegion when the user drags the Quarry window's corner (CanResize, Phase 42).
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.Fill,
                CanScroll = true,
            };

            // ArranPell, 2026-09-13: one click from Here to the Target List, matching the Achievements tab's
            // own shortcut (renamed "Tracked window" -> "Target List" there too, same load-test).
            var targetListButton = new StandardButton()
            {
                Parent = buildPanel,
                Text = "Target List",
                Width = 90,
                Height = 26,
                Location = new Point(buildPanel.ContentRegion.Width - 229, 2),
            };

            targetListButton.Click += (s, e) => this.openTargetList();

            this.trackTheseButton = new StandardButton()
            {
                Parent = buildPanel,
                Text = "Target these",
                Width = 130,
                Height = 26,
                Location = new Point(buildPanel.ContentRegion.Width - 134, 2),
                Visible = false,
            };

            this.trackTheseButton.Click += this.TrackTheseButton_Click;

            this.showHiddenCheckbox = new Checkbox()
            {
                Parent = buildPanel,
                Text = "Show hidden",
                Width = 130,
                Height = 20,
                Location = new Point(buildPanel.ContentRegion.Width - 360, 6),
                Visible = false,
            };

            this.showHiddenCheckbox.CheckedChanged += this.ShowHiddenCheckbox_CheckedChanged;

            this.achievementTrackerService.AchievementTracked += this.AchievementTracker_Changed;
            this.achievementTrackerService.AchievementUntracked += this.AchievementTracker_Changed;

            this.currentMapService.Changed += this.CurrentMapService_Changed;
            this.hereExclusionService.Changed += this.HereExclusionService_Changed;
            // Phase 56 (review item 11): a poll, a manual tick, the API categories arriving, an
            // alignment landing or the pack index finishing all change the answer; the view used to
            // keep its stale cards until the next map change.
            this.hereService.CandidatesInvalidated += this.HereService_CandidatesInvalidated;
            // Safe from the Phase 11 leak because a View is unloaded (and this unsubscribed) when the
            // tab or window goes away -- unlike a service, which outlives nothing and would be pinned.
            this.hereCap.SettingChanged += this.HereCap_SettingChanged;

            this.StartLoadCandidates();
        }

        protected override void Unload()
        {
            this.currentMapService.Changed -= this.CurrentMapService_Changed;
            this.hereExclusionService.Changed -= this.HereExclusionService_Changed;
            this.hereService.CandidatesInvalidated -= this.HereService_CandidatesInvalidated;
            this.hereCap.SettingChanged -= this.HereCap_SettingChanged;
            this.achievementTrackerService.AchievementTracked -= this.AchievementTracker_Changed;
            this.achievementTrackerService.AchievementUntracked -= this.AchievementTracker_Changed;

            if (this.trackTheseButton != null)
            {
                this.trackTheseButton.Click -= this.TrackTheseButton_Click;
            }

            if (this.showHiddenCheckbox != null)
            {
                this.showHiddenCheckbox.CheckedChanged -= this.ShowHiddenCheckbox_CheckedChanged;
            }

            this.cts.Cancel();
            this.cts.Dispose();

            base.Unload();
        }

        private void ShowHiddenCheckbox_CheckedChanged(object sender, CheckChangedEvent e)
        {
            this.showHidden = e.Checked;
            this.StartLoadCandidates();
        }

        // A hide/snooze/unhide already invalidated HereService's cache, so this is the cheap re-render
        // that makes the card disappear (or come back) immediately.
        private void HereExclusionService_Changed() => this.StartLoadCandidates();

        private void HereCap_SettingChanged(object sender, ValueChangedEventArgs<int> e) => this.StartLoadCandidates();

        // Phase 32: relabel only -- the cards themselves don't need rebuilding for someone else's
        // track/untrack, and a rebuild here would fight the one TrackTheseButton_Click already queues.
        private void AchievementTracker_Changed(int achievementId) => this.UpdateTrackTheseButton();

        // How many of the current candidates a "Track these" click would actually add: everything not
        // already tracked, capped by the free slots left under the 15-cap setting.
        private int CountTrackable()
        {
            var untracked = this.lastCandidates.Count(c => !this.achievementTrackerService.IsBeingTracked(c.Achievement.Id));
            return System.Math.Min(untracked, this.achievementTrackerService.FreeSlots);
        }

        private void UpdateTrackTheseButton()
        {
            if (this.trackTheseButton is null)
            {
                return;
            }

            if (this.lastCandidates.Count == 0)
            {
                this.trackTheseButton.Visible = false;
                return;
            }

            var trackable = this.CountTrackable();
            var full = this.achievementTrackerService.FreeSlots == 0;

            this.trackTheseButton.Visible = true;
            this.trackTheseButton.Enabled = trackable > 0;
            this.trackTheseButton.Text = full ? "Target list full" : $"Target these ({trackable})";
            this.trackTheseButton.BasicTooltipText = full
                ? "Untrack something to make room."
                : "Track everything listed here that isn't tracked yet. Nothing is ever untracked.";
        }

        // Counts every hidden and snoozed achievement account-wide, not just this map's -- otherwise
        // something hidden from the Anywhere block has no toggle to bring it back.
        private void UpdateShowHiddenCheckbox()
        {
            if (this.showHiddenCheckbox is null)
            {
                return;
            }

            var total = this.hereExclusionService.TotalExcludedCount;

            this.showHiddenCheckbox.Visible = total > 0 || this.showHidden;
            this.showHiddenCheckbox.Text = $"Show hidden ({total})";

            if (total == 0 && this.showHiddenCheckbox.Checked)
            {
                this.showHiddenCheckbox.Checked = false;
            }
        }

        // Fill the free slots, top-down, never untracking anything -- cross-map tracking set up on
        // purpose survives. Hidden and snoozed achievements are already out of Candidates (Phase 31).
        private void TrackTheseButton_Click(object sender, Blish_HUD.Input.MouseEventArgs e)
        {
            var tracked = 0;
            var hitCap = false;

            foreach (var candidate in this.lastCandidates)
            {
                if (this.achievementTrackerService.IsBeingTracked(candidate.Achievement.Id))
                {
                    continue;
                }

                if (!this.achievementTrackerService.TrackAchievement(candidate.Achievement.Id))
                {
                    hitCap = true;
                    break;
                }

                tracked++;
            }

            this.pendingTrackFeedback = hitCap ? $"Tracked {tracked} · list full" : $"Tracked {tracked}";

            // The card's track icon is set once at build from IsBeingTracked and AchievementCard only
            // listens for AchievementUntracked -- so a rebuild is what refreshes the icons. It's cheap:
            // HereService returns the same cached result.
            this.StartLoadCandidates();
        }

        private void CurrentMapService_Changed() => this.StartLoadCandidates();

        private void HereService_CandidatesInvalidated() => this.StartLoadCandidates();

        private void StartLoadCandidates()
        {
            var requestId = Interlocked.Increment(ref this.requestSequence);
            var token = this.cts.Token;
            _ = Task.Run(() => this.LoadCandidatesAsync(requestId, token), token);
        }

        private async Task LoadCandidatesAsync(int requestId, CancellationToken cancellationToken)
        {
            var mapIdAtRequest = this.currentMapService.MapId;
            IReadOnlyList<HereCandidate> hidden;
            HereResult result;
            IReadOnlyList<HereCandidate> anywhere;

            try
            {
                // Only fetch what this pass will actually render: the hidden mode replaces the live list
                // outright, and a collapsed Anywhere block doesn't need its (account-wide) lookup at all.
                hidden = this.showHidden
                    ? await this.hereService.GetHiddenAsync(HiddenListCap)
                    : System.Array.Empty<HereCandidate>();

                result = this.showHidden ? null : await this.hereService.GetCandidatesAsync(this.hereCap.Value);

                anywhere = this.showHidden || !this.anywhereExpanded
                    ? System.Array.Empty<HereCandidate>()
                    : await this.hereService.GetNearlyDoneAnywhereAsync(this.hereCap.Value);
            }
            catch (System.OperationCanceledException)
            {
                return;
            }
            catch (System.Exception ex)
            {
                // Phase 56 (review item 17): this ran on a bare Task.Run, so a throw was an unobserved
                // task exception -- nothing logged, and the header sat on "Loading..." until the next map
                // change. Say so instead.
                Logger.Warn(ex, "Here: the candidates lookup failed.");

                GameService.Overlay.QueueMainThreadUpdate(gameTime =>
                {
                    if (!this.IsStale(requestId, mapIdAtRequest, cancellationToken))
                    {
                        this.headerLabel.Text = $"{this.currentMapService.MapName ?? "This map"}: couldn't load achievements — try again shortly.";
                    }
                });

                return;
            }

            // The view was unloaded, a newer request superseded this one, or the map changed again while
            // the lookup was in flight -- a fresher request will render instead.
            if (this.IsStale(requestId, mapIdAtRequest, cancellationToken))
            {
                return;
            }

            // Phase 56 (review item 1): everything from here down touches controls, and this method runs
            // on a thread-pool thread (StartLoadCandidates is a Task.Run and Blish has no
            // SynchronizationContext, so the awaits above resume on the pool). Hop to the main thread and
            // re-check staleness there, since another request can land in between.
            GameService.Overlay.QueueMainThreadUpdate(gameTime =>
            {
                if (this.IsStale(requestId, mapIdAtRequest, cancellationToken))
                {
                    return;
                }

                this.UpdateShowHiddenCheckbox();

                this.flowPanel.ClearItems();

                if (this.showHidden)
                {
                    this.RenderHiddenMode(hidden);
                    return;
                }

                this.RenderLiveList(result, anywhere);
            });
        }

        private bool IsStale(int requestId, int mapIdAtRequest, CancellationToken cancellationToken)
            => cancellationToken.IsCancellationRequested
                || requestId != this.requestSequence
                || this.currentMapService.MapId != mapIdAtRequest;

        // The whole panel, not a block under the live list: hidden things are reachable at the top with
        // no scrolling, and the set is account-wide so an Anywhere-hidden achievement can be recovered.
        private void RenderHiddenMode(IReadOnlyList<HereCandidate> hidden)
        {
            this.lastCandidates = System.Array.Empty<HereCandidate>();
            this.UpdateTrackTheseButton();

            this.headerLabel.Text = hidden.Count == 0
                ? "Nothing hidden."
                : $"Hidden: {hidden.Count} achievement(s), anywhere. Unhide from a card's + menu.";

            foreach (var candidate in hidden)
            {
                _ = this.CreateCard(candidate);
            }
        }

        private void RenderLiveList(HereResult result, IReadOnlyList<HereCandidate> anywhere)
        {
            var mapName = this.currentMapService.MapName ?? "This map";

            if (result.Reason == HereResultReason.NoCategoryForMap)
            {
                this.headerLabel.Text = result.IndexReady
                    ? $"{mapName}: only guided achievements shown here."
                    : $"{mapName}: Core Tyria not supported yet.";
            }
            else if (result.Reason == HereResultReason.NoPermission)
            {
                this.headerLabel.Text = $"{mapName}: achievement permissions not granted for this API key.";
            }
            else if (result.Reason == HereResultReason.NotLoaded)
            {
                this.headerLabel.Text = $"{mapName}: still loading achievement data, try again shortly.";
            }
            else if (result.Candidates.Count == 0)
            {
                // Phase 27: an empty list because of the guidance threshold is a different fact from an
                // empty map, and the product rule says say which rather than showing nothing.
                if (result.FilteredByGuidance > 0)
                {
                    this.headerLabel.Text = $"{mapName}: {result.FilteredByGuidance} achievement(s) here, none guided well enough for your filter.";
                }
                else
                {
                    this.headerLabel.Text = result.Partial
                        ? $"{mapName}: some achievements couldn't be fetched — try again shortly."
                        : $"{mapName}: nothing nearly complete here right now.";
                }
            }
            else if (!result.CategorySupported)
            {
                this.headerLabel.Text = result.Partial
                    ? $"{mapName}: {result.Candidates.Count} guided achievement(s) here (some couldn't be fetched — try again shortly)."
                    : $"{mapName}: {result.Candidates.Count} guided achievement(s) here.";
            }
            else
            {
                this.headerLabel.Text = result.Partial
                    ? $"{mapName}: {result.Candidates.Count} achievement(s) close to done (some couldn't be fetched — try again shortly)."
                    : $"{mapName}: {result.Candidates.Count} achievement(s) close to done.";
            }

            // Phase 31: say what was withheld rather than just showing less. Per-map, unlike the
            // "Show hidden (N)" toggle, which counts everything hidden anywhere.
            if (result.HiddenCount > 0)
            {
                this.headerLabel.Text += $" · {result.HiddenCount} hidden here";
            }

            // Phase 32: one refresh's worth of feedback for the last "Track these" click. No toast --
            // the Track window opening/growing is the real feedback.
            if (this.pendingTrackFeedback != null)
            {
                this.headerLabel.Text += $" · {this.pendingTrackFeedback}";
                this.pendingTrackFeedback = null;
            }

            this.lastCandidates = result.Reason == HereResultReason.Ok ? result.Candidates : System.Array.Empty<HereCandidate>();
            this.UpdateTrackTheseButton();

            // Phase 34, moved after the load-test: the roaming line reads as a note on this map, so it
            // belongs above the cards rather than as a footnote under them.
            if (result.Opportunistic.Count > 0)
            {
                this.RenderOpportunisticLine(result.Opportunistic);
            }

            // GroupBy yields groups in first-encounter order, and Candidates is already ranked best-first,
            // so each group's first entry is that category's best candidate -- grouping (rather than just
            // detecting a category change in the flat order) also collapses a category's candidates into
            // one contiguous run even if a better-ranked entry from another category sits between them.
            var categoryGroups = result.Candidates.GroupBy(c => c.Category.Id).ToList();
            var showCategoryHeaders = categoryGroups.Count > 1;

            // Phase 40: the card's rank numeral is the candidate's position in this ranked list --
            // computed here, not per-category, since GroupBy only reorders which achievements sit next to
            // each other, not how well-ranked each one is.
            var rankById = result.Candidates
                .Select((c, index) => (c.Achievement.Id, Rank: index + 1))
                .ToDictionary(x => x.Id, x => x.Rank);

            foreach (var group in categoryGroups)
            {
                if (showCategoryHeaders)
                {
                    _ = this.CreateSectionLabel(group.First().Category.Name);
                }

                foreach (var candidate in group)
                {
                    _ = this.CreateCard(candidate, rankById[candidate.Achievement.Id]);
                }
            }

            if (result.Reason == HereResultReason.Ok || this.anywhereExpanded)
            {
                this.RenderAnywhereBlock(anywhere);
            }
        }

        // Phase 34: one line, never a card. These complete while you do other things, so they get a
        // mention and nothing more -- the "show me where the enemies are" half is deliberately not
        // promised (11 % coordinate coverage, measured).
        private void RenderOpportunisticLine(IReadOnlyList<HereCandidate> opportunistic)
        {
            var named = opportunistic.Take(OpportunisticNamesShown).Select(c => c.Achievement.Name.Trim()).ToList();
            var text = $"Also on this map, no route known: {string.Join(", ", named)}";

            if (opportunistic.Count > named.Count)
            {
                text += $" (+{opportunistic.Count - named.Count} more)";
            }

            var label = this.CreateSectionLabel(StringUtils.TrimNameToWidth(text, this.flowPanel.ContentRegion.Width - 16));

            label.Font = UiStyle.BodyFont;
            UiStyle.ApplyShadow(label, UiStyle.TextSecondary);
            label.BasicTooltipText = "On this map, but nothing can place them — no checklist steps to tick off, and no marker-pack route or wiki location. Still trackable from the All tab.\n\n"
                + string.Join("\n", opportunistic.Select(c => $"{c.Achievement.Name.Trim()}  {c.Current}/{c.Max}"));

        }

        // Phase 38: CardGrid understands a section row, so this no longer has to be full-width purely
        // to force a row break -- the grid starts a new row for it and sizes it to the grid's width.
        private Label CreateSectionLabel(string text)
        {
            var label = new Label()
            {
                Text = text,
                Font = UiStyle.SectionFont,
                Height = SectionLabelHeight,
            };

            UiStyle.ApplyShadow(label, UiStyle.TextSecondary);

            this.flowPanel.Add(label, fullWidth: true);
            return label;
        }

        // Collapsed by default every time the view is built, so Here always opens on this map alone
        // (settled with ArranPell after the 2026-09-11 load-test). Expanding sticks for the life of the view.
        private void RenderAnywhereBlock(IReadOnlyList<HereCandidate> anywhere)
        {
            var header = this.CreateSectionLabel(this.anywhereExpanded
                ? "Anywhere: closest to done   (click to hide)"
                : "Anywhere: closest to done   (click to show)");

            header.BasicTooltipText = "Closest to done across your whole account, wherever they are. Click to show or hide.";

            header.Click += (s, e) =>
            {
                this.anywhereExpanded = !this.anywhereExpanded;
                this.StartLoadCandidates();
            };


            foreach (var candidate in anywhere)
            {
                _ = this.CreateCard(candidate);
            }
        }

        private AchievementCard CreateCard(HereCandidate candidate, int? rank = null)
        {
            var card = this.achievementCardFactory.Create(candidate.Achievement, candidate.Category.Icon, candidate.Guidance, this, rank);
            card.Size = this.flowPanel.CardSize;
            this.flowPanel.Add(card);
            return card;
        }
    }
}
