using RuneArena.Juice;
using RuneArena.Loot;
using RuneArena.Player;
using RuneArena.UI;
using UnityEngine;

namespace RuneArena.Match
{
    /// <summary>Persistent root object holding the long-lived systems (camera, UI, juice, arena, match controller). Created by Bootstrap.</summary>
    public sealed class GameRoot : MonoBehaviour
    {
        private static readonly Color AmbientColor = new Color(0.45f, 0.46f, 0.52f, 1f);

        public static GameRoot Instance { get; private set; }

        public Camera Camera { get; private set; }
        public MatchController Match { get; private set; }
        public UiRoot Ui { get; private set; }
        public JuiceListener Juice { get; private set; }
        public Arena Arena { get; private set; }
        public ControlPoint ControlPoint { get; private set; }
        public ChestSpawner Chests { get; private set; }
        public LaneController Lane { get; private set; }
        public PlayerCamera PlayerCamera { get; private set; }
        public PlayerInput PlayerInput { get; private set; }
        public bool IsSetUp { get; private set; }

        private void Awake()
        {
            if (Instance != null && !ReferenceEquals(Instance, this))
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>Creates or adopts the camera and light, then builds every subsystem and shows the main menu. Idempotent.</summary>
        public void Setup()
        {
            if (IsSetUp) return;
            IsSetUp = true;
            Combat.PhysicsSetup.Ensure();
            Camera = EnsureCamera();
            EnsureLight();
            RenderSettings.ambientLight = AmbientColor;
            PlayerCamera = PlayerCamera.Install(Camera);
            Juice = JuiceListener.Install(Camera);
            Ui = UiRoot.Create();
            Arena = CreateChild<Arena>("Arena");
            ControlPoint = CreateChild<ControlPoint>("ControlPoint");
            Chests = CreateChild<ChestSpawner>("Chests");
            Lane = CreateChild<LaneController>("LaneObjectives");
            PlayerInput = gameObject.AddComponent<PlayerInput>();
            Match = gameObject.AddComponent<MatchController>();
            Match.Initialize(Ui, Juice, Arena, ControlPoint, Chests, Lane, PlayerCamera, PlayerInput);
            Match.ShowMenu();
        }

        private T CreateChild<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        private static Camera EnsureCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            if (cam.GetComponent<AudioListener>() == null && Object.FindAnyObjectByType<AudioListener>() == null)
            {
                cam.gameObject.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f, 1f);
            return cam;
        }

        private static void EnsureLight()
        {
            if (Object.FindAnyObjectByType<Light>() != null) return;
            var go = new GameObject("Directional Light");
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.96f, 0.9f, 1f);
            go.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Instance, this)) Instance = null;
        }
    }
}
