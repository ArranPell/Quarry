namespace Quarry.WikiData.Achievement
{
    public abstract class Reward
    {
        public static Reward EmptyReward { get; } = new EmptyReward();
    }
}
