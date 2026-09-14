using System;
using System.Collections.Generic;
using RuneArena.Core;

namespace RuneArena.Content
{
    /// <summary>Authoring of the 18 items across tiers T1-T3 (passives as hooks under ItemHooks/). Built once, immutable.</summary>
    public static class ItemCatalog
    {
        public static IReadOnlyList<ItemDefinition> All { get; } = Build();

        /// <summary>Returns the item with the id, or null.</summary>
        public static ItemDefinition Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < All.Count; i++)
            {
                if (string.Equals(All[i].Id, id, StringComparison.Ordinal)) return All[i];
            }
            return null;
        }

        private static IReadOnlyList<ItemDefinition> Build()
        {
            var list = new List<ItemDefinition>();
            AddTier1(list);
            AddTier2(list);
            AddTier3(list);
            Validate(list);
            return list.ToArray();
        }

        private static void AddTier1(List<ItemDefinition> list)
        {
            list.Add(Item(ItemIds.LongSword, "长剑", "+20 AD", ItemTier.T1, 450, null, Flat(StatType.AttackDamage, 20f)));
            list.Add(Item(ItemIds.Staff, "法杖", "+35 AP", ItemTier.T1, 450, null, Flat(StatType.AbilityPower, 35f)));
            list.Add(Item(ItemIds.LeatherArmor, "皮甲", "+25 Armor", ItemTier.T1, 450, null, Flat(StatType.Armor, 25f)));
            list.Add(Item(ItemIds.Ruby, "红宝石", "+150 HP", ItemTier.T1, 450, null, Flat(StatType.MaxHealth, 150f)));
            list.Add(Item(ItemIds.Boots, "靴子", "+10% MS", ItemTier.T1, 500, null, Percent(StatType.MoveSpeed, 0.10f)));
            list.Add(Item(ItemIds.Dagger, "短剑", "+15% AS", ItemTier.T1, 450, null, Percent(StatType.AttackSpeed, 0.15f)));
            list.Add(Item(ItemIds.VampiricBlade, "吸血刀", "+8% Lifesteal", ItemTier.T1, 500, null, Flat(StatType.Lifesteal, 0.08f)));
            list.Add(Item(ItemIds.ChronoGem, "时光宝石", "+8% CDR", ItemTier.T1, 550, null, Flat(StatType.CooldownReduction, 0.08f)));
        }

        private static void AddTier2(List<ItemDefinition> list)
        {
            list.Add(Item(ItemIds.InfinityEdge, "无尽之刃", "+40 AD, +20% Crit", ItemTier.T2, 1100, null, Flat(StatType.AttackDamage, 40f), Flat(StatType.CritChance, 0.20f)));
            list.Add(Item(ItemIds.FrostHammer, "冰霜之锤", "+25 AD, +200 HP. 普攻减速 25%，持续 1.5 秒。", ItemTier.T2, 1000, () => new FrostHammerHook(), Flat(StatType.AttackDamage, 25f), Flat(StatType.MaxHealth, 200f)));
            list.Add(Item(ItemIds.Thornmail, "荆棘甲", "+60 Armor. 反弹所受伤害的 15%（魔法）。", ItemTier.T2, 1000, () => new ThornmailHook(), Flat(StatType.Armor, 60f)));
            list.Add(Item(ItemIds.Bloodthirster, "血饮", "+45 AD, +12% Lifesteal. 击杀获得 100 护盾。", ItemTier.T2, 1100, () => new BloodthirsterHook(), Flat(StatType.AttackDamage, 45f), Flat(StatType.Lifesteal, 0.12f)));
            list.Add(Item(ItemIds.PhantomDancer, "幽梦", "+30 AD, +15% AS. 击杀后 +30% 移速 3 秒。", ItemTier.T2, 950, () => new PhantomDancerHook(), Flat(StatType.AttackDamage, 30f), Percent(StatType.AttackSpeed, 0.15f)));
            list.Add(Item(ItemIds.Rabadon, "灭世", "+80 AP, +10% CDR", ItemTier.T2, 1200, null, Flat(StatType.AbilityPower, 80f), Flat(StatType.CooldownReduction, 0.10f)));
        }

        private static void AddTier3(List<ItemDefinition> list)
        {
            list.Add(Item(ItemIds.GuardianAngel, "守护天使", "+30 AD, +30 Armor. 每回合一次：死亡 2 秒后以 40% 生命复活。", ItemTier.T3, 2000, () => new GuardianAngelHook(), Flat(StatType.AttackDamage, 30f), Flat(StatType.Armor, 30f)));
            list.Add(Item(ItemIds.DeathSentence, "死刑宣告", "+50 AD. 技能命中额外造成目标 8% 已损生命的真实伤害。", ItemTier.T3, 1900, () => new DeathSentenceHook(), Flat(StatType.AttackDamage, 50f)));
            list.Add(Item(ItemIds.Archangel, "大天使", "+120 AP. 技能命中治疗 5% 伤害量。", ItemTier.T3, 2000, () => new ArchangelHook(), Flat(StatType.AbilityPower, 120f)));
            list.Add(Item(ItemIds.WitchClaw, "魔女之爪", "+90 AP. 技能命中施加燃烧：3 秒内造成 5% 最大生命的魔法伤害。", ItemTier.T3, 1900, () => new WitchClawHook(), Flat(StatType.AbilityPower, 90f)));
        }

        private static ItemDefinition Item(string id, string name, string description, ItemTier tier, int cost, Func<ICombatHook> hook, params StatModifier[] modifiers)
        {
            var list = new StatModifier[modifiers.Length];
            for (int i = 0; i < modifiers.Length; i++) list[i] = modifiers[i].WithSource(ItemSource.Of(id));
            return new ItemDefinition
            {
                Id = id, Name = name, Description = description, Tier = tier, Cost = cost,
                StatModifiers = list, HookFactory = hook
            };
        }

        private static StatModifier Flat(StatType stat, float value)
        {
            return StatModifier.FlatOf(stat, value, "item");
        }

        private static StatModifier Percent(StatType stat, float value)
        {
            return StatModifier.PercentOf(stat, value, "item");
        }

        private static void Validate(List<ItemDefinition> list)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < list.Count; i++)
            {
                ItemDefinition item = list[i];
                if (string.IsNullOrEmpty(item.Id)) throw new InvalidOperationException("ItemCatalog: item at index " + i + " has an empty Id.");
                if (!ids.Add(item.Id)) throw new InvalidOperationException("ItemCatalog: duplicate item id '" + item.Id + "'.");
                if (item.Cost <= 0) throw new InvalidOperationException("ItemCatalog: item '" + item.Id + "' must cost more than 0.");
            }
        }
    }
}
