using System;
using System.Collections.Generic;
using RuneArena.Combat;
using RuneArena.Content;
using RuneArena.Core;
using RuneArena.Runes;

namespace RuneArena.Items
{
    /// <summary>Between-round shop: 5 tier-gated weighted offers per unit, buy / reroll (100, +50 each) / sell (70%), per-unit ready flags, bot auto-shopping.</summary>
    public sealed class ShopService
    {
        private sealed class UnitState
        {
            public readonly List<ItemDefinition> Offer = new List<ItemDefinition>();
            public int Rerolls;
            public bool Ready;
        }

        private static readonly ItemDefinition[] EmptyOffer = Array.Empty<ItemDefinition>();
        private static readonly float[] HookBonusByTier = { 0f, 6f, 14f, 24f };

        private readonly Dictionary<Unit, UnitState> _states = new Dictionary<Unit, UnitState>();
        private readonly List<Unit> _shopping = new List<Unit>();
        private readonly IReadOnlyList<ItemDefinition> _catalog;
        private readonly Rng _rng;

        public bool IsOpen { get; private set; }
        public int Round { get; private set; }
        public IReadOnlyList<Unit> Shopping => _shopping;

        public ShopService() : this(null, null) { }

        /// <summary>Optional explicit rng (defaults to GameServices.Rng) and catalog (defaults to ContentCatalog.Items) for tests.</summary>
        public ShopService(Rng rng, IReadOnlyList<ItemDefinition> catalog)
        {
            _rng = rng;
            _catalog = catalog;
        }

        private Rng Random => _rng ?? GameServices.RngOrDefault();
        private IReadOnlyList<ItemDefinition> Catalog => _catalog ?? ContentCatalog.Items;

        /// <summary>Generates offers for every unit in GameServices.World and resets reroll counts / ready flags.</summary>
        public void OpenShop(int round)
        {
            IReadOnlyList<Unit> units = GameServices.World != null ? GameServices.World.Units : (IReadOnlyList<Unit>)Array.Empty<Unit>();
            OpenShop(round, units);
        }

        /// <summary>Generates offers for an explicit unit list.</summary>
        public void OpenShop(int round, IReadOnlyList<Unit> units)
        {
            if (units == null) throw new ArgumentNullException(nameof(units));
            Round = Math.Max(1, round);
            IsOpen = true;
            _shopping.Clear();
            for (int i = 0; i < units.Count; i++)
            {
                Unit unit = units[i];
                if (unit == null) continue;
                UnitState state = StateOf(unit);
                state.Rerolls = 0;
                state.Ready = false;
                GenerateOffer(unit, state);
                _shopping.Add(unit);
            }
        }

        /// <summary>The unit's current 5 offers (null entries = bought slots; empty if closed).</summary>
        public IReadOnlyList<ItemDefinition> GetOffer(Unit unit)
        {
            if (!IsOpen || unit == null || !_states.TryGetValue(unit, out UnitState state)) return EmptyOffer;
            return state.Offer;
        }

        /// <summary>True if the offer slot is affordable, not owned and the inventory has room.</summary>
        public bool CanBuy(Unit unit, int index)
        {
            ItemDefinition item = OfferAt(unit, index);
            if (item == null || unit.Items == null || !unit.Items.CanAdd(item)) return false;
            return GameServices.Economy == null || GameServices.Economy.GetGold(unit) >= item.Cost;
        }

        /// <summary>Spends gold, adds the item via Inventory.TryAdd and clears the offer slot. Returns false on any failure.</summary>
        public bool Buy(Unit unit, int index)
        {
            if (!CanBuy(unit, index)) return false;
            ItemDefinition item = OfferAt(unit, index);
            if (GameServices.Economy != null && !GameServices.Economy.TrySpend(unit, item.Cost)) return false;
            if (!unit.Items.TryAdd(item))
            {
                GameServices.Economy?.AddGold(unit, item.Cost);
                return false;
            }
            _states[unit].Offer[index] = null;
            return true;
        }

        /// <summary>Cost of the unit's next reroll this shop phase.</summary>
        public int RerollCost(Unit unit)
        {
            int rerolls = unit != null && _states.TryGetValue(unit, out UnitState state) ? state.Rerolls : 0;
            return GameConstants.ShopRerollBaseCost + GameConstants.ShopRerollIncrement * rerolls;
        }

        /// <summary>Spends RerollCost and regenerates the 5 offers. Returns false if unaffordable or closed.</summary>
        public bool Reroll(Unit unit)
        {
            if (!IsOpen || unit == null || !_states.TryGetValue(unit, out UnitState state)) return false;
            int cost = RerollCost(unit);
            if (GameServices.Economy != null && !GameServices.Economy.TrySpend(unit, cost)) return false;
            state.Rerolls++;
            GenerateOffer(unit, state);
            return true;
        }

