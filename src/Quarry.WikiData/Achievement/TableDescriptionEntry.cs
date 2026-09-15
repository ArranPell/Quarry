using Quarry.WikiData.Interfaces;
using System.Diagnostics;

namespace Quarry.WikiData.Achievement
{
    [DebuggerDisplay("{DisplayName}")]
    public class TableDescriptionEntry : ILinkEntry
    {
        public string DisplayName { get; set; } = string.Empty;

        public string Link { set; get; } = string.Empty;
    }
}
