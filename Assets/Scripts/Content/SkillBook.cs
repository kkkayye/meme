using System;
using System.Collections.Generic;
using RuneArena.Core;

namespace RuneArena.Content
{
    /// <summary>Authoring of the 15 hero skills (basic + Q/W/E/R for Blaze, Vanguard, Shade) as immutable SkillDefinitions; every number comes straight from DESIGN.md section 5.</summary>
    public static class SkillBook
    {
        /// <summary>Hit radius used by projectile shapes (matches the projectile visual; CombatWorld pads it by HeroRadius).</summary>
        public const float ProjectileHitRadius = GameConstants.ProjectileVisualRadius;
        /// <summary>Hit radius used by dash shapes ("enemies passed through"; CombatWorld pads it by HeroRadius).</summary>
        public const float DashHitRadius = GameConstants.DashHitRadius;

        // Blaze R: self damage reduction while channeling; Blaze W: brief invulnerability + speed.
        private const float InfernoChannelSeconds = 2.5f;
        private const float BlinkInvulnerabilitySeconds = 0.2f;
        private const float FullDamageReduction = 1f;

        // ================================================================
        // Blaze 烈焰 - Ranged Mage
        // ================================================================

        /// <summary>火焰弹 Fire Bolt: single projectile, 100% AD Physical, speed 22.</summary>
        public static SkillDefinition BlazeBasic { get; } = new SkillDefinition
        {
            Id = "blaze_basic", Name = "Fire Bolt",
            Description = "火焰弹 Fire Bolt — 向光标方向发射火球，命中首个敌人，造成 100% AD 物理伤害。",
            Key = SkillKey.Basic, Shape = SkillShape.Projectile, AiHint = AiHint.None,
            Windup = 0.12f, Recovery = 0.1f, Cooldown = 0f,
            Range = 7f, Radius = ProjectileHitRadius, Speed = 22f,
            BaseDamage = 0f, AdRatio = 1f, ApRatio = 0f, DamageType = DamageType.Physical
        };

        /// <summary>烈焰冲击 Flame Wave: 70° cone, range 5, 70 + 60% AP Magical, cd 4.</summary>
        public static SkillDefinition BlazeQ { get; } = new SkillDefinition
        {
            Id = "blaze_q", Name = "Flame Wave",
            Description = "烈焰冲击 Flame Wave — 70° 锥形喷射火焰（射程 5），造成 70 + 60% AP 魔法伤害。",
            Key = SkillKey.Q, Shape = SkillShape.Cone, AiHint = AiHint.Poke,
            Windup = 0.15f, Recovery = 0.15f, Cooldown = 4f,
            Range = 5f, Angle = 70f,
            BaseDamage = 70f, AdRatio = 0f, ApRatio = 0.6f, DamageType = DamageType.Magical
        };

        /// <summary>闪现 Blink: teleport 5 toward aim, 0.2 s invulnerability (100% DR) + 30% speed for 1.5 s, cd 9.</summary>
        public static SkillDefinition BlazeW { get; } = new SkillDefinition
        {
            Id = "blaze_w", Name = "Blink",
            Description = "闪现 Blink — 向光标方向瞬移 5 格，获得 0.2 秒无敌与 30% 移速加成（1.5 秒）。",
            Key = SkillKey.W, Shape = SkillShape.Blink, AiHint = AiHint.Escape,
            Windup = 0.05f, Recovery = 0.1f, Cooldown = 9f,
            Range = 5f,
            Effects = Fx(
                SkillEffect.DamageReduction(FullDamageReduction, BlinkInvulnerabilitySeconds),
                SkillEffect.SpeedBoost(0.3f, 1.5f))
        };

