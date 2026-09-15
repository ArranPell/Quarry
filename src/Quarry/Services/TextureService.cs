using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Modules.Managers;
using Quarry.Interfaces;

namespace Quarry.Services
{
    public class TextureService : ITextureService, IDisposable
    {
        private readonly ConcurrentDictionary<string, AsyncTexture2D> textures;
        private readonly ConcurrentDictionary<string, AsyncTexture2D> refTextures;
        private readonly ContentService contentService;
        private readonly ContentsManager contentsManager;

        public TextureService(ContentService contentService, ContentsManager contentsManager)
        {
            this.textures = new ConcurrentDictionary<string, AsyncTexture2D>();
            this.refTextures = new ConcurrentDictionary<string, AsyncTexture2D>();

            this.contentService = contentService;
            this.contentsManager = contentsManager;
        }

        public AsyncTexture2D GetTexture(string url)
        {
            if (this.textures.TryGetValue(url, out var texture))
            {
                return texture;
            }

            texture = this.contentService.GetRenderServiceTexture(url);

            if (texture != null)
            {
                _ = this.textures.AddOrUpdate(url, texture, (key, value) => value = texture);
            }

            return texture;
        }

        public AsyncTexture2D GetRefTexture(string file)
        {
            if (this.refTextures.TryGetValue(file, out var texture))
            {
                return texture;
            }

            texture = this.contentsManager.GetTexture(file);

            if (texture != null)
            {
                _ = this.refTextures.AddOrUpdate(file, texture, (key, value) => value = texture);
            }

            return texture;
        }

        public void Dispose()
        {
            // this.textures comes from ContentService.GetRenderServiceTexture -> DatAssetCache, which
            // caches AsyncTexture2D per asset id and shares that same instance across every module and
            // Blish HUD core. Disposing them here would break other modules' icons -- just drop our
            // references. this.refTextures is this module's own bundled ref/*.png content (via
            // ContentsManager), private to this module, so those are safe to dispose.
            foreach (var item in this.refTextures)
            {
                item.Value.Dispose();
            }

            this.textures.Clear();
            this.refTextures.Clear();
        }
    }
}
