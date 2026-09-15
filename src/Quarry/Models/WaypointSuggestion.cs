namespace Quarry.Models
{
    // What the Track window's copy icon would put on the clipboard, and whether taking it is actually
    // worth doing from where you're standing.
    public class WaypointSuggestion
    {
        public string Code { get; set; }

        // Null when the code came from a marker pack's own annotation rather than the API's waypoint
        // list -- a pack gives us a code with no name and no position attached.
        public string Name { get; set; }

        // How far this waypoint is from the nearest remaining objective.
        public float MetresFromObjective { get; set; }

        // How far you are from that objective right now.
        public float MetresOnFoot { get; set; }

        // False when walking beats porting -- the copy still works, the tooltip just says so.
        public bool WorthTaking { get; set; }

        public string Describe()
        {
            if (this.Name is null)
            {
                return "Copy waypoint";
            }

            if (this.WorthTaking)
            {
                return $"Copy {this.Name}\n{this.MetresFromObjective:F0} m from your next objective (you're {this.MetresOnFoot:F0} m away now)";
            }

            return $"You're closer on foot ({this.MetresOnFoot:F0} m) than any waypoint\nCopy {this.Name} anyway — {this.MetresFromObjective:F0} m from it";
        }
    }
}
