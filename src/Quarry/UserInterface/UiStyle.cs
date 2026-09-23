using Quarry.Models;
using Blish_HUD;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;

namespace Quarry.UserInterface
{
    // Phase 39. UI-DESIGN.md §2's token table -- one place for every font role and colour so nothing in
    // UserInterface/ carries a literal after this batch. GuidanceStyle (Models/Guidance.cs) stays where
    // it is -- Guidance.cs itself reads it internally -- but is exposed here too (Guidance.*) so new code
    // never has to choose between two places to look.
    public static class UiStyle
    {
        private static BitmapFont numeralFont;
        private static BitmapFont sectionFont;

        public static BitmapFont TitleFont => GameService.Content.DefaultFont16;

        // Bold exists at 11-18, 20, 22, 24, 36 in Blish 1.3.0 (verified from the shipped font files,
        // 2026-09-12) -- 18 is the numeral size UI-DESIGN asks for.
        public static BitmapFont NumeralFont => numeralFont ?? (numeralFont = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size18, ContentService.FontStyle.Bold));

        // The floor -- nothing in UserInterface/ goes smaller than this after Phase 39.
        public static BitmapFont BodyFont => GameService.Content.DefaultFont14;

        public static BitmapFont HeaderFont => GameService.Content.DefaultFont18;

        public static BitmapFont SectionFont => sectionFont ?? (sectionFont = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size14, ContentService.FontStyle.Bold));

        public static readonly Color TextPrimary = Color.White;

        public static readonly Color ShadowColor = Color.Black * 0.8f;

        public static readonly Color TextSecondary = new Color(200, 200, 200);

        public static readonly Color TextMuted = new Color(150, 150, 150);

        public static readonly Color Accent = ContentService.Colors.ColonialWhite;

        public static readonly Color ProgressFill = new Color(120, 170, 220);

        public static readonly Color NearDone = new Color(212, 175, 55);

        public static readonly Color Complete = new Color(120, 200, 120) * 0.35f;

        public static readonly Color CardBackground = Color.Black * 0.40f;

        public static readonly Color CardBackgroundHover = Color.Black * 0.28f;

        public static readonly Color CardBorder = new Color(238, 233, 217) * 0.12f;

        public static readonly Color WindowBody = new Color(12, 11, 9) * 0.84f;

        public static readonly Color Rank = new Color(143, 138, 124);

        public static readonly Color PipTodo = new Color(238, 233, 217) * 0.16f;

        public static readonly Color ManualDone = new Color(238, 233, 217);

        public const int Gutter = 8;

        public const int CardPadding = 8;

        // Applies the shadow rule to a label in one call instead of three property sets at every
        // call site. Shadow, never stroke, below 20 px (UI-DESIGN §2).
        public static void ApplyTextPrimary(Blish_HUD.Controls.Label label)
        {
            label.TextColor = TextPrimary;
            label.ShowShadow = true;
            label.ShadowColor = ShadowColor;
        }

        // Same shadow rule for colours other than TextPrimary (the guidance badge, the "no route on
        // this map" line) -- only the fill colour differs.
        public static void ApplyShadow(Blish_HUD.Controls.Label label, Color textColor)
        {
            label.TextColor = textColor;
            label.ShowShadow = true;
            label.ShadowColor = ShadowColor;
        }

        // The GuidanceStyle colours, exposed under UiStyle too so this is the one place to look --
        // Models/Guidance.cs keeps owning the values since GuidanceInfo.Color reads them internally.
        public static class Guidance
        {
            public static Color Tagged => GuidanceStyle.Tagged;

            public static Color Coordinate => GuidanceStyle.Coordinate;

            public static Color Route => GuidanceStyle.Route;

            public static Color Area => GuidanceStyle.Area;
        }
    }
}