        /// <summary>Removes the item from the unit's inventory, refunds SellValue, publishes ItemSold.</summary>
        public bool Sell(Unit unit, ItemDefinition item)
        {
            if (unit == null || item == null || unit.Items == null || !unit.Items.Remove(item)) return false;
            GameServices.Economy?.AddGold(unit, item.SellValue);
            EventBus.Publish(new ItemSold(unit, item));
            return true;
        }

        public bool IsReady(Unit unit)
        {
            return unit != null && _states.TryGetValue(unit, out UnitState state) && state.Ready;
        }

        public void SetReady(Unit unit)
        {
            if (unit == null) return;
            StateOf(unit).Ready = true;
        }

        /// <summary>True when every unit is ready (bots set ready right after buying).</summary>
        public bool AllReady
        {
            get
            {
                if (!IsOpen) return true;
                for (int i = 0; i < _shopping.Count; i++)
                {
                    if (!StateOf(_shopping[i]).Ready) return false;
                }
                return true;
            }
        }

        /// <summary>Closes the shop and clears offers.</summary>
        public void CloseShop()
        {
            IsOpen = false;
            for (int i = 0; i < _shopping.Count; i++) StateOf(_shopping[i]).Offer.Clear();
            _shopping.Clear();
        }

        /// <summary>Clears all per-unit state (match start).</summary>
        public void Reset()
        {
            _states.Clear();
            _shopping.Clear();
            IsOpen = false;
            Round = 0;
        }

        /// <summary>Items whose tier is unlocked at the given round (T1 from round 1, T2 from round 2, T3 from round 3).</summary>
        public List<ItemDefinition> AvailablePool(int round)
        {
            var pool = new List<ItemDefinition>();
            IReadOnlyList<ItemDefinition> catalog = Catalog;
            for (int i = 0; i < catalog.Count; i++)
            {
                if ((int)catalog[i].Tier <= Math.Max(1, round)) pool.Add(catalog[i]);
            }
            return pool;
        }

        /// <summary>Weight of a tier within the pool available at the round (60 / 30 / 10 among unlocked tiers).</summary>
        public static float TierWeight(ItemTier tier, int round)
        {
            if ((int)tier > Math.Max(1, round)) return 0f;
            return GameConstants.ItemTierWeights[(int)tier - 1];
        }

        /// <summary>Bot value of an item for a unit: weighted stats + passive bonus by tier.</summary>
        public static float ScoreItem(Unit unit, ItemDefinition item)
        {
            if (unit == null || item == null) return 0f;
            HeroArchetype archetype = unit.Hero != null ? unit.Hero.Archetype : HeroArchetype.Bruiser;
            float score = RuneScoring.ScoreModifiers(archetype, item.StatModifiers);
            if (item.HasHook) score += HookBonusByTier[(int)item.Tier];
            return score;
        }

        /// <summary>Greedy bot shopping: repeatedly buys the best affordable offer, never rerolls, then marks ready.</summary>
        public void AutoShop(Unit unit)
        {
            if (unit == null) return;
            int guard = GameConstants.ShopOfferCount;
            while (guard-- > 0)
            {
                int best = -1;
                float bestScore = float.MinValue;
                IReadOnlyList<ItemDefinition> offer = GetOffer(unit);
                for (int i = 0; i < offer.Count; i++)
                {
                    if (!CanBuy(unit, i)) continue;
                    float s = ScoreItem(unit, offer[i]);
                    if (s > bestScore)
                    {
                        bestScore = s;
                        best = i;
                    }
                }
                if (best < 0 || !Buy(unit, best)) break;
            }
            SetReady(unit);
        }

        private ItemDefinition OfferAt(Unit unit, int index)
        {
            IReadOnlyList<ItemDefinition> offer = GetOffer(unit);
            if (index < 0 || index >= offer.Count) return null;
            return offer[index];
        }

        private UnitState StateOf(Unit unit)
        {
            if (!_states.TryGetValue(unit, out UnitState state))
            {
                state = new UnitState();
                _states[unit] = state;
            }
            return state;
        }

        private void GenerateOffer(Unit unit, UnitState state)
        {
            state.Offer.Clear();
            List<ItemDefinition> pool = AvailablePool(Round);
            pool.RemoveAll(item => unit.Items != null && unit.Items.Has(item.Id));
            int round = Round;
            for (int slot = 0; slot < GameConstants.ShopOfferCount && pool.Count > 0; slot++)
            {
                ItemDefinition pick = Random.WeightedPick(pool, item => TierWeight(item.Tier, round));
                state.Offer.Add(pick);
                pool.Remove(pick);
            }
        }
    }
}
