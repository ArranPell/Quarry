using Quarry.WikiData.Achievement;

namespace Quarry.WikiData.Interfaces
{
    public interface IHasInteractiveMap
    {
        InteractiveMapInformation InteractiveMap { get; set; }
    }
}