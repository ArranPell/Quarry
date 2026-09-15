using Blish_HUD.Content;
using System.Threading;
using System.Threading.Tasks;

namespace Quarry.Interfaces
{
    public interface IExternalImageService
    {
        Task<string> GetDirectImageLink(string imagePath, CancellationToken cancellationToken = default);
        AsyncTexture2D GetImage(string imageUrl);
        AsyncTexture2D GetImageFromIndirectLink(string imagePath);
    }
}