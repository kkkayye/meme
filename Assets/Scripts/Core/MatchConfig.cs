using System;

namespace RuneArena.Core
{
    /// <summary>Immutable match configuration. Build with an object initializer; use WithSeed/WithHero to derive variants.</summary>
    public sealed class MatchConfig
    {
        /// <summary>Units per team (1..3).</summary>
        public int TeamSize { get; init; } = GameConstants.DefaultTeamSize;
        /// <summary>Gameplay seed. 0 means "pick a seed from the clock" at match start (MatchController resolves it).</summary>
        public int Seed { get; init; } = 0;
        public string PlayerHeroId { get; init; } = GameConstants.DefaultPlayerHeroId;
        public float RoundSeconds { get; init; } = GameConstants.RoundSeconds;
        public int RoundsToWin { get; init; } = GameConstants.RoundsToWin;
        /// <summary>False for all-bot matches (tests / spectate).</summary>
        public bool HumanPlayer { get; init; } = true;

        public static MatchConfig Default => new MatchConfig();

        public int ClampedTeamSize => Math.Max(GameConstants.MinTeamSize, Math.Min(GameConstants.MaxTeamSize, TeamSize));

        public MatchConfig WithSeed(int seed)
        {
            return new MatchConfig
            {
                TeamSize = TeamSize, Seed = seed, PlayerHeroId = PlayerHeroId,
                RoundSeconds = RoundSeconds, RoundsToWin = RoundsToWin, HumanPlayer = HumanPlayer
            };
        }

        public MatchConfig WithHero(string heroId)
        {
            if (heroId == null) throw new ArgumentNullException(nameof(heroId));
            return new MatchConfig
            {
                TeamSize = TeamSize, Seed = Seed, PlayerHeroId = heroId,
                RoundSeconds = RoundSeconds, RoundsToWin = RoundsToWin, HumanPlayer = HumanPlayer
            };
        }

        public MatchConfig WithTeamSize(int teamSize)
        {
            return new MatchConfig
            {
                TeamSize = teamSize, Seed = Seed, PlayerHeroId = PlayerHeroId,
                RoundSeconds = RoundSeconds, RoundsToWin = RoundsToWin, HumanPlayer = HumanPlayer
            };
        }

        public MatchConfig WithHumanPlayer(bool humanPlayer)
        {
            return new MatchConfig
            {
                TeamSize = TeamSize, Seed = Seed, PlayerHeroId = PlayerHeroId,
                RoundSeconds = RoundSeconds, RoundsToWin = RoundsToWin, HumanPlayer = humanPlayer
            };
        }
    }
}