        /// <summary>陨石 Meteor: ground circle, range 9, radius 2.2, delay 0.8, 140 + 80% AP Magical, Slow 40% 1.5 s, cd 8.</summary>
        public static SkillDefinition BlazeE { get; } = new SkillDefinition
        {
            Id = "blaze_e", Name = "Meteor",
            Description = "陨石 Meteor — 0.8 秒后在目标点（射程 9，半径 2.2）降下陨石，造成 140 + 80% AP 魔法伤害并减速 40%（1.5 秒）。",
            Key = SkillKey.E, Shape = SkillShape.Circle, AiHint = AiHint.Burst,
            Windup = 0.15f, Recovery = 0.15f, Cooldown = 8f,
            Range = 9f, Radius = 2.2f, Delay = 0.8f,
            BaseDamage = 140f, AdRatio = 0f, ApRatio = 0.8f, DamageType = DamageType.Magical,
            Effects = Fx(SkillEffect.Slow(0.4f, 1.5f))
        };

        /// <summary>炼狱 Inferno: 2.5 s rooted channel, radius 4, 30 + 25% AP Magical per 0.25 s tick, self 30% DR while channeling, cd 45.</summary>
        public static SkillDefinition BlazeR { get; } = new SkillDefinition
        {
            Id = "blaze_r", Name = "Inferno",
            Description = "炼狱 Inferno — 定身引导 2.5 秒，每 0.25 秒对半径 4 内敌人造成 30 + 25% AP 魔法伤害，引导期间自身减伤 30%。",
            Key = SkillKey.R, Shape = SkillShape.Channel, AiHint = AiHint.Ult,
            Windup = 0.2f, Recovery = 0.2f, Cooldown = 45f,
            Range = 4f, Radius = 4f, Delay = InfernoChannelSeconds,
            BaseDamage = 30f, AdRatio = 0f, ApRatio = 0.25f, DamageType = DamageType.Magical,
            Effects = Fx(SkillEffect.DamageReduction(0.3f, InfernoChannelSeconds))
        };

        // ================================================================
        // Vanguard 铁壁 - Melee Bruiser
        // ================================================================

        /// <summary>重击 Heavy Strike: 90° melee arc, range 2.2, 100% AD Physical.</summary>
        public static SkillDefinition VanguardBasic { get; } = new SkillDefinition
        {
            Id = "vanguard_basic", Name = "Heavy Strike",
            Description = "重击 Heavy Strike — 90° 近战挥击（范围 2.2），造成 100% AD 物理伤害。",
            Key = SkillKey.Basic, Shape = SkillShape.Melee, AiHint = AiHint.None,
            Windup = 0.1f, Recovery = 0.15f, Cooldown = 0f,
            Range = 2.2f, Angle = 90f,
            BaseDamage = 0f, AdRatio = 1f, ApRatio = 0f, DamageType = DamageType.Physical
        };

        /// <summary>冲锋 Charge: dash 6 at speed 20, 60 + 70% AD Physical, Knockback 3, cd 9.</summary>
        public static SkillDefinition VanguardQ { get; } = new SkillDefinition
        {
            Id = "vanguard_q", Name = "Charge",
            Description = "冲锋 Charge — 向前冲刺 6 格，途经敌人受到 60 + 70% AD 物理伤害并被击退 3 格。",
            Key = SkillKey.Q, Shape = SkillShape.Dash, AiHint = AiHint.Engage,
            Windup = 0.08f, Recovery = 0.1f, Cooldown = 9f,
            Range = 6f, Radius = DashHitRadius, Speed = 20f,
            BaseDamage = 60f, AdRatio = 0.7f, ApRatio = 0f, DamageType = DamageType.Physical,
            Effects = Fx(SkillEffect.Knockback(3f))
        };

