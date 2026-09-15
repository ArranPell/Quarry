using Blish_HUD.Content;
using System;

namespace Quarry.Interfaces
{
    public interface ITextureService : IDisposable
    {
        AsyncTexture2D GetTexture(string url);

        AsyncTexture2D GetRefTexture(string fileName);
    }
}
