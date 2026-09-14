using RuneArena.Core;
using UnityEngine;

namespace RuneArena.UI
{
    /// <summary>Shared UI palette: rarity / team / set / tier / damage colours plus panel chrome. All UI colour decisions live here.</summary>
    public static class UiStyle
    {
        // ---- Chrome ----
        public static readonly Color Dim = new Color(0.02f, 0.02f, 0.04f, 0.82f);
        public static readonly Color Opaque = new Color(0.07f, 0.08f, 0.11f, 1f);
        public static readonly Color PanelBg = new Color(0.10f, 0.11f, 0.15f, 0.97f);
        public static readonly Color CardBg = new Color(0.13f, 0.14f, 0.19f, 1f);
        public static readonly Color CardBgPicked = new Color(0.20f, 0.22f, 0.28f, 1f);
        public static readonly Color SlotBg = new Color(0.06f, 0.06f, 0.09f, 0.9f);
        public static readonly Color SlotBorder = new Color(0.30f, 0.32f, 0.40f, 1f);
        public static readonly Color ButtonNormal = new Color(0.22f, 0.25f, 0.34f, 1f);
        public static readonly Color ButtonHighlight = new Color(0.32f, 0.36f, 0.48f, 1f);
        public static readonly Color ButtonPressed = new Color(0.16f, 0.18f, 0.26f, 1f);
        public static readonly Color ButtonDisabled = new Color(0.18f, 0.18f, 0.21f, 0.6f);
        public static readonly Color ButtonSelected = new Color(0.85f, 0.62f, 0.18f, 1f);
        public static readonly Color ButtonDanger = new Color(0.45f, 0.18f, 0.18f, 1f);
        public static readonly Color TextMain = new Color(0.95f, 0.95f, 0.97f, 1f);
        public static readonly Color TextMuted = new Color(0.66f, 0.68f, 0.74f, 1f);
        public static readonly Color TextDark = new Color(0.08f, 0.08f, 0.10f, 1f);
        public static readonly Color OutlineDark = new Color(0f, 0f, 0f, 0.8f);
        public static readonly Color GoldText = new Color(1f, 0.82f, 0.25f, 1f);
        public static readonly Color HealthAlly = new Color(0.25f, 0.85f, 0.35f, 1f);
        public static readonly Color HealthEnemy = new Color(0.90f, 0.25f, 0.22f, 1f);
        public static readonly Color HealthBg = new Color(0.08f, 0.08f, 0.10f, 0.85f);
        public static readonly Color ShieldColor = new Color(0.95f, 0.95f, 1f, 0.9f);
        public static readonly Color CooldownOverlay = new Color(0f, 0f, 0f, 0.72f);
        public static readonly Color FlashWhite = new Color(1f, 1f, 1f, 0.9f);
        public static readonly Color InputBg = new Color(0.90f, 0.90f, 0.93f, 1f);
        public static readonly Color InputPlaceholder = new Color(0.45f, 0.45f, 0.50f, 1f);

        // ---- Teams ----
        public static readonly Color TeamBlue = new Color(0.30f, 0.55f, 1f, 1f);
        public static readonly Color TeamRed = new Color(1f, 0.35f, 0.30f, 1f);

        // ---- Rarity ----
        public static readonly Color RarityCommon = new Color(0.62f, 0.64f, 0.68f, 1f);
        public static readonly Color RarityRare = new Color(0.30f, 0.55f, 1f, 1f);
        public static readonly Color RarityEpic = new Color(0.68f, 0.38f, 0.95f, 1f);
        public static readonly Color RarityLegendary = new Color(1f, 0.78f, 0.20f, 1f);

        // ---- Sets ----
        public static readonly Color SetEmber = new Color(1f, 0.45f, 0.15f, 1f);
        public static readonly Color SetIron = new Color(0.55f, 0.65f, 0.80f, 1f);
        public static readonly Color SetShadow = new Color(0.60f, 0.35f, 0.85f, 1f);
        public static readonly Color SetStorm = new Color(0.30f, 0.85f, 0.95f, 1f);
        public static readonly Color SetNone = new Color(0.45f, 0.47f, 0.52f, 1f);

        // ---- Tiers ----
        public static readonly Color Tier1 = new Color(0.55f, 0.75f, 0.55f, 1f);
        public static readonly Color Tier2 = new Color(0.35f, 0.60f, 1f, 1f);
        public static readonly Color Tier3 = new Color(1f, 0.78f, 0.20f, 1f);

        // ---- Damage numbers ----
        public static readonly Color DamagePhysical = new Color(1f, 0.60f, 0.15f, 1f);
        public static readonly Color DamageMagical = new Color(0.35f, 0.90f, 1f, 1f);
        public static readonly Color DamageTrue = Color.white;
        public static readonly Color HealGreen = new Color(0.45f, 1f, 0.50f, 1f);

        // ---- Skill slot tints ----
        public static readonly Color KeyBasic = new Color(0.70f, 0.70f, 0.75f, 1f);
        public static readonly Color KeyQ = new Color(0.90f, 0.35f, 0.30f, 1f);
        public static readonly Color KeyW = new Color(0.30f, 0.55f, 0.95f, 1f);
        public static readonly Color KeyE = new Color(0.30f, 0.80f, 0.45f, 1f);
        public static readonly Color KeyR = new Color(0.95f, 0.75f, 0.20f, 1f);
        public static readonly Color KeyEmpty = new Color(0.25f, 0.25f, 0.28f, 1f);

        public static Color Rarity(RuneRarity rarity)
        {
            switch (rarity)
            {
                case RuneRarity.Rare: return RarityRare;
                case RuneRarity.Epic: return RarityEpic;
                case RuneRarity.Legendary: return RarityLegendary;
                default: return RarityCommon;
            }
        }

        public static Color TeamColor(Team team)
        {
            return team == Team.Blue ? TeamBlue : TeamRed;
        }

        public static Color SetColor(RuneSet set)
        {
            switch (set)
            {
                case RuneSet.Ember: return SetEmber;
                case RuneSet.Iron: return SetIron;
                case RuneSet.Shadow: return SetShadow;
                case RuneSet.Storm: return SetStorm;
                default: return SetNone;
            }
        }

        public static Color TierColor(ItemTier tier)
        {
            switch (tier)
            {
                case ItemTier.T2: return Tier2;
                case ItemTier.T3: return Tier3;
                default: return Tier1;
            }
        }

        public static Color DamageColor(DamageType type)
        {
            switch (type)
            {
                case DamageType.Magical: return DamageMagical;
                case DamageType.True: return DamageTrue;
                default: return DamagePhysical;
            }
        }

        public static Color KeyColor(SkillKey key)
        {
            switch (key)
            {
                case SkillKey.Q: return KeyQ;
                case SkillKey.W: return KeyW;
                case SkillKey.E: return KeyE;
                case SkillKey.R: return KeyR;
                default: return KeyBasic;
            }
        }

        /// <summary>Returns a copy of the colour with a different alpha.</summary>
        public static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        /// <summary>Returns a darkened copy (factor 0..1 multiplies rgb).</summary>
        public static Color Darken(Color color, float factor)
        {
            return new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
        }

        /// <summary>"#RRGGBB" for rich text tags.</summary>
        public static string Hex(Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        /// <summary>Wraps text in a rich-text colour tag.</summary>
        public static string Colored(string text, Color color)
        {
            return "<color=" + Hex(color) + ">" + text + "</color>";
        }
    }
}