        /// <summary>盾击 Shield Bash: 60° cone, range 2.5, 50 + 60% AD Physical, Stun 0.8 s, windup 0.2, cd 8.</summary>
        public static SkillDefinition VanguardW { get; } = new SkillDefinition
        {
            Id = "vanguard_w", Name = "Shield Bash",
            Description = "盾击 Shield Bash — 60° 锥形盾击（范围 2.5），造成 50 + 60% AD 物理伤害并眩晕 0.8 秒。",
            Key = SkillKey.W, Shape = SkillShape.Cone, AiHint = AiHint.Burst,
            Windup = 0.2f, Recovery = 0.15f, Cooldown = 8f,
            Range = 2.5f, Angle = 60f,
            BaseDamage = 50f, AdRatio = 0.6f, ApRatio = 0f, DamageType = DamageType.Physical,
            Effects = Fx(SkillEffect.Stun(0.8f))
        };

        /// <summary>铁甲 Iron Skin: self shield 120 + 20% MaxHealth for 3 s and 20% DR for 3 s, cd 14.</summary>
        public static SkillDefinition VanguardE { get; } = new SkillDefinition
        {
            Id = "vanguard_e", Name = "Iron Skin",
            Description = "铁甲 Iron Skin — 获得 120 + 20% 最大生命值的护盾并减伤 20%，持续 3 秒。",
            Key = SkillKey.E, Shape = SkillShape.Buff, AiHint = AiHint.Defensive,
            Windup = 0.05f, Recovery = 0.1f, Cooldown = 14f,
            Range = 0f,
            Effects = Fx(
                SkillEffect.Shield(120f, 3f, 0.2f),
                SkillEffect.DamageReduction(0.2f, 3f))
        };

        /// <summary>地震 Earthquake: self circle radius 4.5, 180 + 100% AD Physical, Slow 50% 2 s, Knockback 2, windup 0.35, cd 50.</summary>
        public static SkillDefinition VanguardR { get; } = new SkillDefinition
        {
            Id = "vanguard_r", Name = "Earthquake",
            Description = "地震 Earthquake — 蓄力 0.35 秒后震击半径 4.5 内敌人，造成 180 + 100% AD 物理伤害，减速 50%（2 秒）并击退 2 格。",
            Key = SkillKey.R, Shape = SkillShape.SelfCircle, AiHint = AiHint.Ult,
            Windup = 0.35f, Recovery = 0.3f, Cooldown = 50f,
            Range = 4.5f, Radius = 4.5f,
            BaseDamage = 180f, AdRatio = 1f, ApRatio = 0f, DamageType = DamageType.Physical,
            Effects = Fx(
                SkillEffect.Slow(0.5f, 2f),
                SkillEffect.Knockback(2f))
        };

        // ================================================================
        // Shade 影刃 - Melee Assassin
        // ================================================================

        /// <summary>影刃斩 Shadow Slash: 70° melee arc, range 1.8, 100% AD Physical, very fast.</summary>
        public static SkillDefinition ShadeBasic { get; } = new SkillDefinition
        {
            Id = "shade_basic", Name = "Shadow Slash",
            Description = "影刃斩 Shadow Slash — 70° 近战快斩（范围 1.8），造成 100% AD 物理伤害。",
            Key = SkillKey.Basic, Shape = SkillShape.Melee, AiHint = AiHint.None,
            Windup = 0.06f, Recovery = 0.08f, Cooldown = 0f,
            Range = 1.8f, Angle = 70f,
            BaseDamage = 0f, AdRatio = 1f, ApRatio = 0f, DamageType = DamageType.Physical
        };

        /// <summary>影步 Shadow Step: dash 5.5 at speed 26, 50 + 80% AD Physical; backstab (+50%) is resolved by the executor via GameConstants.ShadeBackstabBonus, cd 7.</summary>
        public static SkillDefinition ShadeQ { get; } = new SkillDefinition
        {
            Id = "shade_q", Name = "Shadow Step",
            Description = "影步 Shadow Step — 向前突进 5.5 格，途经敌人受到 50 + 80% AD 物理伤害；从背后命中额外 +50%。",
            Key = SkillKey.Q, Shape = SkillShape.Dash, AiHint = AiHint.Engage,
            Windup = 0.05f, Recovery = 0.1f, Cooldown = 7f,
            Range = 5.5f, Radius = DashHitRadius, Speed = 26f,
            BaseDamage = 50f, AdRatio = 0.8f, ApRatio = 0f, DamageType = DamageType.Physical
        };

