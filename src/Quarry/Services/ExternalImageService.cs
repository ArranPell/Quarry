using Blish_HUD;
using Blish_HUD.Content;
using Quarry.Interfaces;
using Flurl.Http;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Quarry.Services
{
    public class ExternalImageService : IExternalImageService
    {
        private readonly GraphicsService graphicsService;
        private readonly Logger logger;

        public ExternalImageService(GraphicsService graphicsService, Logger logger)
        {
            this.graphicsService = graphicsService;
            this.logger = logger;
        }

        public AsyncTexture2D GetImage(string imageUrl)
            => this.GetImageInternal((async () => await this.DownloadWikiContent(imageUrl).GetStreamAsync(), imageUrl));

        public async Task<string> GetDirectImageLink(string imagePath, CancellationToken cancellationToken = default)
        {
            if (imagePath.Contains("File:"))
            {
                try
                {
                    var source = await this.DownloadWikiContent(imagePath).GetStringAsync(cancellationToken);

                    // Phase 56 (review item 28): a File: page without the marker used to feed -1 into the
                    // next IndexOf, throw, return "" and then download the wiki homepage as an image.
                    var fillImageStartIndex = source.IndexOf("fullImageLink", StringComparison.Ordinal);
                    var hrefStartIndex = fillImageStartIndex < 0 ? -1 : source.IndexOf("href=", fillImageStartIndex, StringComparison.Ordinal);
                    var quoteIndex = hrefStartIndex < 0 ? -1 : source.IndexOf("\"", hrefStartIndex, StringComparison.Ordinal);
                    var linkStartIndex = quoteIndex < 0 ? -1 : quoteIndex + 1;
                    var linkEndIndex = linkStartIndex < 0 ? -1 : source.IndexOf("\"", linkStartIndex, StringComparison.Ordinal);

                    if (linkStartIndex < 0 || linkEndIndex < 0)
                    {
                        this.logger.Debug($"No full-image link on wiki page {imagePath}; showing the error texture.");
                        return null;
                    }

                    return source.Substring(linkStartIndex, linkEndIndex - linkStartIndex);
                }
                catch (Exception ex)
                {
                    this.logger.Debug(ex, "Couldn't resolve a wiki File: page to a direct image link.");
                    return string.Empty;
                }
            }

            return imagePath;
        }

        public AsyncTexture2D GetImageFromIndirectLink(string imagePath)
            => this.GetImageInternal((async () =>
            {
                var link = await this.GetDirectImageLink(imagePath);
                return string.IsNullOrEmpty(link) ? null : await this.DownloadWikiContent(link).GetStreamAsync();
            }, imagePath));

        private AsyncTexture2D GetImageInternal((Func<Task<Stream>> GetStream, string Url) getImageStream)
        {
            var texture = new AsyncTexture2D(ContentService.Textures.TransparentPixel);

            _ = Task.Run(async () =>
            {
                try
                {
                    var imageStream = await getImageStream.GetStream();

                    if (imageStream is null)
                    {
                        // No direct link could be resolved -- nothing was requested, nothing to decode.
                        this.graphicsService.QueueMainThreadRender(_ => texture.SwapTexture(ContentService.Textures.Error));
                        return;
                    }

                    this.graphicsService.QueueMainThreadRender(device =>
                    {
                        try
                        {
                            texture.SwapTexture(TextureUtil.FromStreamPremultiplied(device, imageStream));
                            imageStream.Close();
                        }
                        catch (Exception ex)
                        {
                            this.logger.Warn(ex, $"Couldn't decode a wiki image; showing the error texture. URL: {getImageStream.Url}");

                            this.graphicsService.QueueMainThreadRender(_ => texture.SwapTexture(ContentService.Textures.Error));
                        }
                    });
                }
                catch (Exception ex)
                {
                    this.logger.Warn(ex, $"Couldn't download a wiki image; showing the error texture. URL: {getImageStream.Url}");

                    this.graphicsService.QueueMainThreadRender(_ => texture.SwapTexture(ContentService.Textures.Error));
                }
            });

            return texture;
        }

        private IFlurlRequest DownloadWikiContent(string url)
            => ("https://wiki.guildwars2.com" + url)
                    .WithHeader("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/94.0.4606.81 Safari/537.36");
    }
}
