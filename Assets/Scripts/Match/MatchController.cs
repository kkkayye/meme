using System;
using System.Collections.Generic;
using RuneArena.AI;
using RuneArena.Combat;
using RuneArena.Core;
using RuneArena.Items;
using RuneArena.Juice;
using RuneArena.Loot;
using RuneArena.Player;
using RuneArena.Runes;
using RuneArena.UI;
using UnityEngine;

namespace RuneArena.Match
{
    /// <summary>Owns the match: creates services, spawns teams, runs the MatchPhase state machine (via MatchPhaseRunner), pause, kill rewards and round/match results.</summary>
    public sealed class MatchController : MonoBehaviour
    {
        private static readonly Unit[] EmptyUnits = Array.Empty<Unit>();

        private readonly int[] _wins = new int[2];
        private readonly List<Unit> _units = new List<Unit>();
        private MatchPhaseRunner _runner;
        private Transform _unitsRoot;
        private bool _subscribed;

        public MatchPhase Phase { get; private set; } = MatchPhase.MainMenu;
        /// <summary>1-based round number (0 before the first draft).</summary>
        public int Round { get; internal set; }
        public bool IsPaused { get; private set; }
        /// <summary>Seconds left in the current timed phase (draft / shop / countdown / combat / round end).</summary>
        public float PhaseTimeRemaining { get; internal set; }
        /// <summary>The human's unit, or null in all-bot matches / outside a match.</summary>
        public Unit HumanUnit { get; private set; }
        public IReadOnlyList<Unit> Units { get; private set; } = EmptyUnits;
        public MatchConfig Config { get; private set; } = MatchConfig.Default;
        public Arena Arena { get; private set; }
        public ControlPoint ControlPoint { get; private set; }
        /// <summary>Winner once Phase == MatchEnd, else null.</summary>
        public Team? MatchWinner { get; internal set; }
        /// <summary>True between StartMatch and Teardown.</summary>
        public bool IsMatchActive { get; private set; }
        /// <summary>Seed actually used by the current match (resolved from the clock when Config.Seed is 0).</summary>
        public int ResolvedSeed { get; private set; }

        public UiRoot Ui { get; private set; }
        public JuiceListener Juice { get; private set; }
        public ChestSpawner Chests { get; private set; }
        public PlayerCamera Camera { get; private set; }
        public PlayerInput Input { get; private set; }

        /// <summary>Unit the camera and HUD follow: the human, else Blue unit 0.</summary>
        public Unit SpectatedUnit => HumanUnit != null ? HumanUnit : (_units.Count > 0 ? _units[0] : null);

        public int Wins(Team team)
        {
            return _wins[(int)team];
        }

        /// <summary>Wires the long-lived scene objects created by Bootstrap. Call once.</summary>
        public void Initialize(UiRoot ui, JuiceListener juice, Arena arena, ControlPoint controlPoint, ChestSpawner chests, PlayerCamera camera, PlayerInput input)
        {
            Ui = ui;
            Juice = juice;
            Arena = arena;
            ControlPoint = controlPoint;
            Chests = chests;
            Camera = camera;
            Input = input;
            _runner = new MatchPhaseRunner(this);
            if (!_subscribed)
            {
                EventBus.Subscribe<UnitDied>(OnUnitDied);
                _subscribed = true;
            }
            GameServices.Match = this;
            GameServices.Ui = ui;
            GameServices.Juice = juice;
        }

        /// <summary>Shows the main menu (no match running).</summary>
        public void ShowMenu()
        {
            SetPhase(MatchPhase.MainMenu);
            Ui?.ShowMainMenu();
        }

        /// <summary>Enters HeroSelect with the given config (from the main menu).</summary>
        public void BeginHeroSelect(MatchConfig config)
        {
            Config = config ?? MatchConfig.Default;
            SetPhase(MatchPhase.HeroSelect);
            Ui?.ShowHeroSelect();
        }

        /// <summary>Confirms the human hero and starts the match with the stored config.</summary>
        public void ConfirmHero(string heroId)
        {
            StartMatch(string.IsNullOrEmpty(heroId) ? Config : Config.WithHero(heroId));
        }

        /// <summary>Resets GameServices, resolves the seed (0 = clock), builds the arena, spawns teams, and enters RuneDraft for round 1.</summary>
        public void StartMatch(MatchConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (_runner == null) throw new InvalidOperationException("MatchController.Initialize must run before StartMatch.");
            Teardown();
            Config = config;
            ResolvedSeed = config.Seed != 0 ? config.Seed : (Environment.TickCount & 0x7fffffff) | 1;
            CreateServices();
            SpawnUnits();
            IsMatchActive = true;
            EventBus.Publish(new MatchStarted(Config));
            _runner.EnterRuneDraft();
        }

        /// <summary>Restarts with the same config and a new seed.</summary>
        public void Rematch()
        {
            StartMatch(Config.WithSeed(0));
        }

        /// <summary>Tears down the match (units, arena, chests, services) and shows the main menu.</summary>
        public void ReturnToMenu()
        {
            Teardown();
            ShowMenu();
        }

