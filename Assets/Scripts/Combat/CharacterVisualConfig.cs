using UnityEngine;

namespace RuneArena.Combat
{
    /// <summary>Per-model tweaks stored on a character prefab (Resources/Characters/&lt;heroId&gt;.prefab): facing correction and scale. Edited by hand after looking at the imported model.</summary>
    public sealed class CharacterVisualConfig : MonoBehaviour
    {
        [Tooltip("Extra yaw (degrees) so the model faces +Z, the unit's forward.")]
        public float FacingYawOffset;
        [Tooltip("Multiplies the automatic fit-to-body-height scale.")]
        public float ScaleMultiplier = 1f;
        [Tooltip("Vertical offset applied after the feet are placed on the ground.")]
        public float GroundOffset;
    }
}
