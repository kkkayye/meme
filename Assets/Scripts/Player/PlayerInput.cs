using RuneArena.Combat;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Player
{
    /// <summary>Legacy Input Manager bindings for the human unit: WASD movement, mouse facing, LMB basic attack, Q/W/E/R quick-cast, Esc pause, Enter/Space ready, 1/2/3 draft picks.</summary>
    public sealed class PlayerInput : MonoBehaviour
    {
        private static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);
        private static readonly KeyCode[] DraftKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };
        private static readonly KeyCode[] SkillKeys = { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R };
        private static readonly SkillKey[] SkillSlots = { SkillKey.Q, SkillKey.W, SkillKey.E, SkillKey.R };

        public static PlayerInput Instance { get; private set; }

        public Unit Bound { get; private set; }
        /// <summary>Cursor projected onto the ground plane (y = 0) this frame.</summary>
        public Vector3 CursorGroundPoint { get; private set; }
        /// <summary>False while menus/draft/shop own the input.</summary>
        public bool GameplayEnabled { get; private set; } = true;
        /// <summary>Movement input this frame (world axes, normalized).</summary>
        public Vector3 MoveInput { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>Starts driving the given unit (null to unbind).</summary>
        public void Bind(Unit unit)
        {
            Bound = unit;
        }

        public void Unbind()
        {
            Bound = null;
        }

        /// <summary>Enables/disables gameplay controls (movement/attacks/skills); Esc and menu keys keep working.</summary>
        public void SetGameplayEnabled(bool enabled)
        {
            GameplayEnabled = enabled;
        }

        private void Update()
        {
            UpdateCursor();
            HandleMenuKeys();
            if (Bound == null || !GameplayEnabled || !Bound.IsAlive) return;
            if (GameServices.Match != null && (GameServices.Match.IsPaused || GameServices.Match.Phase != MatchPhase.Combat)) return;
            HandleMovement();
            HandleAim();
            HandleAttacks();
        }

        private void UpdateCursor()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Ground.Raycast(ray, out float enter)) CursorGroundPoint = ray.GetPoint(enter);
        }

        private void HandleMenuKeys()
        {
            var match = GameServices.Match;
            if (match == null) return;
            if (Input.GetKeyDown(KeyCode.Escape)) match.TogglePause();
            if (match.IsPaused || Bound == null) return;
            if (match.Phase == MatchPhase.Shop && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                match.PlayerReadyForShop();
            }
            if (match.Phase == MatchPhase.RuneDraft && GameServices.Draft != null)
            {
                for (int i = 0; i < DraftKeys.Length; i++)
                {
                    if (Input.GetKeyDown(DraftKeys[i])) GameServices.Draft.Pick(Bound, i);
                }
            }
        }

        private void HandleMovement()
        {
            Vector3 dir = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) dir += Vector3.forward;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) dir += Vector3.back;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) dir += Vector3.left;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) dir += Vector3.right;
            MoveInput = dir.sqrMagnitude > 0f ? dir.normalized : Vector3.zero;
            Bound.Motor.Move(MoveInput);
        }

        private void HandleAim()
        {
            if (Bound.Motor.IsDashing) return;
            Vector3 aim = CursorGroundPoint - Bound.Position;
            aim.y = 0f;
            if (aim.sqrMagnitude > 0.04f) Bound.Motor.Face(aim);
        }

        private void HandleAttacks()
        {
            Vector3 aimDir = CursorGroundPoint - Bound.Position;
            aimDir.y = 0f;
            if (aimDir.sqrMagnitude < 1e-4f) aimDir = Bound.Facing;
            for (int i = 0; i < SkillKeys.Length; i++)
            {
                if (Input.GetKeyDown(SkillKeys[i])) Bound.Caster.TryCast(SkillSlots[i], aimDir, CursorGroundPoint);
            }
            if (Input.GetMouseButton(0) && !Bound.Caster.IsCasting) Bound.Caster.TryCast(SkillKey.Basic, aimDir, CursorGroundPoint);
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Instance, this)) Instance = null;
        }
    }
}
