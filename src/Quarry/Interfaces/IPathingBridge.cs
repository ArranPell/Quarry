namespace Quarry.Interfaces
{
    /// <summary>
    /// Owns the only reflection into Pathing in the module. All three members return false (and log a
    /// Warn once per session) when Pathing isn't installed, isn't enabled, or its CategoryStates shape
    /// changed underneath us -- never throws.
    /// </summary>
    public interface IPathingBridge
    {
        bool IsAvailable { get; }

        bool TryGetInactive(string categoryNamespace, out bool inactive);

        bool TrySetInactive(string categoryNamespace, bool inactive);
    }
}
