using System.Collections.Generic;

namespace Quarry.WikiData.Interfaces
{
    public interface IHasDescriptionList
    {
        List<KeyValuePair<string, string>> DescriptionList { get; set; }
    }
}