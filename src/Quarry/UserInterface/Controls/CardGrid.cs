using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quarry.UserInterface.Controls
{
    /// <summary>
    /// Phase 38: the card list's own layout, replacing <see cref="FlowPanel"/> in Here and the
    /// Achievements category view.
    ///
    /// Why not FlowPanel: its <c>ReflowChildLayoutLeftToRight</c> wraps a child when
    /// <c>child.Width &gt;= this.Width - lastRight</c> — note the <c>&gt;=</c> — so two cards of exactly
    /// half the panel's width wrap to *one per row*, which is what made the 821 px window look half
    /// empty. It also has no notion of a full-width row, so section headings were faked by handing a
    /// Label the panel's whole width purely to force a break. Owning the layout makes both first-class
    /// and gives us a real content height for Phase 40.
    ///
    /// Items keep their insertion order; cards flow into columns and section rows always start a new
    /// row and span the width.
    /// </summary>
    public class CardGrid : Panel
    {
        // Phase 44: folded in from the retired CardLayout -- CardGrid was already its only real caller.

        // Phase 40: 380 gives the place row ("Arborstone · 172 m") the width it needs; two columns from
        // ~770 px usable, which the 900 px minimum window always has.
        public const int MinCardWidth = 380;

        // Phase 40: AchievementCard's fixed height (UI-DESIGN §3) -- three ~20px lines plus 8px padding.
        public const int CardHeight = 92;

        // Breathing room between cards and between rows.
        public const int Gutter = 8;

        // Panel parents its scrollbar to the panel's *parent* and straddles the right edge
        // (Scrollbar.Right = panel.Right - Width / 2), so roughly half of it sits over the content.
        public const int ScrollbarAllowance = 12;

        // Smallest card we'll produce rather than returning something unusable on a very narrow panel.
        private const int AbsoluteMinCardWidth = 120;

        /// <param name="usableWidth">Row width already net of the scrollbar and outer padding.</param>
        private static int ColumnsFor(int usableWidth)
            => Math.Max(1, (usableWidth + Gutter) / (MinCardWidth + Gutter));

        /// <param name="usableWidth">Row width already net of the scrollbar and outer padding.</param>
        public static int CardWidth(int usableWidth, int columns)
        {
            if (columns <= 0)
            {
                return AbsoluteMinCardWidth;
            }

            return Math.Max(AbsoluteMinCardWidth, (usableWidth - ((columns - 1) * Gutter)) / columns);
        }

        private readonly List<Item> items = new List<Item>();

        private readonly struct Item
        {
            public Item(Control control, bool fullWidth)
            {
                this.Control = control;
                this.FullWidth = fullWidth;
            }

            public Control Control { get; }

            public bool FullWidth { get; }
        }

        /// <summary>Height the laid-out items actually occupy. Phase 40 sizes the window from this.</summary>
        public int ContentHeight { get; private set; }

        /// <summary>Columns the current width supports. 1 when the panel is too narrow for two.</summary>
        public int Columns => ColumnsFor(this.UsableWidth);

        /// <summary>Size every card in this grid gets. Callers size their card control from this.</summary>
        public Point CardSize => new Point(CardWidth(this.UsableWidth, this.Columns), CardHeight);

        // The panel's scrollbar is parented to the panel's *parent* and straddles the right edge
        // (Scrollbar.Right = panel.Right - Width / 2), so about half of it sits over our content.
        private int UsableWidth => this.ContentRegion.Width - ScrollbarAllowance - Gutter;

        /// <param name="fullWidth">
        /// A section row — a category name, the "Also on this map" line, the "Anywhere:" header. Starts
        /// a new row, spans the grid, and keeps whatever Height the caller set.
        /// </param>
        public void Add(Control control, bool fullWidth = false)
        {
            control.Parent = this;
            this.items.Add(new Item(control, fullWidth));
            this.Invalidate();
        }

        public void ClearItems()
        {
            foreach (var item in this.items)
            {
                item.Control.Dispose();
            }

            this.items.Clear();
            this.ContentHeight = 0;
            this.Invalidate();
        }

        public override void RecalculateLayout()
        {
            // Panel's own pass sets ContentRegion (and the border/accent bounds) -- everything below
            // reads it, so it has to run first.
            base.RecalculateLayout();

            this.LayoutItems();
        }

        private void LayoutItems()
        {
            if (this.items.Count == 0)
            {
                this.ContentHeight = 0;
                return;
            }

            var gutter = Gutter;
            var outer = gutter / 2;
            var usable = this.UsableWidth;

            if (usable <= 0)
            {
                return;
            }

            var columns = ColumnsFor(usable);
            var cardWidth = CardWidth(usable, columns);

            var x = this.ContentRegion.X + outer;
            var y = this.ContentRegion.Y + outer;
            var column = 0;

            // Fatal crash, 2026-09-14: "Collection was modified" -- Fill-sized CardGrids get
            // RecalculateLayout() re-entered off the ordinary update/layout cascade (Control.set_Size ->
            // OnPropertyChanged -> UpdateLayout, nested several Containers deep), and a track/untrack
            // click's Populate() (ClearItems + re-Add) can land in that same window. A snapshot makes
            // this loop immune to a concurrent mutation of the live list; it isn't a fix for whatever
            // triggers the re-entrant repopulate in the first place, which would need a debugger to
            // pin down precisely, the same as the still-open resize mystery.
            foreach (var item in this.items.ToList())
            {
                if (item.FullWidth)
                {
                    // Close the current card row before the section starts.
                    if (column > 0)
                    {
                        y += CardHeight + gutter;
                        column = 0;
                    }

                    item.Control.Location = new Point(x, y);
                    item.Control.Width = usable;
                    y += item.Control.Height + gutter;
                    continue;
                }

                if (column == columns)
                {
                    column = 0;
                    y += CardHeight + gutter;
                }

                item.Control.Location = new Point(x + (column * (cardWidth + gutter)), y);
                item.Control.Size = new Point(cardWidth, CardHeight);
                column++;
            }

            if (column > 0)
            {
                y += CardHeight + gutter;
            }

            this.ContentHeight = y - this.ContentRegion.Y + outer;
        }
    }
}
