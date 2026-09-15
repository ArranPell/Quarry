namespace Quarry.Interfaces
{
    /// <summary>
    /// Phase 31: the hide/snooze affordance on an <see cref="UserInterface.Controls.AchievementCard"/>.
    /// The card is shared with the All tab, where hiding makes no sense (browse surfaces stay
    /// unfiltered, per Phase 25/27), so the affordance is opt-in -- only HereView passes one of these.
    /// </summary>
    public interface IHereCardActions
    {
        // True when this card is being rendered in Here's "Show hidden" block, which swaps the menu from
        // Not today / Not interested to Unhide.
        bool IsHidden(int achievementId);

        // Hidden until the next 00:00 UTC.
        void SnoozeUntilReset(int achievementId);

        // Hidden until un-hidden.
        void HideIndefinitely(int achievementId);

        void Unhide(int achievementId);

        // Tooltip line for a hidden card, e.g. "Hidden until the daily reset." Null for a live card.
        string DescribeExclusion(int achievementId);
    }
}
