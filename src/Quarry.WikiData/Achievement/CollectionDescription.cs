using System.Collections.Generic;
using System.Diagnostics;

namespace Quarry.WikiData.Achievement
{
    [DebuggerDisplay("CollectionItems: {EntryList.Count} || {GameText} || {GameHint}")]
    public class CollectionDescription : AchievementTableEntryDescription
    {
        public List<CollectionDescriptionEntry> EntryList { get; set; } = new List<CollectionDescriptionEntry>();
    }
}
