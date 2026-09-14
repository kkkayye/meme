using RuneArena.Combat;
using RuneArena.Items;
using RuneArena.Juice;
using RuneArena.Loot;
using RuneArena.Match;
using RuneArena.Runes;
using RuneArena.UI;

namespace RuneArena.Core
{
    /// <summary>Static service locator. MatchController populates it on match start (after Reset()); everything else reads from it. Values may be null outside a match.</summary>
    public static class GameServices
    {
        public static Rng Rng { get; set; }
        public static CombatWorld World { get; set; }
        public static MatchController Match { get; set; }
        public static Economy Economy { get; set; }
        public static RuneDraftService Draft { get; set; }
        public static ShopService Shop { get; set; }
        public static LootService Loot { get; set; }
        public static UiRoot Ui { get; set; }
        public static MatchConfig Config { get; set; }
        public static ChestSpawner Chests { get; set; }
        public static JuiceListener Juice { get; set; }
        public static Arena Arena { get; set; }
        public static Scoring Scoring { get; set; }
        public static ControlPoint ControlPoint { get; set; }

        /// <summary>True once a match populated the core services (Rng, World, Config).</summary>
        public static bool IsReady => Rng != null && World != null && Config != null;

        /// <summary>Returns the Rng or a fallback seeded with 0 so callers outside a match still work deterministically.</summary>
        public static Rng RngOrDefault()
        {
            if (Rng == null) Rng = new Rng(0);
            return Rng;
        }

        /// <summary>Clears every service reference (does NOT clear EventBus; MatchController decides that).</summary>
        public static void Reset()
        {
            Rng = null;
            World = null;
            Match = null;
            Economy = null;
            Draft = null;
            Shop = null;
            Loot = null;
            Ui = null;
            Config = null;
            Chests = null;
            Juice = null;
            Arena = null;
            Scoring = null;
            ControlPoint = null;
        }
    }
}
