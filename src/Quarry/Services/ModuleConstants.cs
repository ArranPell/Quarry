namespace Quarry.Services
{
    internal static class ModuleConstants
    {
        // Must match manifest.json "directories".
        public const string DataDirectoryName = "quarry";

        // Blish's shared markers folder (the same addons\blishhud\markers Pathing watches) -- also must
        // match manifest.json "directories".
        public const string MarkerPacksDirectoryName = "markers";
    }
}
