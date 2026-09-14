using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Combat
{
    /// <summary>Configures the layer collision matrix at runtime: dashing units ignore other units (so dashes pass through bodies) but still hit obstacles. Idempotent.</summary>
    public static class PhysicsSetup
    {
        private static bool _done;

        public static void Ensure()
        {
            if (_done) return;
            _done = true;
            Physics.IgnoreLayerCollision(GameConstants.DashLayer, GameConstants.UnitLayer, true);
            Physics.IgnoreLayerCollision(GameConstants.DashLayer, GameConstants.DashLayer, true);
            Physics.IgnoreLayerCollision(GameConstants.DashLayer, GameConstants.ObstacleLayer, false);
            Physics.IgnoreLayerCollision(GameConstants.UnitLayer, GameConstants.ObstacleLayer, false);
            Physics.IgnoreLayerCollision(GameConstants.UnitLayer, GameConstants.UnitLayer, false);
        }
    }
}
