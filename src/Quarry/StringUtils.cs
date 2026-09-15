namespace Quarry
{
    public static class StringUtils
    {
        // Blish's Label (and DetailsButton's title) wraps rather than ellipsizing on overflow, so trim
        // character-by-character -- measuring with the same font it draws with -- until "name..." fits
        // maxWidth. Phase 33d moved this out of AchievementTrackWindow so the card can use it too.
        public static string TrimNameToWidth(string name, int maxWidth)
            => TrimNameToWidth(name, maxWidth, Blish_HUD.GameService.Content.DefaultFont14);

        // Phase 40: AchievementCard's title measures against the font it actually draws with
        // (UiStyle.TitleFont, 16px) rather than the 14px default every other caller still uses.
        public static string TrimNameToWidth(string name, int maxWidth, MonoGame.Extended.BitmapFonts.BitmapFont font)
        {
            if (font.MeasureString(name).Width <= maxWidth)
            {
                return name;
            }

            for (var length = name.Length - 1; length > 0; length--)
            {
                var candidate = name.Substring(0, length) + "…";
                if (font.MeasureString(candidate).Width <= maxWidth)
                {
                    return candidate;
                }
            }

            return "…";
        }
    }
}
