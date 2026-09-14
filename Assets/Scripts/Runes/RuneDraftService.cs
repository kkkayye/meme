using System;
using System.Collections.Generic;
using RuneArena.Combat;
using RuneArena.Content;
using RuneArena.Core;

namespace RuneArena.Runes
{
    /// <summary>Between-round rune draft: 3 distinct offers per unit with rarity weights, comeback weights, per-unit pity, exclusions for owned uniques / maxed stacks.</summary>
    public sealed class RuneDraftService
    {
        private sealed class UnitState
        {
            public readonly List<RuneDefinition> Offer = new List<RuneDefinition>();
            public RuneDefinition Picked;
            public int DraftsWithoutEpic;
            public bool PityForced;
        }

        private static readonly RuneDefinition[] EmptyOffer = Array.Empty<RuneDefinition>();
        private static readonly RuneRarity[] Rarities = { RuneRarity.Common, RuneRarity.Rare, RuneRarity.Epic, RuneRarity.Legendary };

        private readonly Dictionary<Unit, UnitState> _states = new Dictionary<Unit, UnitState>();
        private readonly List<Unit> _drafting = new List<Unit>();
        private readonly IReadOnlyList<RuneDefinition> _catalog;
        private readonly Rng _rng;

        /// <summary>True between BeginDraft and EndDraft.</summary>
        public bool IsActive { get; private set; }
        public int Round { get; private set; }
        /// <summary>Units that received an offer in the current draft.</summary>
        public IReadOnlyList<Unit> Drafting => _drafting;

        public RuneDraftService() : this(null, null) { }

        /// <summary>Optional explicit rng (defaults to GameServices.Rng) and catalog (defaults to ContentCatalog.Runes) for tests.</summary>
        public RuneDraftService(Rng rng, IReadOnlyList<RuneDefinition> catalog)
        {
            _rng = rng;
            _catalog = catalog;
        }

        private Rng Random => _rng ?? GameServices.RngOrDefault();
        private IReadOnlyList<RuneDefinition> Catalog => _catalog ?? ContentCatalog.Runes;

        /// <summary>Creates offers for every unit in GameServices.World (dead units included; everyone drafts between rounds).</summary>
        public void BeginDraft(int round)
        {
            IReadOnlyList<Unit> units = GameServices.World != null ? GameServices.World.Units : (IReadOnlyList<Unit>)Array.Empty<Unit>();
            BeginDraft(round, units);
        }

        /// <summary>Creates offers for an explicit unit list.</summary>
        public void BeginDraft(int round, IReadOnlyList<Unit> units)
        {
            if (units == null) throw new ArgumentNullException(nameof(units));
            Round = round;
            IsActive = true;
            _drafting.Clear();
            for (int i = 0; i < units.Count; i++)
            {
                Unit unit = units[i];
                if (unit == null) continue;
                UnitState state = StateOf(unit);
                state.Picked = null;
                GenerateOffer(unit, state);
                _drafting.Add(unit);
            }
        }

        /// <summary>The unit's 3 offered runes (empty if no draft is active).</summary>
        public IReadOnlyList<RuneDefinition> GetOffer(Unit unit)
        {
            if (!IsActive || unit == null || !_states.TryGetValue(unit, out UnitState state)) return EmptyOffer;
            return state.Offer;
        }

        /// <summary>Picks offer[index] for the unit (adds it via RuneInventory, updates pity). Returns false if already picked or invalid.</summary>
        public bool Pick(Unit unit, int index)
        {
            if (!IsActive || unit == null || !_states.TryGetValue(unit, out UnitState state)) return false;
            if (state.Picked != null || index < 0 || index >= state.Offer.Count) return false;
            RuneDefinition rune = state.Offer[index];
            if (unit.Runes != null && !unit.Runes.Add(rune)) return false;
            state.Picked = rune;
            return true;
        }

        public bool HasPicked(Unit unit)
        {
            return unit != null && _states.TryGetValue(unit, out UnitState state) && state.Picked != null;
        }

        /// <summary>The rune the unit picked this draft, or null.</summary>
        public RuneDefinition GetPicked(Unit unit)
        {
            return unit != null && _states.TryGetValue(unit, out UnitState state) ? state.Picked : null;
        }

        /// <summary>True when every unit with an offer has picked.</summary>
        public bool AllPicked
        {
            get
            {
                if (!IsActive) return true;
                for (int i = 0; i < _drafting.Count; i++)
                {
                    UnitState state = StateOf(_drafting[i]);
                    if (state.Offer.Count > 0 && state.Picked == null) return false;
                }
                return true;
            }
        }

        /// <summary>Picks for every unit that has not picked yet (bots use their scoring; the human gets the highest-rarity card).</summary>
        public void AutoPickRemaining()
        {
            if (!IsActive) return;
            for (int i = 0; i < _drafting.Count; i++)
            {
                Unit unit = _drafting[i];
                if (HasPicked(unit)) continue;
                int index = BestIndexFor(unit);
                if (index >= 0) Pick(unit, index);
            }
        }

