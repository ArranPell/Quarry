using Quarry.Models.Markers;
using Microsoft.Xna.Framework;

namespace Quarry.Services
{
    // Phase 17: the only place that knows about the Mumble/pack coordinate swap. Pack xpos/ypos/zpos are
    // Mumble-space metres (TacO reads the avatar position straight from Mumble Link, no scale); Pathing's
    // own StandardMarker[xpos,ypos,zpos].cs transposes to Vector3(x, z, y) for Blish's Z-up world, and
    // GameService.Gw2Mumble.PlayerCharacter.Position is that same space -- so distance needs no
    // conversion beyond the same transpose.
    public static class ObjectiveGeometry
    {
        public static Vector3 ToWorld(AchievementObjective objective)
            => new Vector3(objective.X, objective.Z, objective.Y);

        public static float DistanceMetres(Vector3 player, AchievementObjective objective)
        {
            var world = ToWorld(objective);

            // Phase 28: a wiki coordinate has no height, so compare on the ground plane only. Filling in
            // a guessed height would produce a confidently wrong number -- the exact failure Phase 24
            // removed for trails -- and on foot the horizontal distance is the one you care about anyway.
            if (objective.HeightUnknown)
            {
                var dx = player.X - world.X;
                var dy = player.Y - world.Y;
                return (float)System.Math.Sqrt((dx * dx) + (dy * dy));
            }

            return Vector3.Distance(player, world);
        }
    }
}
