using System;
using System.Collections.Generic;
using RuneArena.Core;
using RuneArena.Runes;

namespace RuneArena.Content
{
    /// <summary>Authoring of the 32 runes: 12 stackable stat runes, 16 unique mechanic runes (hooks under RuneHooks/) and 4 Legendary transforms. Built once, immutable.</summary>
    public static class RuneCatalog
    {
        public static IReadOnlyList<RuneDefinition> All { get; } = Build();

        /// <summary>Returns the rune with the id, or null.</summary>
        public static RuneDefinition Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < All.Count; i++)
            {
                if (string.Equals(All[i].Id, id, StringComparison.Ordinal)) return All[i];
            }
            return null;
        }

        private static IReadOnlyList<RuneDefinition> Build()
        {
            var list = new List<RuneDefinition>();
            AddStatRunes(list);
            AddMechanicRunes(list);
            AddLegendaryRunes(list);
            Validate(list);
            return list.ToArray();
        }

        private static void AddStatRunes(List<RuneDefinition> list)
        {
            list.Add(Stat(RuneIds.EmberHeart, "烈焰之心", "+40 AP", RuneRarity.Common, RuneSet.Ember, StatModifier.FlatOf(StatType.AbilityPower, 40f, RuneIds.EmberHeart)));
            list.Add(Stat(RuneIds.IronBones, "铁骨", "+80 HP", RuneRarity.Common, RuneSet.Iron, StatModifier.FlatOf(StatType.MaxHealth, 80f, RuneIds.IronBones)));
            list.Add(Stat(RuneIds.Gale, "疾风", "+6% MS", RuneRarity.Common, RuneSet.Storm, StatModifier.PercentOf(StatType.MoveSpeed, 0.06f, RuneIds.Gale)));
            list.Add(Stat(RuneIds.SharpEdge, "锋刃", "+12 AD", RuneRarity.Common, RuneSet.Shadow, StatModifier.FlatOf(StatType.AttackDamage, 12f, RuneIds.SharpEdge)));
            list.Add(Stat(RuneIds.Haste, "迅捷", "+12% AS", RuneRarity.Common, RuneSet.Storm, StatModifier.PercentOf(StatType.AttackSpeed, 0.12f, RuneIds.Haste)));
            list.Add(Stat(RuneIds.Plating, "护甲", "+15 Armor", RuneRarity.Common, RuneSet.Iron, StatModifier.FlatOf(StatType.Armor, 15f, RuneIds.Plating)));
            list.Add(Stat(RuneIds.Leech, "吸血", "+6% Lifesteal", RuneRarity.Common, RuneSet.Shadow, StatModifier.FlatOf(StatType.Lifesteal, 0.06f, RuneIds.Leech)));
            list.Add(Stat(RuneIds.Precision, "精准", "+8% Crit", RuneRarity.Rare, RuneSet.Shadow, StatModifier.FlatOf(StatType.CritChance, 0.08f, RuneIds.Precision)));
            list.Add(Stat(RuneIds.Cooling, "冷却", "+6% CDR", RuneRarity.Rare, RuneSet.Storm, StatModifier.FlatOf(StatType.CooldownReduction, 0.06f, RuneIds.Cooling)));
            list.Add(Stat(RuneIds.Regrowth, "再生", "+2.5 HP/s", RuneRarity.Common, RuneSet.Iron, StatModifier.FlatOf(StatType.HealthRegen, 2.5f, RuneIds.Regrowth)));
            list.Add(Stat(RuneIds.ManaSurge, "法力涌动", "+12% AP", RuneRarity.Rare, RuneSet.Ember, StatModifier.PercentOf(StatType.AbilityPower, 0.12f, RuneIds.ManaSurge)));
            list.Add(Stat(RuneIds.Giant, "巨人", "+8% Max HP", RuneRarity.Common, RuneSet.Iron, StatModifier.PercentOf(StatType.MaxHealth, 0.08f, RuneIds.Giant)));
        }

        private static void AddMechanicRunes(List<RuneDefinition> list)
        {
            list.Add(Hook(RuneIds.TripleStrike, "三连击", "每第 3 次普攻额外造成 60% 伤害。", RuneRarity.Rare, RuneSet.Shadow, () => new TripleStrikeHook()));
            list.Add(Hook(RuneIds.BurningTouch, "燃烧之触", "技能命中施加燃烧：2 秒内造成目标 4% 最大生命的魔法伤害。", RuneRarity.Rare, RuneSet.Ember, () => new BurningTouchHook()));
            list.Add(Hook(RuneIds.DashShield, "冲刺护盾", "冲刺 / 闪现后获得 10% 最大生命的护盾，持续 2 秒。", RuneRarity.Rare, RuneSet.Storm, () => new DashShieldHook()));
            list.Add(Hook(RuneIds.Hunter, "猎杀", "击杀恢复 20% 最大生命。", RuneRarity.Rare, RuneSet.Shadow, () => new HunterHook()));
            list.Add(Hook(RuneIds.Counter, "反击", "受到伤害时储存其 8%（上限 40% 最大生命）；下一次技能命中造成储存的额外伤害。", RuneRarity.Epic, RuneSet.Iron, () => new CounterHook()));
            list.Add(Hook(RuneIds.Thorns, "荆棘", "反弹所受普攻伤害的 12%（魔法）。", RuneRarity.Rare, RuneSet.Iron, () => new ThornsHook()));
            list.Add(Hook(RuneIds.LastStand, "破釜沉舟", "生命低于 30% 时 +25% AD 与 AP。", RuneRarity.Epic, RuneSet.Ember, () => new LastStandHook()));
            list.Add(Hook(RuneIds.WarFrenzy, "战争狂热", "每次普攻命中 +8% 攻速 3 秒，最多 5 层。", RuneRarity.Epic, RuneSet.Storm, () => new WarFrenzyHook()));
            list.Add(Hook(RuneIds.Frost, "冰霜", "普攻减速 20%，持续 1 秒。", RuneRarity.Rare, RuneSet.Iron, () => new FrostHook()));
            list.Add(Hook(RuneIds.Echo, "回响", "技能命中额外造成 30 魔法伤害（3 秒冷却）。", RuneRarity.Rare, RuneSet.Ember, () => new EchoHook()));
            list.Add(Hook(RuneIds.SwiftShadow, "迅影", "击杀后 +40% 移速 3 秒。", RuneRarity.Rare, RuneSet.Shadow, () => new SwiftShadowHook()));
            list.Add(Hook(RuneIds.FirstStrike, "先手", "每回合第一个技能造成 +40% 伤害。", RuneRarity.Rare, RuneSet.Shadow, () => new FirstStrikeHook()));
            list.Add(Hook(RuneIds.Tenacity, "坚韧", "眩晕 / 减速时长 -40%。", RuneRarity.Rare, RuneSet.Iron, () => new TenacityHook()));
            list.Add(Hook(RuneIds.Revival, "复苏", "回合开始获得 150 护盾，持续 5 秒。", RuneRarity.Rare, RuneSet.Iron, () => new RevivalHook()));
            list.Add(Hook(RuneIds.Siphon, "吸魂", "技能命中治疗自身 8% 的伤害量。", RuneRarity.Epic, RuneSet.Ember, () => new SiphonHook()));
            list.Add(Hook(RuneIds.FrostArmor, "冰霜护甲", "普攻命中你的敌人被减速 20%，持续 1 秒。", RuneRarity.Epic, RuneSet.Storm, () => new FrostArmorHook()));
        }

        private static void AddLegendaryRunes(List<RuneDefinition> list)
        {
            list.Add(Hook(RuneIds.DoubleCast, "双重施法", "Q / W / E 获得第二层充能。", RuneRarity.Legendary, RuneSet.Storm, () => new DoubleCastHook()));
            list.Add(Hook(RuneIds.FlameTrail, "烈焰足迹", "冲刺 / 闪现留下 3 秒火焰轨迹，每 0.25 秒造成 20 + 20% AP 魔法伤害。", RuneRarity.Legendary, RuneSet.Ember, () => new FlameTrailHook()));
            list.Add(Hook(RuneIds.SplitShot, "分裂弹", "普攻命中后向最近的 2 名其他敌人发射 30% 伤害的分裂弹。", RuneRarity.Legendary, RuneSet.Shadow, () => new SplitShotHook()));
            list.Add(Hook(RuneIds.Undying, "不死", "每回合一次：致命伤害时保留 1 点生命并获得 60% 减伤 3 秒。", RuneRarity.Legendary, RuneSet.Iron, () => new UndyingHook()));
        }

        private static RuneDefinition Stat(string id, string name, string description, RuneRarity rarity, RuneSet set, StatModifier modifier)
        {
            return new RuneDefinition
            {
                Id = id, Name = name, Description = description, Rarity = rarity, Set = set,
                Stackable = true, MaxStacks = GameConstants.StackableRuneMaxStacks,
                StatModifiers = new[] { modifier }
            };
        }

        private static RuneDefinition Hook(string id, string name, string description, RuneRarity rarity, RuneSet set, Func<ICombatHook> factory)
        {
            return new RuneDefinition
            {
                Id = id, Name = name, Description = description, Rarity = rarity, Set = set,
                Stackable = false, MaxStacks = 1, HookFactory = factory
            };
        }

        private static void Validate(List<RuneDefinition> list)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < list.Count; i++)
            {
                RuneDefinition rune = list[i];
                if (string.IsNullOrEmpty(rune.Id)) throw new InvalidOperationException("RuneCatalog: rune at index " + i + " has an empty Id.");
                if (!ids.Add(rune.Id)) throw new InvalidOperationException("RuneCatalog: duplicate rune id '" + rune.Id + "'.");
                if (rune.StatModifiers.Count == 0 && rune.HookFactory == null) throw new InvalidOperationException("RuneCatalog: rune '" + rune.Id + "' does nothing.");
            }
        }
    }
}