        /// <summary>飞刀 Fan of Knives: 3 projectiles over 30°, speed 24, range 8, each 40 + 45% AD Physical, cd 6.</summary>
        public static SkillDefinition ShadeW { get; } = new SkillDefinition
        {
            Id = "shade_w", Name = "Fan of Knives",
            Description = "飞刀 Fan of Knives — 在 30° 内掷出 3 把飞刀（射程 8），每把造成 40 + 45% AD 物理伤害。",
            Key = SkillKey.W, Shape = SkillShape.Fan, AiHint = AiHint.Poke,
            Windup = 0.1f, Recovery = 0.12f, Cooldown = 6f,
            Range = 8f, Radius = ProjectileHitRadius, Angle = 30f, Speed = 24f, ProjectileCount = 3,
            BaseDamage = 40f, AdRatio = 0.45f, ApRatio = 0f, DamageType = DamageType.Physical
        };

        /// <summary>烟雾 Smoke: Invisible 2.5 s + 40% speed 2.5 s; does not break its own invisibility, cd 16.</summary>
        public static SkillDefinition ShadeE { get; } = new SkillDefinition
        {
            Id = "shade_e", Name = "Smoke",
            Description = "烟雾 Smoke — 隐身 2.5 秒并获得 40% 移速加成；攻击会解除隐身。",
            Key = SkillKey.E, Shape = SkillShape.Buff, AiHint = AiHint.Escape,
            Windup = 0.05f, Recovery = 0.1f, Cooldown = 16f,
            Range = 0f, BreaksInvisibility = false,
            Effects = Fx(
                SkillEffect.Invisible(2.5f),
                SkillEffect.SpeedBoost(0.4f, 2.5f))
        };

        /// <summary>处决 Execute: 60° melee arc, range 2.5, 150 + 120% AD True; x2 below 35% HP is resolved by the executor via GameConstants.ShadeExecute*, cd 40.</summary>
        public static SkillDefinition ShadeR { get; } = new SkillDefinition
        {
            Id = "shade_r", Name = "Execute",
            Description = "处决 Execute — 60° 近战重斩（范围 2.5），造成 150 + 120% AD 真实伤害；目标生命低于 35% 时伤害翻倍。",
            Key = SkillKey.R, Shape = SkillShape.Melee, AiHint = AiHint.Burst,
            Windup = 0.12f, Recovery = 0.2f, Cooldown = 40f,
            Range = 2.5f, Angle = 60f,
            BaseDamage = 150f, AdRatio = 1.2f, ApRatio = 0f, DamageType = DamageType.True
        };

        // ================================================================
        // Helpers
        // ================================================================

        /// <summary>True for effects a skill applies to its CASTER (Shield, Heal, Invisible, DamageReduction, SpeedBoost); false for effects applied to enemies hit (Knockback, Pull, Stun, Slow). Lets the executor route Effects of damaging skills such as Blaze R (self DR) correctly.</summary>
        public static bool IsSelfEffect(SkillEffectType type)
        {
            switch (type)
            {
                case SkillEffectType.Shield:
                case SkillEffectType.Heal:
                case SkillEffectType.Invisible:
                case SkillEffectType.DamageReduction:
                case SkillEffectType.SpeedBoost:
                case SkillEffectType.DamageAmp:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Immutable effect list from a params array (copies are never handed out as a mutable array).</summary>
        private static IReadOnlyList<SkillEffect> Fx(params SkillEffect[] effects)
        {
            if (effects == null || effects.Length == 0) return Array.Empty<SkillEffect>();
            return Array.AsReadOnly(effects);
        }
    }
}