        /// <summary>Index the unit's automation would choose (bots: RuneScoring; human: highest rarity).</summary>
        public int BestIndexFor(Unit unit)
        {
            IReadOnlyList<RuneDefinition> offer = GetOffer(unit);
            if (offer.Count == 0) return -1;
            if (!unit.IsHuman) return RuneScoring.BestIndex(unit, offer);
            int best = 0;
            for (int i = 1; i < offer.Count; i++)
            {
                if (offer[i].Rarity > offer[best].Rarity) best = i;
            }
            return best;
        }

        /// <summary>Ends the draft and clears offers.</summary>
        public void EndDraft()
        {
            IsActive = false;
            for (int i = 0; i < _drafting.Count; i++) StateOf(_drafting[i]).Offer.Clear();
            _drafting.Clear();
        }

        /// <summary>Rarity weights (Common, Rare, Epic, Legendary) that apply to this unit right now: comeback if its team trails in round wins.</summary>
        public IReadOnlyList<float> GetWeights(Unit unit)
        {
            if (unit != null && GameServices.Match != null)
            {
                Team other = unit.Team == Team.Blue ? Team.Red : Team.Blue;
                if (GameServices.Match.Wins(unit.Team) < GameServices.Match.Wins(other)) return GameConstants.DraftWeightsComeback;
            }
            return GameConstants.DraftWeights;
        }

        /// <summary>Number of consecutive past drafts in which the unit saw no Epic+ card (pity triggers at GameConstants.DraftPityWindow).</summary>
        public int DraftsWithoutEpic(Unit unit)
        {
            return unit != null && _states.TryGetValue(unit, out UnitState state) ? state.DraftsWithoutEpic : 0;
        }

        /// <summary>True if the unit's current offer was forced to contain an Epic+ card by pity.</summary>
        public bool WasPityForced(Unit unit)
        {
            return unit != null && _states.TryGetValue(unit, out UnitState state) && state.PityForced;
        }

        /// <summary>Clears all per-unit pity/offer state (match start).</summary>
        public void Reset()
        {
            _states.Clear();
            _drafting.Clear();
            IsActive = false;
            Round = 0;
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
            state.PityForced = false;
            List<RuneDefinition> eligible = EligibleFor(unit);
            IReadOnlyList<float> weights = GetWeights(unit);
            for (int slot = 0; slot < GameConstants.DraftCardCount && eligible.Count > 0; slot++)
            {
                RuneDefinition pick = PickWeighted(eligible, weights);
                if (pick == null) break;
                state.Offer.Add(pick);
                eligible.Remove(pick);
            }
            if (state.DraftsWithoutEpic >= GameConstants.DraftPityWindow) ForceEpic(state, eligible);
            state.DraftsWithoutEpic = ContainsEpic(state.Offer) ? 0 : state.DraftsWithoutEpic + 1;
        }

        private List<RuneDefinition> EligibleFor(Unit unit)
        {
            var list = new List<RuneDefinition>();
            IReadOnlyList<RuneDefinition> catalog = Catalog;
            for (int i = 0; i < catalog.Count; i++)
            {
                RuneDefinition rune = catalog[i];
                if (unit.Runes == null || unit.Runes.CanAdd(rune)) list.Add(rune);
            }
            return list;
        }

        private RuneDefinition PickWeighted(List<RuneDefinition> eligible, IReadOnlyList<float> weights)
        {
            var rarityWeights = new float[Rarities.Length];
            for (int r = 0; r < Rarities.Length; r++)
            {
                rarityWeights[r] = HasRarity(eligible, Rarities[r]) ? weights[r] : 0f;
            }
            int rarityIndex = Random.WeightedIndex(rarityWeights);
            if (rarityIndex < 0) return null;
            var candidates = new List<RuneDefinition>();
            for (int i = 0; i < eligible.Count; i++)
            {
                if (eligible[i].Rarity == Rarities[rarityIndex]) candidates.Add(eligible[i]);
            }
            return candidates.Count > 0 ? Random.Pick(candidates) : null;
        }

        private void ForceEpic(UnitState state, List<RuneDefinition> remainingEligible)
        {
            if (ContainsEpic(state.Offer)) return;
            var epics = new List<RuneDefinition>();
            for (int i = 0; i < remainingEligible.Count; i++)
            {
                if (remainingEligible[i].Rarity >= RuneRarity.Epic) epics.Add(remainingEligible[i]);
            }
            if (epics.Count == 0) return;
            RuneDefinition forced = Random.Pick(epics);
            if (state.Offer.Count == 0) state.Offer.Add(forced);
            else state.Offer[0] = forced;
            state.PityForced = true;
        }

        private static bool ContainsEpic(List<RuneDefinition> offer)
        {
            for (int i = 0; i < offer.Count; i++)
            {
                if (offer[i].Rarity >= RuneRarity.Epic) return true;
            }
            return false;
        }

        private static bool HasRarity(List<RuneDefinition> list, RuneRarity rarity)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Rarity == rarity) return true;
            }
            return false;
        }
    }
}