        /// <summary>Human pressed Ready in the shop (Enter / Space / button).</summary>
        public void PlayerReadyForShop()
        {
            if (Phase != MatchPhase.Shop || HumanUnit == null) return;
            GameServices.Shop?.SetReady(HumanUnit);
        }

        /// <summary>Toggles pause (Time.timeScale 0 / 1) and the pause overlay. Ignored outside a match.</summary>
        public void TogglePause()
        {
            if (!IsMatchActive || Phase == MatchPhase.MatchEnd) return;
            IsPaused = !IsPaused;
            Time.timeScale = IsPaused ? 0f : 1f;
            Ui?.ShowPause(IsPaused);
        }

        internal void SetPhase(MatchPhase to)
        {
            MatchPhase from = Phase;
            Phase = to;
            bool combat = to == MatchPhase.Combat;
            Input?.SetGameplayEnabled(combat);
            for (int i = 0; i < _units.Count; i++)
            {
                BotBrain brain = _units[i] != null ? _units[i].GetComponent<BotBrain>() : null;
                if (brain != null) brain.SetEnabled(combat);
            }
            EventBus.Publish(new PhaseChanged(from, to, Round));
        }

        internal void AddWin(Team team)
        {
            _wins[(int)team]++;
        }

        private void CreateServices()
        {
            GameServices.Reset();
            GameServices.Match = this;
            GameServices.Ui = Ui;
            GameServices.Juice = Juice;
            GameServices.Config = Config;
            GameServices.Rng = new Rng(ResolvedSeed);
            GameServices.World = new CombatWorld();
            GameServices.Economy = new Economy();
            GameServices.Draft = new RuneDraftService();
            GameServices.Shop = new ShopService();
            GameServices.Loot = new LootService();
            GameServices.Scoring = new Scoring();
            GameServices.Arena = Arena;
            GameServices.ControlPoint = ControlPoint;
            GameServices.Chests = Chests;
            Arena.Build();
            GameServices.World.Arena = Arena;
            ControlPoint.Build(Arena.Center);
            ControlPoint.ResetPoint();
            Chests.ResetTimer();
            _wins[0] = 0;
            _wins[1] = 0;
            Round = 0;
            MatchWinner = null;
            IsPaused = false;
            Time.timeScale = 1f;
        }

        private void SpawnUnits()
        {
            _unitsRoot = new GameObject("Units").transform;
            _unitsRoot.SetParent(transform, false);
            List<Unit> spawned = TeamSpawner.Spawn(Config, Arena, GameServices.Rng.Fork("teams"), _unitsRoot, out Unit human);
            _units.Clear();
            _units.AddRange(spawned);
            Units = _units;
            HumanUnit = human;
            for (int i = 0; i < _units.Count; i++) GameServices.Economy.Register(_units[i]);
            Input?.Bind(HumanUnit);
            Camera?.Follow(SpectatedUnit);
            Camera?.SnapToTarget();
        }

        private void Teardown()
        {
            bool wasActive = IsMatchActive;
            IsMatchActive = false;
            Input?.Unbind();
            Camera?.Follow(null);
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i] != null) Destroy(_units[i].gameObject);
            }
            _units.Clear();
            Units = EmptyUnits;
            HumanUnit = null;
            if (_unitsRoot != null) Destroy(_unitsRoot.gameObject);
            _unitsRoot = null;
            Chests?.ClearAll();
            if (CombatFx.Exists) CombatFx.Instance.ClearAll();
            Arena?.Clear();
            GameServices.World?.Clear();
            GameServices.Draft?.Reset();
            GameServices.Shop?.Reset();
            GameServices.Loot?.Reset();
            GameServices.Economy?.Clear();
            IsPaused = false;
            Time.timeScale = 1f;
            if (wasActive) Ui?.HideOverlays();
        }

        private void Update()
        {
            if (!IsMatchActive || IsPaused || _runner == null) return;
            _runner.Tick(Time.deltaTime);
        }

        private void OnUnitDied(UnitDied e)
        {
            if (!IsMatchActive || Phase != MatchPhase.Combat || e.Victim == null) return;
            Team scoringTeam = e.Killer != null && e.Killer.Team != e.Victim.Team ? e.Killer.Team : Opposite(e.Victim.Team);
            GameServices.Scoring?.RecordKill(scoringTeam);
            if (e.Killer == null || e.Killer.Team == e.Victim.Team) return;
            e.Killer.AddGold(GameConstants.KillGold);
            CombatWorld world = GameServices.World;
            if (world == null) return;
            foreach (Unit ally in world.AlliesOf(e.Killer.Team))
            {
                if (ReferenceEquals(ally, e.Killer)) continue;
                if (CombatWorld.FlatSqrDistance(ally.Position, e.Victim.Position) <= GameConstants.AssistRadius * GameConstants.AssistRadius)
                {
                    ally.AddGold(GameConstants.AssistGold);
                }
            }
        }

        private static Team Opposite(Team team)
        {
            return team == Team.Blue ? Team.Red : Team.Blue;
        }

        private void OnDestroy()
        {
            if (_subscribed) EventBus.Unsubscribe<UnitDied>(OnUnitDied);
            _subscribed = false;
            if (ReferenceEquals(GameServices.Match, this)) GameServices.Match = null;
        }
    }
}
