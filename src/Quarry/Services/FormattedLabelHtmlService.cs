using Blish_HUD.Controls;
using Blish_HUD.Modules.Managers;
using Quarry.Interfaces;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quarry.Services
{
    public class FormattedLabelHtmlService : IFormattedLabelHtmlService
    {
        // Ported from the wiki's own client-side chat-link encoder (the script embedded next to a
        // "gamelink" placeholder span -- see the span-handling branch below). data-type/data-id are real
        // in-game ids, the same ones GW2's own chat-link feature uses, not a wiki invention -- decoding
        // locally needs no API call and works for any map regardless of which one the player is on.
        private static readonly Dictionary<string, int> GameLinkTypeBytes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["item"] = 2,
            ["text"] = 3,
            ["map"] = 4,
            ["skill"] = 6,
            ["trait"] = 7,
            ["recipe"] = 9,
            ["skin"] = 10,
            ["outfit"] = 11,
        };

        private readonly IExternalImageService externalImageService;
        private readonly ContentsManager contentsManager;

        public FormattedLabelHtmlService(ContentsManager contentsManager, IExternalImageService externalImageService)
        {
            this.contentsManager = contentsManager;
            this.externalImageService = externalImageService;
        }

        public FormattedLabelBuilder CreateLabel(string textWithHtml)
        {
            var labelBuilder = new FormattedLabelBuilder();

            var node = HtmlNode.CreateNode("<div>" + textWithHtml + "</div>");

            MergeGameLinksIntoPrecedingAnchors(node);

            foreach (var childNode in node.ChildNodes)
            {
                foreach (var item in this.CreateParts(childNode, labelBuilder))
                {
                    _ = labelBuilder.CreatePart(item);
                }
            }

            return labelBuilder;
        }

        // The wiki's "insert a chat link" template writes two things for one concept: "<a>place name</a>
        // <dash> <span class="gamelink"></span><script>...decodes the span client-side...</script>". Left
        // alone that renders as a wiki-page link followed by a separate raw chat code (load-test
        // 2026-09-14). Folding them together reads better and is what was actually asked for: the anchor
        // keeps its text/icon but copies the code on click instead of opening the wiki page, and the
        // dash/span/script are removed rather than left dangling. Falls back to leaving the gamelink span
        // in place (Content-Parts still renders it standalone, click-to-copy) if no preceding anchor is
        // found through only dash/blank separators -- the same behaviour as before this merge.
        private static void MergeGameLinksIntoPrecedingAnchors(HtmlNode root)
        {
            var gameLinkSpans = root.SelectNodes(".//span[@class='gamelink']");

            if (gameLinkSpans == null)
            {
                return;
            }

            foreach (var gameLinkSpan in gameLinkSpans.ToList())
            {
                var chatLink = DecodeGameLink(gameLinkSpan.GetAttributeValue("data-type", null), gameLinkSpan.GetAttributeValue("data-id", null));

                if (chatLink == null)
                {
                    continue;
                }

                var separators = new List<HtmlNode>();
                var cursor = gameLinkSpan.PreviousSibling;

                while (cursor != null && cursor.Name != "a")
                {
                    var isBareSpan = cursor.Name == "span" && !cursor.GetClasses().Any();
                    var isDashOrBlank = (cursor.Name == "#text" || isBareSpan) && IsDashOrBlank(cursor.InnerText);

                    if (!isDashOrBlank)
                    {
                        cursor = null;
                        break;
                    }

                    separators.Add(cursor);
                    cursor = cursor.PreviousSibling;
                }

                if (cursor == null || cursor.Name != "a")
                {
                    continue;
                }

                cursor.SetAttributeValue("data-copy-chatlink", chatLink);

                foreach (var separator in separators)
                {
                    separator.Remove();
                }

                var script = gameLinkSpan.NextSibling;
                gameLinkSpan.Remove();

                if (script != null && script.Name == "script")
                {
                    script.Remove();
                }
            }
        }

        private static bool IsDashOrBlank(string text)
        {
            var trimmed = System.Net.WebUtility.HtmlDecode(text)?.Trim();
            return string.IsNullOrEmpty(trimmed) || trimmed == "—" || trimmed == "-";
        }

        private IEnumerable<FormattedLabelPartBuilder> CreateParts(HtmlNode childNode, FormattedLabelBuilder labelBuilder)
        {
            if (childNode.Name == "#text")
            {
                // A numeric entity like the wiki's "&#160;" (a hard space before a place name, common in
                // the achievement_tables.json Notes column, Phase 47) can arrive undecoded from
                // HtmlNode.CreateNode's fragment parsing -- InnerText alone isn't reliably decoding it,
                // so force it explicitly rather than show the literal entity text in the UI.
                yield return labelBuilder.CreatePart(System.Net.WebUtility.HtmlDecode(childNode.InnerText));
            }
            else if (childNode.Name == "a")
            {
                // TODO: Check for more
                if (!childNode.GetClasses().Contains("mw-selflink"))
                {
                    var copyChatLink = childNode.GetAttributeValue("data-copy-chatlink", null);
                    var isFirstPart = true;

                    foreach (var innerChildNode in childNode.ChildNodes)
                    {
                        foreach (var part in this.CreateParts(innerChildNode, labelBuilder))
                        {
                            if (!string.IsNullOrEmpty(copyChatLink))
                            {
                                // MergeGameLinksIntoPrecedingAnchors folded a trailing wiki chat-link
                                // placeholder into this anchor -- copy the real in-game code on click
                                // instead of opening the wiki page for it.
                                yield return part.SetLink(() => { _ = Blish_HUD.ClipboardUtil.WindowsClipboardService.SetTextAsync(copyChatLink); }).MakeUnderlined();
                                continue;
                            }

                            // Phase 48, ArranPell's call: every wiki link opens in the browser now --
                            // SubPageInformationWindow retired with the 70 MB subPages.json it needed to
                            // decide "is this an in-app page".
                            var link = childNode.GetAttributeValue("href", "");

                            if (link.StartsWith("/"))
                            {
                                link = "https://wiki.guildwars2.com" + link;
                            }

                            var hyperlinkPart = part.SetHyperLink(link).MakeUnderlined();

                            if (isFirstPart)
                            {
                                // ArranPell, 2026-09-14: a browser-opening link should look different from the
                                // in-game copy-to-clipboard ones (the merged gamelink case above) so it's
                                // not a surprise when clicking it leaves the game. Once per anchor, not
                                // once per fragment, for an anchor split across several inline nodes.
                                _ = hyperlinkPart.SetPrefixImage(this.contentsManager.GetTexture("wiki.png")).SetPrefixImageSize(new Microsoft.Xna.Framework.Point(16, 16));
                                isFirstPart = false;
                            }

                            yield return hyperlinkPart;
                        }
                    }
                }
                else
                {
                    foreach (var innerChildNode in childNode.ChildNodes)
                    {
                        foreach (var part in this.CreateParts(innerChildNode, labelBuilder))
                        {
                            yield return part;
                        }
                    }
                }
            }
            else if (childNode.Name == "span")
            {
                // TODO: Check for more
                if (childNode.GetClasses().Contains("inline-icon"))
                {
                    var imageNode = childNode.ChildNodes.FindFirst("img");

                    if (imageNode != null)
                    {
                        var builder = labelBuilder.CreatePart("");
                        _ = builder.SetPrefixImage(this.externalImageService.GetImage(imageNode.GetAttributeValue("src", ""))).SetPrefixImageSize(new Microsoft.Xna.Framework.Point(24, 24));
                        yield return builder;
                    }
                }
                else if (childNode.GetClasses().Contains("gamelink"))
                {
                    // The wiki leaves this span empty and fills it in with the <script> right after it
                    // (dropped separately, see the script/style branch) -- decode the real chat link
                    // instead of rendering nothing, so surrounding prose that leads into it ("... near
                    // the waypoint —") doesn't dangle, and the player gets something pasteable in-game.
                    var chatLink = DecodeGameLink(childNode.GetAttributeValue("data-type", null), childNode.GetAttributeValue("data-id", null));

                    if (chatLink != null)
                    {
                        var builder = labelBuilder.CreatePart(chatLink);
                        yield return builder.SetLink(() => { _ = Blish_HUD.ClipboardUtil.WindowsClipboardService.SetTextAsync(chatLink); }).MakeUnderlined();
                    }
                }
                else
                {
                    foreach (var innerChildNode in childNode.ChildNodes)
                    {
                        foreach (var part in this.CreateParts(innerChildNode, labelBuilder))
                        {
                            yield return part;
                        }
                    }
                }
            }
            else if (childNode.Name == "b")
            {
                // TODO: Make it bold when merged with core
                foreach (var innerChildNode in childNode.ChildNodes)
                {
                    foreach (var part in this.CreateParts(innerChildNode, labelBuilder))
                    {
                        yield return part;
                    }
                }
            }
            else if (childNode.Name == "i")
            {
                foreach (var innerChildNode in childNode.ChildNodes)
                {
                    foreach (var part in this.CreateParts(innerChildNode, labelBuilder))
                    {
                        yield return part.MakeItalic();
                    }
                }
            }
            else if (childNode.Name == "sup")
            {
                // TODO: Ignore for now
                yield return labelBuilder.CreatePart(string.Empty);
            }
            else if (childNode.Name == "h3")
            {
                foreach (var innerChildNode in childNode.ChildNodes)
                {
                    foreach (var part in this.CreateParts(innerChildNode, labelBuilder))
                    {
                        yield return part;
                    }
                }
            }
            else if (childNode.Name == "style" || childNode.Name == "script")
            {
                // TODO: Ignore for now
                yield return labelBuilder.CreatePart(string.Empty);
            }
            else if (childNode.Name == "p")
            {
                foreach (var innerChildNode in childNode.ChildNodes)
                {
                    foreach (var part in this.CreateParts(innerChildNode, labelBuilder))
                    {
                        yield return part;
                    }
                }
            }
            else if (childNode.Name == "br")
            {
                yield return labelBuilder.CreatePart("\n");
            }
            else if (childNode.Name == "small")
            {
                // TODO: Make it small when merged with core
                foreach (var innerChildNode in childNode.ChildNodes)
                {
                    foreach (var part in this.CreateParts(innerChildNode, labelBuilder))
                    {
                        yield return part;
                    }
                }
            }
            else if (childNode.Name == "ul")
            {
                // TODO: Does this work?
                foreach (var item in childNode.ChildNodes.Where(x => x.Name == "li"))
                {
                    foreach (var part in this.CreateParts(item, labelBuilder))
                    {
                        yield return part;
                    }
                }
            }
            else if (childNode.Name == "li")
            {
                // TODO: Does this work?
                foreach (var innerChildNode in childNode.ChildNodes)
                {
                    foreach (var part in this.CreateParts(innerChildNode, labelBuilder))
                    {
                        yield return part;
                    }
                }
            }
            else if (childNode.Name == "div")
            {
                foreach (var innerChildNode in childNode.ChildNodes)
                {
                    foreach (var part in this.CreateParts(innerChildNode, labelBuilder))
                    {
                        yield return part;
                    }
                }
            }
            else if (childNode.Name == "code")
            {
                yield return labelBuilder.CreatePart(childNode.InnerText);
            }
            else if (childNode.Name == "img")
            {
                var builder = labelBuilder.CreatePart(string.Empty);
                yield return builder.SetPrefixImage(this.externalImageService.GetImage(childNode.GetAttributeValue("src", string.Empty)));
            }
            else if (childNode.Name == "s")
            {
                foreach (var innerChildNode in childNode.ChildNodes)
                {
                    foreach (var part in this.CreateParts(innerChildNode, labelBuilder))
                    {
                        yield return part.MakeStrikeThrough();
                    }
                }
            }
            else if (childNode.Name == "dl")
            {
                foreach (var innerChildNode in childNode.ChildNodes)
                {
                    foreach (var part in this.CreateParts(innerChildNode, labelBuilder))
                    {
                        yield return part;
                    }
                }
            }
            else if (childNode.Name == "font")
            {
                foreach (var innerChildNode in childNode.ChildNodes)
                {
                    foreach (var part in this.CreateParts(innerChildNode, labelBuilder))
                    {
                        yield return part;
                    }
                }
            }
            else if (childNode.Name == "big")
            {
                // TODO: Make it big when merged with core
                foreach (var innerChildNode in childNode.ChildNodes)
                {
                    foreach (var part in this.CreateParts(innerChildNode, labelBuilder))
                    {
                        yield return part;
                    }
                }
            }
            else if (childNode.Name == "ol")
            {
                // TODO: Does this work?
                foreach (var item in childNode.ChildNodes.Where(x => x.Name == "li"))
                {
                    foreach (var part in this.CreateParts(item, labelBuilder))
                    {
                        yield return part;
                    }
                }
            }
        }

        // GW2's chat-link binary format: a type byte, an item-quantity byte only for type 2, then the id
        // little-endian, padded to at least 4 bytes and an even length -- ported directly from the
        // wiki's own encoder (data-type/data-id come from its "gamelink" template).
        private static string DecodeGameLink(string type, string idText)
        {
            if (string.IsNullOrEmpty(type) || !GameLinkTypeBytes.TryGetValue(type.Trim(), out var typeByte) ||
                !long.TryParse(idText, out var id) || id < 0)
            {
                return null;
            }

            var bytes = new List<byte>();
            var remaining = id;
            while (remaining > 0)
            {
                bytes.Add((byte)(remaining & 0xFF));
                remaining >>= 8;
            }

            while (bytes.Count < 4 || bytes.Count % 2 != 0)
            {
                bytes.Add(0);
            }

            if (typeByte == GameLinkTypeBytes["item"])
            {
                bytes.Insert(0, 1);
            }

            bytes.Insert(0, (byte)typeByte);

            return "[&" + Convert.ToBase64String(bytes.ToArray()) + "]";
        }
    }
}
