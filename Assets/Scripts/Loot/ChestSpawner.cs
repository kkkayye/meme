using System;
using System.Collections.Generic;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Loot
{
    /// <summary>Spawns chests: periodic random spawns during Combat (every 20 s, max 2 alive) and on-demand spawns (control point capture).</summary>
    public sealed class ChestSpawner : MonoBehaviour
    {
        private readonly List<Chest> _alive = new List<Chest>();

        public IReadOnlyList<Chest> Alive => _alive;
        /// <summary>Seconds until the next periodic spawn attempt.</summary>
        public float TimeToNextSpawn { get; private set; } = GameConstants.ChestSpawnIntervalSeconds;

        /// <summary>Creates a chest at the position (registers with CombatWorld, publishes ChestSpawned). Returns null if the max-alive cap is reached.</summary>
        public Chest SpawnAt(Vector3 position)
        {
            _alive.RemoveAll(c => c == null);
            if (_alive.Count >= GameConstants.MaxChestsAlive) return null;
            var go = new GameObject("Chest");
            go.transform.SetParent(transform, false);
            Chest chest = go.AddComponent<Chest>();
            chest.Setup(position);
            _alive.Add(chest);
            GameServices.World?.RegisterChest(chest);
            EventBus.Publish(new ChestSpawned(chest));
            return chest;
        }

        /// <summary>SpawnAt a random walkable arena point (GameServices.Rng).</summary>
        public Chest SpawnRandom()
        {
            Vector3 point = Vector3.zero;
            if (GameServices.Arena != null) point = GameServices.Arena.RandomWalkablePoint(GameServices.RngOrDefault());
            return SpawnAt(point);
        }

        /// <summary>Advances the periodic spawn timer; MatchController calls this only during Combat.</summary>
        public void Tick(float dt)
        {
            TimeToNextSpawn -= dt;
            if (TimeToNextSpawn > 0f) return;
            TimeToNextSpawn = GameConstants.ChestSpawnIntervalSeconds;
            _alive.RemoveAll(c => c == null);
            if (_alive.Count < GameConstants.MaxChestsAlive) SpawnRandom();
        }

        /// <summary>Restarts the periodic timer (round start).</summary>
        public void ResetTimer()
        {
            TimeToNextSpawn = GameConstants.ChestSpawnIntervalSeconds;
        }

        /// <summary>Despawns every chest (round end / match end).</summary>
        public void ClearAll()
        {
            var copy = new List<Chest>(_alive);
            _alive.Clear();
            for (int i = 0; i < copy.Count; i++)
            {
                if (copy[i] != null) copy[i].Despawn();
            }
        }

        /// <summary>Called by Chest.Despawn to drop itself from Alive.</summary>
        public void Forget(Chest chest)
        {
            if (chest == null) return;
            _alive.Remove(chest);
        }
    }
}
