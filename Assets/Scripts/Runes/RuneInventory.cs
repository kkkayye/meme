using System;
using System.Collections.Generic;
using RuneArena.Combat;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Runes
{
    /// <summary>Mutable owned-rune entry: definition, current stacks and the attached hook instance (if any).</summary>
    public sealed class RuneStack
    {
        public RuneDefinition Definition { get; }
        public int Stacks { get; set; }
        public ICombatHook Hook { get; set; }

        public RuneStack(RuneDefinition definition, int stacks)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            Definition = definition;
            Stacks = stacks;
        }
    }

    /// <summary>Per-unit rune ownership: applies stat modifiers per stack, attaches hooks, tracks set counts and triggers set bonuses (RuneSets).</summary>
    [DisallowMultipleComponent]
    public sealed class RuneInventory : MonoBehaviour
    {
        private static readonly RuneSet[] AllSets = { RuneSet.Ember, RuneSet.Iron, RuneSet.Shadow, RuneSet.Storm };

        private readonly List<RuneStack> _owned = new List<RuneStack>();
        private readonly Dictionary<RuneSet, ICombatHook> _setHooks = new Dictionary<RuneSet, ICombatHook>();
        private readonly HashSet<RuneSet> _activeSets = new HashSet<RuneSet>();

        public Unit Owner { get; private set; }
        public IReadOnlyList<RuneStack> Owned => _owned;
        public int TotalRunes => _owned.Count;

        private void Awake()
        {
            Owner = GetComponent<Unit>();
        }

        /// <summary>True if the rune can still be drafted/granted (not owned if unique, below MaxStacks if stackable).</summary>
        public bool CanAdd(RuneDefinition rune)
        {
            if (rune == null) return false;
            RuneStack stack = Find(rune.Id);
            if (stack == null) return true;
            return rune.Stackable && stack.Stacks < rune.EffectiveMaxStacks;
        }

        /// <summary>Adds one stack: applies StatModifiers (SourceId = rune id + stack), attaches the hook on first stack, checks set bonuses, publishes RuneAcquired. Returns false if CanAdd is false.</summary>
        public bool Add(RuneDefinition rune)
        {
            if (!CanAdd(rune)) return false;
            if (Owner == null) Owner = GetComponent<Unit>();
            RuneStack stack = Find(rune.Id);
            if (stack == null)
            {
                stack = new RuneStack(rune, 0);
                _owned.Add(stack);
                if (rune.HookFactory != null)
                {
                    stack.Hook = rune.HookFactory();
                    stack.Hook?.Attach(Owner);
                }
            }
            stack.Stacks++;
            Owner.Stats.AddModifiers(rune.StatModifiers, StackSourceId(rune.Id, stack.Stacks));
            CheckSets();
            EventBus.Publish(new RuneAcquired(Owner, rune));
            return true;
        }

        /// <summary>Stacks owned of a rune id (0 if none).</summary>
        public int Count(string runeId)
        {
            RuneStack stack = Find(runeId);
            return stack != null ? stack.Stacks : 0;
        }

        /// <summary>True if a unique (non-stackable) rune is owned.</summary>
        public bool HasUnique(string runeId)
        {
            RuneStack stack = Find(runeId);
            return stack != null && !stack.Definition.Stackable;
        }

        /// <summary>Number of distinct runes owned that belong to the set (stacks count once).</summary>
        public int SetCount(RuneSet set)
        {
            int count = 0;
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i].Definition.Set == set) count++;
            }
            return count;
        }

        /// <summary>True once SetCount(set) reached GameConstants.RuneSetBonusCount and the bonus was applied.</summary>
        public bool IsSetActive(RuneSet set)
        {
            return _activeSets.Contains(set);
        }

        /// <summary>Detaches every hook, removes every modifier and set bonus, empties the list.</summary>
        public void ClearAll()
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                RuneStack stack = _owned[i];
                stack.Hook?.Detach();
                if (Owner != null)
                {
                    for (int s = 1; s <= stack.Stacks; s++) Owner.Stats.RemoveBySource(StackSourceId(stack.Definition.Id, s));
                }
            }
            _owned.Clear();
            foreach (KeyValuePair<RuneSet, ICombatHook> pair in _setHooks) pair.Value?.Detach();
            _setHooks.Clear();
            if (Owner != null)
            {
                foreach (RuneSet set in _activeSets) RuneSets.RemoveModifiers(Owner, set);
            }
            _activeSets.Clear();
        }

        /// <summary>Source id used for the modifiers of one stack ("rune:<id>#<stack>").</summary>
        public static string StackSourceId(string runeId, int stack)
        {
            return RuneTuning.SourceId(runeId) + "#" + stack;
        }

        private RuneStack Find(string runeId)
        {
            if (runeId == null) return null;
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i].Definition.Id == runeId) return _owned[i];
            }
            return null;
        }

        private void CheckSets()
        {
            for (int i = 0; i < AllSets.Length; i++)
            {
                RuneSet set = AllSets[i];
                if (_activeSets.Contains(set) || SetCount(set) < GameConstants.RuneSetBonusCount) continue;
                _activeSets.Add(set);
                RuneSets.ApplyModifiers(Owner, set);
                ICombatHook hook = RuneSets.CreateHook(set);
                if (hook == null) continue;
                hook.Attach(Owner);
                _setHooks[set] = hook;
            }
        }

        private void OnDestroy()
        {
            ClearAll();
        }
    }
}
