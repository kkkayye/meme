using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Juice
{
    /// <summary>Coalescing hit-stop: drops Time.timeScale to GameConstants.HitStopTimeScale and, after an UNSCALED delay, restores the time scale that was captured before the stop (never a hard-coded 1, so paused/sped-up sessions keep working).</summary>
    public sealed class HitStop
    {
        /// <summary>True while the time scale is lowered by this hit-stop.</summary>
        public bool IsActive { get; private set; }
        /// <summary>Unscaled seconds left before the time scale is restored.</summary>
        public float Remaining { get; private set; }
        /// <summary>Time scale that will be restored when the stop ends.</summary>
        public float CapturedTimeScale { get; private set; } = 1f;

        /// <summary>Requests a stop of the given unscaled duration. Overlapping requests coalesce (max remaining). Ignored while the game is paused or already slowed by someone else.</summary>
        public void Request(float seconds)
        {
            if (seconds <= 0f) return;
            if (IsActive)
            {
                Remaining = Mathf.Max(Remaining, seconds);
                return;
            }
            float current = Time.timeScale;
            if (current <= GameConstants.HitStopTimeScale) return;
            CapturedTimeScale = current;
            Time.timeScale = GameConstants.HitStopTimeScale;
            Remaining = seconds;
            IsActive = true;
        }

        /// <summary>Advances the stop with unscaled delta time and restores the time scale when it runs out.</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (!IsActive) return;
            Remaining -= Mathf.Max(0f, unscaledDeltaTime);
            if (Remaining <= 0f) Restore();
        }

        /// <summary>Ends the stop immediately. The captured scale is only written back if nobody else changed Time.timeScale meanwhile (e.g. a pause to 0 wins).</summary>
        public void Restore()
        {
            if (!IsActive) return;
            IsActive = false;
            Remaining = 0f;
            if (Mathf.Approximately(Time.timeScale, GameConstants.HitStopTimeScale))
            {
                Time.timeScale = CapturedTimeScale;
            }
        }
    }
}
