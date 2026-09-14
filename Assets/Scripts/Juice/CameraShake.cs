using System.Collections;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Juice
{
    /// <summary>Trauma-model camera shake (shake = trauma squared, decays 1.5/s) applied as a transient local offset/rotation on top of whatever PlayerCamera set. Runs after PlayerCamera (execution order 1000) and undoes its own offset before the next camera update.</summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class CameraShake : MonoBehaviour
    {
        private const float NoiseFrequency = 22f;
        private const float SeedRange = 1000f;

        /// <summary>Current trauma 0..1.</summary>
        public float Trauma { get; private set; }
        public bool IsShaking => Trauma > 0f;
        /// <summary>World-space positional offset applied this frame (zero when idle).</summary>
        public Vector3 CurrentOffset { get; private set; }

        private float _seedX;
        private float _seedY;
        private float _seedPitch;
        private float _seedYaw;
        private float _seedRoll;
        private Vector3 _basePosition;
        private Vector3 _appliedPosition;
        private Quaternion _baseRotation;
        private Quaternion _appliedRotation;
        private bool _applied;
        private WaitForEndOfFrame _endOfFrame;

        /// <summary>Adds (or returns the existing) CameraShake on the camera's GameObject.</summary>
        public static CameraShake Install(Camera cam)
        {
            if (cam == null) throw new System.ArgumentNullException(nameof(cam));
            CameraShake existing = cam.GetComponent<CameraShake>();
            if (existing == null) existing = cam.gameObject.AddComponent<CameraShake>();
            return existing;
        }

        /// <summary>Adds trauma (clamped to 1). Negative or zero amounts are ignored.</summary>
        public void AddTrauma(float amount)
        {
            if (amount <= 0f) return;
            Trauma = Mathf.Clamp01(Trauma + amount);
        }

        /// <summary>Zeroes trauma and removes any offset still applied to the camera.</summary>
        public void ClearShake()
        {
            Trauma = 0f;
            CurrentOffset = Vector3.zero;
            Undo();
        }

        private void Awake()
        {
            // Cosmetic jitter only: UnityEngine.Random is allowed here (never affects gameplay).
            _seedX = Random.Range(0f, SeedRange);
            _seedY = Random.Range(0f, SeedRange);
            _seedPitch = Random.Range(0f, SeedRange);
            _seedYaw = Random.Range(0f, SeedRange);
            _seedRoll = Random.Range(0f, SeedRange);
            _endOfFrame = new WaitForEndOfFrame();
        }

        private void OnEnable()
        {
            StartCoroutine(UndoAtEndOfFrame());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            Undo();
        }

        private void Update()
        {
            Undo();
        }

        private void LateUpdate()
        {
            Undo();
            Trauma = Mathf.Max(0f, Trauma - GameConstants.TraumaDecayPerSecond * Time.unscaledDeltaTime);
            if (Trauma <= 0f)
            {
                CurrentOffset = Vector3.zero;
                return;
            }
            Apply();
        }

        private IEnumerator UndoAtEndOfFrame()
        {
            while (true)
            {
                yield return _endOfFrame;
                Undo();
            }
        }

        private void Apply()
        {
            float shake = Trauma * Trauma;
            float t = Time.unscaledTime * NoiseFrequency;
            float maxOffset = GameConstants.ShakeMaxOffset * shake;
            float maxRotation = GameConstants.ShakeMaxRotationDegrees * shake;
            Transform tr = transform;
            _basePosition = tr.position;
            _baseRotation = tr.rotation;
            Vector3 offset = tr.right * (Noise(_seedX, t) * maxOffset) + tr.up * (Noise(_seedY, t) * maxOffset);
            Quaternion wobble = Quaternion.Euler(
                Noise(_seedPitch, t) * maxRotation,
                Noise(_seedYaw, t) * maxRotation,
                Noise(_seedRoll, t) * maxRotation);
            _appliedPosition = _basePosition + offset;
            _appliedRotation = _baseRotation * wobble;
            tr.position = _appliedPosition;
            tr.rotation = _appliedRotation;
            CurrentOffset = offset;
            _applied = true;
        }

        /// <summary>Removes the last applied offset, but only if nobody moved the camera since (an absolute set by PlayerCamera already discarded it).</summary>
        private void Undo()
        {
            if (!_applied) return;
            _applied = false;
            Transform tr = transform;
            if (tr.position == _appliedPosition && tr.rotation == _appliedRotation)
            {
                tr.position = _basePosition;
                tr.rotation = _baseRotation;
            }
        }

        private static float Noise(float seed, float t)
        {
            return Mathf.PerlinNoise(seed, t) * 2f - 1f;
        }
    }
}
