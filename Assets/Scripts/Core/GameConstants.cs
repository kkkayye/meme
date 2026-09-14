namespace RuneArena.Core
{
    /// <summary>Every tunable number from DESIGN.md in one place. No magic numbers elsewhere.</summary>
    public static class GameConstants
    {
        // ---- Match flow (section 2) ----
        public const int DefaultTeamSize = 2;
        public const int MinTeamSize = 1;
        public const int MaxTeamSize = 3;
        public const string DefaultPlayerHeroId = "blaze";
        public const int RoundsToWin = 3;
        public const int MaxRounds = 5;
        public const float RoundSeconds = 120f;
        public const float DraftSeconds = 12f;
        public const float ShopSeconds = 12f;
        public const float CountdownSeconds = 3f;
        public const float RoundEndSeconds = 3f;
        public const float BotDraftDelayMin = 1f;
        public const float BotDraftDelayMax = 3f;
        /// <summary>Round timeout tie-break: on equal points and equal HP%, Red wins (arbitrary, documented).</summary>
        public const Team TimeoutTieBreakWinner = Team.Red;

        // ---- Scoring (section 2) ----
        public const int PointsPerKill = 100;
        public const int PointsPerCaptureSecond = 10;
        public const int PointsPerMinionKill = 10;
        public const float PointsPerTowerDamage = 0.05f;

        // ---- Lane: towers (v0.2) ----
        public const float TowerX = 12f;
        public const float TowerRadius = 1.2f;
        public const float TowerHeight = 4f;
        public const float TowerHealth = 2500f;
        public const float TowerArmor = 60f;
        public const float TowerAttackDamage = 100f;
        public const float TowerAttackSpeed = 0.8f;
        public const float TowerRange = 7.5f;
        public const float TowerProjectileSpeed = 30f;
        /// <summary>Damage bonus per consecutive shot on the same hero (+25%), capped at TowerHeatMax.</summary>
        public const float TowerHeatPerShot = 0.25f;
        public const float TowerHeatMax = 1.0f;
        /// <summary>Seconds a tower keeps targeting a hero that hit an allied hero in its range.</summary>
        public const float TowerAggroSeconds = 3f;
        public const float TowerTickInterval = 0.2f;

        // ---- Lane: minions (v0.2) ----
        public const float MinionFirstWaveDelay = 4f;
        public const float MinionWaveInterval = 25f;
        public const int MinionMeleePerWave = 3;
        public const int MinionRangedPerWave = 1;
        /// <summary>+10% minion HP and AD per round after the first.</summary>
        public const float MinionScalingPerRound = 0.10f;
        public const float MinionTickInterval = 0.15f;
        public const float MinionAggroRange = 6f;
        public const float MinionHeroAggroRange = 4f;
        public const float MinionSpawnSpread = 1.2f;
        public const float MinionRadius = 0.3f;
        public const float MinionHeight = 1.2f;

        // ---- Lane economy (v0.2) ----
        public const int MinionGoldMelee = 20;
        public const int MinionGoldRanged = 25;
        /// <summary>Allied heroes near a minion kill get this fraction of its gold.</summary>
        public const float MinionGoldShareFraction = 0.4f;
        public const float MinionGoldShareRadius = 8f;
        /// <summary>Gold to every hero of the team that destroys the enemy tower.</summary>
        public const int TowerGold = 250;

        // ---- Control point (section 2) ----
        public const float ControlPointRadius = 3.0f;
        public const float CaptureProgressMax = 100f;
        public const float CaptureRatePerSecond = 20f;
        public const float CaptureDecayPerSecond = 10f;
        public const float CaptureLockoutSeconds = 10f;
        public const int CaptureGold = 150;

        // ---- Chests (sections 2, 8) ----
        public const float ChestSpawnIntervalSeconds = 20f;
        public const int MaxChestsAlive = 2;
        public const float ChestOpenRadius = 1.2f;
        public const float ChestOpenSeconds = 1.0f;
        public const float ChestSize = 0.8f;
        public const int ChestPityEvery = 4;
        public const float ChestRevealSeconds = 1.5f;
        public const int ChestGoldReward = 200;
        public const float LootWeightGold = 40f;
        public const float LootWeightCommonRune = 30f;
        public const float LootWeightRareItem = 20f;
        public const float LootWeightEpicRune = 8f;
        public const float LootWeightLegendaryItem = 2f;

        // ---- Economy (section 2) ----
        public const int StartingGold = 500;
        public const float PassiveGoldPerSecond = 4f;
        public const int KillGold = 300;
        public const int AssistGold = 150;
        public const float AssistRadius = 10f;
        public const int RoundWinGold = 400;
        public const int RoundLossGold = 250;

        // ---- Controls / casting (section 3) ----
        public const float MoveVisualSmoothing = 0.05f;
        public const float CastMoveSpeedFactor = 0.6f;
        public const float InputBufferSeconds = 0.25f;
        public const float ChannelTickInterval = 0.25f;

        // ---- Stats & damage (section 4) ----
        public const float MaxCooldownReduction = 0.40f;
        public const float MaxCritChance = 1.0f;
        public const float DefaultCritDamage = 1.75f;
        public const float ArmorConstant = 100f;
        public const float BurnTickInterval = 0.25f;

        // ---- Heroes (section 5) ----
        public const float HeroRadius = 0.5f;
        public const float HeroHeight = 2f;
        public const float BackstabDotThreshold = 0.3f;
        public const float ShadeBackstabBonus = 0.5f;
        public const float ShadeExecuteHealthThreshold = 0.35f;
        public const float ShadeExecuteMultiplier = 2f;
        public const float InvisibleOwnTeamAlpha = 0.3f;

        // ---- Runes (section 6) ----
        public const int DraftCardCount = 3;
        public const int DraftPityWindow = 3;
        public const int StackableRuneMaxStacks = 3;
        public const int RuneSetBonusCount = 3;
        public static readonly float[] DraftWeights = { 60f, 28f, 10f, 2f };
        public static readonly float[] DraftWeightsComeback = { 40f, 35f, 20f, 5f };
        public const float EmberBurnMaxHealthFraction = 0.03f;
        public const float EmberBurnDuration = 2f;
        public const float IronSetMaxHealthPercent = 0.15f;
        public const float IronSetArmorFlat = 20f;
        public const float ShadowSetBackstabBonus = 0.25f;
        public const float StormSetCooldownReset = 1f;
        public const float StormSetMoveSpeedPercent = 0.10f;
        public const string SetBonusSourcePrefix = "set:";

        // ---- Items (section 7) ----
        public const int ShopOfferCount = 5;
        public const int ShopRerollBaseCost = 100;
        public const int ShopRerollIncrement = 50;
        public const int InventorySlots = 6;
        public const float ItemSellRefundFraction = 0.7f;
        public static readonly float[] ItemTierWeights = { 60f, 30f, 10f };

        // ---- Bots (section 9) ----
        public const float BotTickInterval = 0.1f;
        public const float BotAimErrorDegrees = 8f;
        public const float BotReactionDelay = 0.25f;
        public const float BotRangedStopFactor = 0.85f;
        public const float BotDefensiveHealthFraction = 0.35f;
        public const float BotDefensiveEnemyRange = 4f;
        public const float BotUltTargetHealthFraction = 0.5f;
        public const int BotUltEnemyCount = 2;
        public const float BotRetreatHealthFraction = 0.30f;
        public const float BotRetreatSeconds = 2f;
        public const float BotCaptureNoEnemyRange = 8f;
        public const float BotLootChestRange = 12f;
        public const float BotLootNoEnemyRange = 6f;
        public const float BotAvoidanceAngle = 45f;

        // ---- UI (section 10) ----
        public const float UiReferenceWidth = 1920f;
        public const float UiReferenceHeight = 1080f;
        public const float DamageNumberSeconds = 0.8f;
        public const float DamageNumberCritScale = 1.4f;
        public const float DraftCardWidth = 300f;
        public const float DraftCardHeight = 420f;
        public const float DraftCardHoverScale = 1.05f;

        // ---- Juice (section 11) ----
        public const float HitStopSkillSeconds = 0.04f;
        public const float HitStopBasicSeconds = 0.025f;
        public const float HitStopKillSeconds = 0.09f;
        public const float HitStopTimeScale = 0.05f;
        public const float TraumaBasicHit = 0.15f;
        public const float TraumaSkillHit = 0.3f;
        public const float TraumaUlt = 0.6f;
        public const float TraumaOwnDeath = 0.8f;
        public const float TraumaDecayPerSecond = 1.5f;
        public const float ShakeMaxOffset = 0.35f;
        public const float ShakeMaxRotationDegrees = 1.5f;
        public const float HitFlashSeconds = 0.06f;
        public const float SquashStretchSeconds = 0.15f;
        public const float ProjectileVisualRadius = 0.3f;
        public const float ProjectileTrailWidth = 0.25f;
        public const float ProjectileTrailSeconds = 0.2f;
        public const float CooldownPingSeconds = 0.2f;
        public const float CooldownPingScale = 1.2f;
        public const float SfxVolume = 0.35f;
        public const float SfxPitchJitter = 0.05f;
        public const int SfxSampleRate = 44100;

        // ---- Arena & camera (section 12) ----
        public const float ArenaWidth = 44f;
        public const float ArenaDepth = 26f;
        public const float BaseX = 18f;
        public const int ObstacleLayer = 8;
        public const int ObstacleMask = 1 << ObstacleLayer;
        /// <summary>Layer of unit bodies (CharacterControllers collide with each other and with obstacles).</summary>
        public const int UnitLayer = 9;
        /// <summary>Layer a unit switches to while dashing / being knocked back: collides with obstacles only, passes through units.</summary>
        public const int DashLayer = 10;
        /// <summary>Dash hit radius around the dash path (padded by HeroRadius in queries).</summary>
        public const float DashHitRadius = 0.75f;
        public const float CameraFov = 50f;
        public const float CameraPitchDegrees = 62f;
        public const float CameraHeight = 16f;
        public const float CameraSmoothSeconds = 0.15f;
        public const float CameraCursorLead = 0.15f;
    }
}
