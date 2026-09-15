using System.Collections.Generic;
using System.Diagnostics;

namespace Quarry.WikiData.Achievement
{
    [DebuggerDisplay("Objectives: {EntryList.Count} || {GameText} || {GameHint}")]
    public class ObjectivesDescription : AchievementTableEntryDescription
    {
        public List<TableDescriptionEntry> EntryList { get; set; } = new List<TableDescriptionEntry>();
    }
}
