using RuneArena.Combat;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.UI
{
    /// <summary>Owns the overlay canvas and every panel (HUD, draft, shop, menus, banners, chest reveal, damage numbers, overhead bars); one method per screen.</summary>
    public sealed class UiRoot : MonoBehaviour
    {
        private Hud _hud;
        private DraftPanel _draft;
        private ShopPanel _shop;
        private MenuPanels _menus;
        private ChestRevealPanel _reveal;
        private DamageNumbers _numbers;
        private OverheadBars _overheads;
        private bool _built;
        private bool _subscribed;

        public Canvas Canvas { get; private set; }
        /// <summary>Unit the HUD currently displays (null outside combat).</summary>
        public Unit HudUnit { get; private set; }
        public Hud Hud => _hud;
        public DraftPanel Draft => _draft;
        public ShopPanel Shop => _shop;
        public MenuPanels Menus => _menus;

        /// <summary>Creates the canvas GameObject with a UiRoot (or returns the existing one) and builds all panels hidden.</summary>
        public static UiRoot Create()
        {
            UiRoot existing = Object.FindAnyObjectByType<UiRoot>();
            if (existing != null)
            {
                existing.Build();
                return existing;
            }
            var go = new GameObject("UiRoot");
            if (Application.isPlaying) DontDestroyOnLoad(go);
            UiRoot root = go.AddComponent<UiRoot>();
            root.Build();
            return root;
        }

        private void Build()
        {
            if (_built) return;
            _built = true;
            Canvas = UiFactory.CreateCanvas(gameObject);
            Transform root = transform;
            _overheads = new GameObject("OverheadBars").AddComponent<OverheadBars>();
            _overheads.transform.SetParent(root, false);
            _hud = Hud.Build(root);
            _numbers = DamageNumbers.Build(root);
            _draft = DraftPanel.Build(root);
            _shop = ShopPanel.Build(root);
            _menus = MenuPanels.Build(root);
            _reveal = ChestRevealPanel.Build(root);
            HideOverlays();
            _hud.SetVisible(false);
            if (!_subscribed)
            {
                _subscribed = true;
                EventBus.Subscribe<ChestOpened>(OnChestOpened);
                EventBus.Subscribe<PhaseChanged>(OnPhaseChanged);
            }
        }

        public void ShowMainMenu()
        {
            HideOverlays();
            _hud.SetVisible(false);
            HudUnit = null;
            _menus.ShowMainMenu();
        }

        public void ShowHeroSelect()
        {
            HideOverlays();
            _menus.ShowHeroSelect();
        }

        /// <summary>Shows the 3-card draft for the unit (human); bots never see UI.</summary>
        public void ShowDraft(Unit unit)
        {
            HideOverlays();
            _draft.Show(unit);
        }

        public void ShowShop(Unit unit)
        {
            HideOverlays();
            _shop.Show(unit);
        }

        /// <summary>Shows the combat HUD bound to the unit (skill bar, gold, runes, items, HP).</summary>
        public void ShowHud(Unit unit)
        {
            HudUnit = unit;
            _hud.Bind(unit);
            _hud.SetVisible(unit != null);
        }

        public void ShowRoundEnd(Team winner, int round)
        {
            HideOverlays();
            _menus.ShowRoundEnd(winner, round);
        }

        public void ShowMatchEnd(Team winner)
        {
            HideOverlays();
            _menus.ShowMatchEnd(winner);
        }

        public void ShowPause(bool visible)
        {
            _menus.ShowPause(visible);
        }

        /// <summary>Hides every full-screen panel (draft, shop, menus, banners, pause) but keeps the HUD.</summary>
        public void HideOverlays()
        {
            _draft.Hide();
            _shop.Hide();
            _menus.HideAll();
        }

        /// <summary>1.5 s card-flip reveal with the rarity color for the reward kind.</summary>
        public void ShowChestReveal(LootKind kind, string rewardName)
        {
            _reveal.Show(kind, rewardName);
        }

        /// <summary>Floating world-space damage number (orange Physical, cyan Magical, white True; crit 1.4x).</summary>
        public void SpawnDamageNumber(Vector3 worldPos, float amount, DamageType type, bool crit)
        {
            _numbers.Spawn(worldPos, amount, type, crit);
        }

        private void OnChestOpened(ChestOpened e)
        {
            if (HudUnit == null || !ReferenceEquals(e.Unit, HudUnit)) return;
            ShowChestReveal(e.Kind, e.RewardName);
        }

        private void OnPhaseChanged(PhaseChanged e)
        {
            if (e.To == MatchPhase.MainMenu || e.To == MatchPhase.HeroSelect) _hud.SetVisible(false);
        }

        private void OnDestroy()
        {
            if (!_subscribed) return;
            _subscribed = false;
            EventBus.Unsubscribe<ChestOpened>(OnChestOpened);
            EventBus.Unsubscribe<PhaseChanged>(OnPhaseChanged);
        }
    }
}
