using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Quarry.UserInterface
{
    // Phase 41. Both windows are now built on a transparent ref/window_blank.png (ConstructWindow only
    // reads its dimensions -- the stretched draw of the old background.png/asset-156006 texture is
    // invisible), so this is the only thing that actually paints their content area. Called from
    // PaintBeforeChildren, before base.PaintBeforeChildren -- verified against Blish v1.3.0 source:
    // WindowBase2.PaintBeforeChildren paints the (now-blank) background, then the sidebar, then the
    // title bar, so calling this first and the base second lands the native sidebar fade and title bar
    // on top of our body, in the same order the game's own window draws in.
    public static class WindowBodyPainter
    {
        private const int TopFadeHeight = 46;

        private static AsyncTexture2D leftAccent;

        // Phase 41: the overview window's existing left-side accent. NOT confirmed to be a repo asset
        // (no ref/*.png or in-code reference to id 605025 predates this phase) -- fetched the same way
        // asset 156006 already is elsewhere in this codebase, and skipped silently if the id doesn't
        // resolve to anything. Flag at the load-test: if it doesn't show, drop this rather than chase it.
        private static AsyncTexture2D LeftAccent => leftAccent ?? (leftAccent = AsyncTexture2D.FromAssetId(605025));

        public static void PaintBody(SpriteBatch spriteBatch, Control ctrl, Rectangle contentRegion, bool showLeftAccent)
        {
            spriteBatch.DrawOnCtrl(ctrl, ContentService.Textures.Pixel, contentRegion, UiStyle.WindowBody);

            // 1px hairline around the body, and a lighter one right under where the title bar sits, so the
            // native-to-flat transition reads as one piece rather than a seam.
            spriteBatch.DrawOnCtrl(ctrl, ContentService.Textures.Pixel, new Rectangle(contentRegion.X, contentRegion.Y, contentRegion.Width, 1), UiStyle.Accent * 0.25f);
            spriteBatch.DrawOnCtrl(ctrl, ContentService.Textures.Pixel, new Rectangle(contentRegion.X, contentRegion.Y, 1, contentRegion.Height), UiStyle.CardBorder);
            spriteBatch.DrawOnCtrl(ctrl, ContentService.Textures.Pixel, new Rectangle(contentRegion.Right - 1, contentRegion.Y, 1, contentRegion.Height), UiStyle.CardBorder);
            spriteBatch.DrawOnCtrl(ctrl, ContentService.Textures.Pixel, new Rectangle(contentRegion.X, contentRegion.Bottom - 1, contentRegion.Width, 1), UiStyle.CardBorder);

            // The sidebar already fades in exactly this way -- reusing the same texture keeps the join
            // between the native title bar and our body looking like it was drawn by the same hand.
            var fade = GameService.Content.GetTexture("fade-down-46");

            if (fade != null)
            {
                spriteBatch.DrawOnCtrl(ctrl, fade, new Rectangle(contentRegion.X, contentRegion.Y, contentRegion.Width, TopFadeHeight));
            }

            if (showLeftAccent && LeftAccent != null && LeftAccent.HasTexture)
            {
                spriteBatch.DrawOnCtrl(ctrl, LeftAccent, new Rectangle(contentRegion.X, contentRegion.Y, LeftAccent.Texture.Width, contentRegion.Height));
            }
        }
    }
}
