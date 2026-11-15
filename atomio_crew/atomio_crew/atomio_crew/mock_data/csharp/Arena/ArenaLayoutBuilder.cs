using System.Collections.Generic;
using AtomIO.Data;
using UnityEngine;

namespace AtomIO.Arena
{
    /// <summary>
    /// Builds each level's arena: a boundary ring and the static obstacles described by the
    /// tier's arena layout (none, ring, scatter, cross, spiral, lattice). Rebuilt on every
    /// evolution, leaving a clear circle around the player so they are never trapped.
    /// Obstacle prefab: SpriteRenderer (1-unit circle) + CircleCollider2D (radius 0.5), no Rigidbody2D.
    /// </summary>
    public class ArenaLayoutBuilder : MonoBehaviour
    {
        [SerializeField] private GameObject obstaclePrefab;
        [SerializeField] private LineRenderer boundary;
        [SerializeField] private float clearRadiusAroundPlayer = 4f;
        [SerializeField] private int boundarySegments = 96;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Vector3> obstacles = new List<Vector3>(); // x, y, radius

        public float Radius { get; private set; } = 20f;

        public void Build(TierConfig config, Vector2 playerPosition)
        {
            Clear();
            var arena = config.arena;
            Radius = arena.radius;
            Color grid = TierConfig.ParseHex(arena.grid_hex, Color.gray);
            DrawBoundary(grid);

            float size = Mathf.Max(0.25f, arena.obstacle_size);
            foreach (var p in Positions(arena.obstacle_pattern, arena.obstacle_count, Radius, config.atomic_number))
            {
                if ((p - playerPosition).magnitude < clearRadiusAroundPlayer + size)
                {
                    continue;
                }

                obstacles.Add(new Vector3(p.x, p.y, size));
                if (obstaclePrefab == null)
                {
                    continue;
                }

                var go = Instantiate(obstaclePrefab, p, Quaternion.identity, transform);
                go.transform.localScale = new Vector3(size * 2f, size * 2f, 1f);
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = Color.Lerp(grid, Color.white, 0.25f);
                }

                spawned.Add(go);
            }
        }

        /// <summary>True if a point is inside (or within margin of) an obstacle or outside the arena.</summary>
        public bool IsBlocked(Vector2 point, float margin)
        {
            if (point.magnitude > Radius - margin)
            {
                return true;
            }

            foreach (var o in obstacles)
            {
                if ((point - new Vector2(o.x, o.y)).magnitude < o.z + margin)
                {
                    return true;
                }
            }

            return false;
        }

        private void Clear()
        {
            foreach (var go in spawned)
            {
                if (go != null)
                {
                    Destroy(go);
                }
            }

            spawned.Clear();
            obstacles.Clear();
        }

        private void DrawBoundary(Color color)
        {
            if (boundary == null)
            {
                return;
            }

            boundary.loop = true;
            boundary.useWorldSpace = true;
            boundary.positionCount = boundarySegments;
            boundary.startColor = color;
            boundary.endColor = color;
            for (int i = 0; i < boundarySegments; i++)
            {
                float a = 2f * Mathf.PI * i / boundarySegments;
                boundary.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Radius);
            }
        }

        /// <summary>Deterministic obstacle positions for a pattern (same level = same layout).</summary>
        private static List<Vector2> Positions(string pattern, int count, float radius, int seed)
        {
            var list = new List<Vector2>();
            if (count <= 0 || string.IsNullOrEmpty(pattern) || pattern == "none")
            {
                return list;
            }

            var rng = new System.Random(seed * 7919 + count);
            switch (pattern)
            {
                case "ring":
                    for (int i = 0; i < count; i++)
                    {
                        float a = 2f * Mathf.PI * i / count;
                        list.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * 0.55f);
                    }

                    break;
                case "cross":
                    int perArm = Mathf.Max(1, Mathf.CeilToInt(count / 4f));
                    for (int i = 0; i < count; i++)
                    {
                        int arm = i % 4;
                        int step = i / 4;
                        float d = radius * (0.25f + 0.55f * (perArm == 1 ? 0.5f : step / (perArm - 1f)));
                        float a = Mathf.PI * 0.25f + arm * Mathf.PI * 0.5f;
                        list.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d);
                    }

                    break;
                case "spiral":
                    for (int i = 0; i < count; i++)
                    {
                        float t = count == 1 ? 0.5f : i / (count - 1f);
                        float a = t * 4f * Mathf.PI;
                        list.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * (0.2f + 0.65f * t));
                    }

                    break;
                case "lattice":
                    int side = Mathf.CeilToInt(Mathf.Sqrt(count));
                    float spacing = radius * 1.3f / side;
                    float origin = -spacing * (side - 1) * 0.5f;
                    for (int i = 0; i < count; i++)
                    {
                        list.Add(new Vector2(origin + spacing * (i % side), origin + spacing * (i / side)));
                    }

                    break;
                default: // scatter
                    for (int i = 0; i < count; i++)
                    {
                        float a = (float)(rng.NextDouble() * 2.0 * Mathf.PI);
                        float d = radius * (0.2f + 0.65f * (float)rng.NextDouble());
                        list.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d);
                    }

                    break;
            }

            return list;
        }
    }
}
