using RuneArena.Combat;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Player
{
    /// <summary>Follow camera: perspective FOV 50, pitch 62 deg, height 16, 0.15 s smoothing, 15% lead toward the cursor. Lives on the Camera GameObject.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class PlayerCamera : MonoBehaviour
    {
        private const float MaxLeadDistance = 6f;

        private Vector3 _focus;
        private Vector3 _velocity;
        private bool _snap = true;

        public Camera Cam { get; private set; }
        public Unit Target { get; private set; }
        /// <summary>Camera offset from the focus point (height 16, pulled back by the pitch).</summary>
        public static Vector3 Offset => new Vector3(0f, GameConstants.CameraHeight, -GameConstants.CameraHeight / Mathf.Tan(GameConstants.CameraPitchDegrees * Mathf.Deg2Rad));

        /// <summary>Adds (or returns the existing) PlayerCamera on the camera and configures FOV/pitch.</summary>
        public static PlayerCamera Install(Camera cam)
        {
            if (cam == null) throw new System.ArgumentNullException(nameof(cam));
            PlayerCamera existing = cam.GetComponent<PlayerCamera>();
            if (existing == null) existing = cam.gameObject.AddComponent<PlayerCamera>();
            existing.Cam = cam;
            cam.fieldOfView = GameConstants.CameraFov;
            cam.transform.rotation = Quaternion.Euler(GameConstants.CameraPitchDegrees, 0f, 0f);
            cam.transform.position = Offset;
            return existing;
        }

        /// <summary>Starts following the unit (null to stop). All-bot matches follow Blue unit 0.</summary>
        public void Follow(Unit unit)
        {
            Target = unit;
        }

        /// <summary>Snaps immediately to the target (round start).</summary>
        public void SnapToTarget()
        {
            _snap = true;
        }

        private void LateUpdate()
        {
            if (Target == null) return;
            Vector3 desired = DesiredFocus();
            if (_snap)
            {
                _focus = desired;
                _velocity = Vector3.zero;
                _snap = false;
            }
            else
            {
                _focus = Vector3.SmoothDamp(_focus, desired, ref _velocity, GameConstants.CameraSmoothSeconds, Mathf.Infinity, Time.unscaledDeltaTime);
            }
            transform.position = _focus + Offset;
            transform.rotation = Quaternion.Euler(GameConstants.CameraPitchDegrees, 0f, 0f);
        }

        private Vector3 DesiredFocus()
        {
            Vector3 focus = Target.Position;
            focus.y = 0f;
            PlayerInput input = PlayerInput.Instance;
            if (input != null && Target.IsHuman)
            {
                Vector3 lead = input.CursorGroundPoint - focus;
                lead.y = 0f;
                if (lead.magnitude > MaxLeadDistance) lead = lead.normalized * MaxLeadDistance;
                focus += lead * GameConstants.CameraCursorLead;
            }
            return focus;
        }
    }
}
