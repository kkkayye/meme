using System;
using System.Collections.Generic;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Match
{
    /// <summary>Builds the 44 x 26 arena from primitives (floor, walls, symmetric pillars, bases) and answers walkability / clamping / spawn queries.</summary>
    public sealed class Arena : MonoBehaviour
    {
        private struct PillarSpec
        {
            public Vector2 Center;
            public Vector2 Size;
            public PillarSpec(float x, float z, float w, float d) { Center = new Vector2(x, z); Size = new Vector2(w, d); }
        }

        private static readonly Color FloorColor = new Color(0.16f, 0.18f, 0.22f, 1f);
        private static readonly Color WallColor = new Color(0.30f, 0.32f, 0.38f, 1f);
        private static readonly Color PillarColor = new Color(0.42f, 0.40f, 0.48f, 1f);
        private static readonly Color BlueBase = new Color(0.30f, 0.55f, 1f, 0.35f);
        private static readonly Color RedBase = new Color(1f, 0.35f, 0.30f, 0.35f);
        private const float WallHeight = 2f;
        private const float WallThickness = 1f;
        private const float PillarHeight = 2.2f;
        private const float BaseRadius = 3f;
        private const float BaseKeepOut = 4.5f;
        private const int RandomPointTries = 60;

        /// <summary>Symmetric obstacle layout (8 pillars).</summary>
        private static readonly PillarSpec[] Pillars =
        {
            new PillarSpec(-7f, 6f, 2f, 2f), new PillarSpec(7f, 6f, 2f, 2f),
            new PillarSpec(-7f, -6f, 2f, 2f), new PillarSpec(7f, -6f, 2f, 2f),
            new PillarSpec(-13f, 6.5f, 1.5f, 2.5f), new PillarSpec(13f, 6.5f, 1.5f, 2.5f),
            new PillarSpec(-13f, -6.5f, 1.5f, 2.5f), new PillarSpec(13f, -6.5f, 1.5f, 2.5f),
            new PillarSpec(0f, 9.5f, 4f, 1.5f), new PillarSpec(0f, -9.5f, 4f, 1.5f)
        };

        private readonly List<Collider> _obstacles = new List<Collider>();
        private readonly List<Bounds> _obstacleBounds = new List<Bounds>();
        private readonly Dictionary<object, Bounds> _dynamicObstacles = new Dictionary<object, Bounds>();
        private readonly List<Material> _materials = new List<Material>();
        private Transform _root;

        /// <summary>Physics mask of the obstacle layer (layer 8).</summary>
        public static readonly int ObstacleMask = GameConstants.ObstacleMask;

        public Vector3 Center { get; private set; } = Vector3.zero;
        /// <summary>Half extents on X and Z (22, 13).</summary>
        public Vector2 HalfSize { get; private set; } = new Vector2(GameConstants.ArenaWidth * 0.5f, GameConstants.ArenaDepth * 0.5f);
        /// <summary>Colliders of walls and pillars.</summary>
        public IReadOnlyList<Collider> Obstacles => _obstacles;
        public bool IsBuilt { get; private set; }

        /// <summary>Creates floor, boundary walls, 8 pillars and base markers. Idempotent.</summary>
        public void Build()
        {
            if (IsBuilt) return;
            _root = new GameObject("ArenaRoot").transform;
            _root.SetParent(transform, false);
            BuildFloor();
            BuildWalls();
            BuildPillars();
            BuildBases();
            IsBuilt = true;
        }

        /// <summary>Destroys everything Build created.</summary>
        public void Clear()
        {
            if (_root != null) PrimitiveFactory.SafeDestroy(_root.gameObject);
            _root = null;
            _obstacles.Clear();
            _obstacleBounds.Clear();
            _dynamicObstacles.Clear();
            for (int i = 0; i < _materials.Count; i++)
            {
                PrimitiveFactory.SafeDestroy(_materials[i]);
            }
            _materials.Clear();
            IsBuilt = false;
        }

        /// <summary>True if the point is inside the bounds and not inside an obstacle (hero radius padding).</summary>
        public bool IsWalkable(Vector3 position)
        {
            float pad = GameConstants.HeroRadius;
            if (Mathf.Abs(position.x - Center.x) > HalfSize.x - pad) return false;
            if (Mathf.Abs(position.z - Center.z) > HalfSize.y - pad) return false;
            Vector3 probe = new Vector3(position.x, 1f, position.z);
            for (int i = 0; i < _obstacleBounds.Count; i++)
            {
                Bounds b = _obstacleBounds[i];
                b.Expand(pad * 2f);
                if (b.Contains(probe)) return false;
            }
            foreach (KeyValuePair<object, Bounds> pair in _dynamicObstacles)
            {
                Bounds b = pair.Value;
                b.Expand(pad * 2f);
                if (b.Contains(probe)) return false;
            }
            return true;
        }

        /// <summary>Registers a runtime obstacle (a tower) for walkability queries.</summary>
        public void AddDynamicObstacle(object key, Bounds bounds)
        {
            if (key == null) return;
            _dynamicObstacles[key] = bounds;
        }

        public void RemoveDynamicObstacle(object key)
        {
            if (key == null) return;
            _dynamicObstacles.Remove(key);
        }

        /// <summary>Lane direction a team pushes in: Blue toward +X, Red toward -X.</summary>
        public static Vector3 LaneDirection(Team team)
        {
            return team == Team.Blue ? Vector3.right : Vector3.left;
        }

        /// <summary>Position of a team's tower (in front of its base on the lane).</summary>
        public Vector3 TowerPosition(Team team)
        {
            float x = team == Team.Blue ? -GameConstants.TowerX : GameConstants.TowerX;
            return new Vector3(Center.x + x, 0f, Center.z);
        }

        /// <summary>Spawn point for the i-th minion of a wave: just in front of the base, spread on Z.</summary>
        public Vector3 MinionSpawnPoint(Team team, int index, int count)
        {
            Vector3 b = BasePosition(team) + LaneDirection(team) * 2.5f;
            float z = (index - (count - 1) * 0.5f) * GameConstants.MinionSpawnSpread;
            return new Vector3(b.x, 0f, b.z + z);
        }

        /// <summary>Clamps to the floor bounds (hero radius padding) with y = 0.</summary>
        public Vector3 ClampToArena(Vector3 position)
        {
            float pad = GameConstants.HeroRadius;
            position.x = Mathf.Clamp(position.x, Center.x - HalfSize.x + pad, Center.x + HalfSize.x - pad);
            position.z = Mathf.Clamp(position.z, Center.z - HalfSize.y + pad, Center.z + HalfSize.y - pad);
            position.y = 0f;
            return position;
        }

        /// <summary>A random walkable point away from both bases (rejection sampling with rng).</summary>
        public Vector3 RandomWalkablePoint(Rng rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            float padX = HalfSize.x - 2f;
            float padZ = HalfSize.y - 2f;
            for (int i = 0; i < RandomPointTries; i++)
            {
                var p = new Vector3(Center.x + rng.Range(-padX, padX), 0f, Center.z + rng.Range(-padZ, padZ));
                if (!IsWalkable(p)) continue;
                if (Vector3.Distance(p, BasePosition(Team.Blue)) < BaseKeepOut) continue;
                if (Vector3.Distance(p, BasePosition(Team.Red)) < BaseKeepOut) continue;
                return p;
            }
            return Center;
        }

        /// <summary>Spawn point for a team member: Blue base at x = -18, Red at x = +18, spread on Z by index.</summary>
        public Vector3 SpawnPoint(Team team, int index)
        {
            Vector3 b = BasePosition(team);
            return new Vector3(b.x, 0f, b.z + (index - 1) * 2f);
        }

        /// <summary>Center of a team's base.</summary>
        public Vector3 BasePosition(Team team)
        {
            float x = team == Team.Blue ? -GameConstants.BaseX : GameConstants.BaseX;
            return new Vector3(Center.x + x, 0f, Center.z);
        }

        private void BuildFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(_root, false);
            floor.transform.position = Center;
            floor.transform.localScale = new Vector3(GameConstants.ArenaWidth / 10f, 1f, GameConstants.ArenaDepth / 10f);
            PrimitiveFactory.RemoveCollider(floor);
            floor.GetComponent<Renderer>().sharedMaterial = Track(PrimitiveFactory.Lit(FloorColor));
        }

        private void BuildWalls()
        {
            Material wall = Track(PrimitiveFactory.Lit(WallColor));
            float hx = HalfSize.x + WallThickness * 0.5f;
            float hz = HalfSize.y + WallThickness * 0.5f;
            float y = WallHeight * 0.5f;
            AddObstacle(PrimitiveFactory.Cube("WallNorth", _root, Center + new Vector3(0f, y, hz), new Vector3(GameConstants.ArenaWidth + WallThickness * 2f, WallHeight, WallThickness), wall, GameConstants.ObstacleLayer));
            AddObstacle(PrimitiveFactory.Cube("WallSouth", _root, Center + new Vector3(0f, y, -hz), new Vector3(GameConstants.ArenaWidth + WallThickness * 2f, WallHeight, WallThickness), wall, GameConstants.ObstacleLayer));
            AddObstacle(PrimitiveFactory.Cube("WallEast", _root, Center + new Vector3(hx, y, 0f), new Vector3(WallThickness, WallHeight, GameConstants.ArenaDepth), wall, GameConstants.ObstacleLayer));
            AddObstacle(PrimitiveFactory.Cube("WallWest", _root, Center + new Vector3(-hx, y, 0f), new Vector3(WallThickness, WallHeight, GameConstants.ArenaDepth), wall, GameConstants.ObstacleLayer));
        }

        private void BuildPillars()
        {
            Material pillar = Track(PrimitiveFactory.Lit(PillarColor));
            for (int i = 0; i < Pillars.Length; i++)
            {
                PillarSpec spec = Pillars[i];
                Vector3 center = Center + new Vector3(spec.Center.x, PillarHeight * 0.5f, spec.Center.y);
                AddObstacle(PrimitiveFactory.Cube("Pillar" + i, _root, center, new Vector3(spec.Size.x, PillarHeight, spec.Size.y), pillar, GameConstants.ObstacleLayer));
            }
        }

        private void BuildBases()
        {
            PrimitiveFactory.Disc("BaseBlue", _root, BasePosition(Team.Blue) + new Vector3(0f, 0.02f, 0f), BaseRadius, 0.02f, Track(PrimitiveFactory.Unlit(BlueBase)));
            PrimitiveFactory.Disc("BaseRed", _root, BasePosition(Team.Red) + new Vector3(0f, 0.02f, 0f), BaseRadius, 0.02f, Track(PrimitiveFactory.Unlit(RedBase)));
        }

        /// <summary>Registers an axis-aligned obstacle; bounds come from the transform (Collider.bounds may lag until the next physics sync).</summary>
        private void AddObstacle(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider == null) return;
            _obstacles.Add(collider);
            _obstacleBounds.Add(new Bounds(go.transform.position, go.transform.localScale));
        }

        private Material Track(Material material)
        {
            _materials.Add(material);
            return material;
        }
    }
}
