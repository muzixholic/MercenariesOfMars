using System;
using System.Collections.Generic;
using UnityEngine;

namespace MercenariesOfMars
{
    // X/Z 평면의 사각 격자. 경로는 출발 칸을 제외하고 목적지 칸을 포함한다.
    public sealed class BattleGrid
    {
        static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        readonly HashSet<Vector2Int> blocked = new();
        readonly Dictionary<Vector2Int, (bool walkable, int cost, int accuracy, int defense)> terrain = new();
        public int Width { get; }
        public int Height { get; }

        public BattleGrid(int width, int height, IEnumerable<Vector2Int> obstacles = null)
        {
            if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width), "격자 크기는 1 이상이어야 합니다.");
            Width = width; Height = height;
            if (obstacles != null) foreach (var cell in obstacles) blocked.Add(cell);
        }
        public bool Contains(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
        public bool IsWalkable(Vector2Int cell) => Contains(cell) && !blocked.Contains(cell) && (!terrain.TryGetValue(cell, out var t) || t.walkable);
        public int MovementCost(Vector2Int cell) => terrain.TryGetValue(cell, out var t) ? t.cost : 1;
        public int AccuracyModifier(Vector2Int cell) => terrain.TryGetValue(cell, out var t) ? t.accuracy : 0;
        public int DefenseModifier(Vector2Int cell) => terrain.TryGetValue(cell, out var t) ? t.defense : 0;
        public void SetTerrain(Vector2Int cell, bool walkable, int cost, int accuracy = 0, int defense = 0)
        {
            if (Contains(cell)) terrain[cell] = (walkable, Mathf.Max(1, cost), Mathf.Clamp(accuracy, -100, 100), defense);
        }
        public static int Distance(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        public Dictionary<Vector2Int, List<Vector2Int>> Reachable(Vector2Int start, int budget, Func<Vector2Int, bool> occupied = null)
        {
            var paths = new Dictionary<Vector2Int, List<Vector2Int>>();
            if (!IsWalkable(start) || budget < 0) return paths;
            var costs = new Dictionary<Vector2Int, int> { [start] = 0 };
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            var queue = new SortedSet<(int cost, int order, Vector2Int cell)>(Comparer<(int cost, int order, Vector2Int cell)>.Create((a, b) =>
            {
                int byCost = a.cost.CompareTo(b.cost); return byCost != 0 ? byCost : a.order.CompareTo(b.order);
            }));
            int order = 0; queue.Add((0, order++, start));
            while (queue.Count > 0)
            {
                var entry = queue.Min; queue.Remove(entry); var current = entry.cell;
                if (entry.cost != costs[current]) continue;
                foreach (var direction in Directions)
                {
                    var next = current + direction;
                    if (!IsWalkable(next) || (occupied?.Invoke(next) ?? false)) continue;
                    long cost = (long)entry.cost + MovementCost(next);
                    if (cost > budget || (costs.TryGetValue(next, out int known) && known <= cost)) continue;
                    costs[next] = (int)cost; previous[next] = current;
                    queue.Add(((int)cost, order++, next));
                }
            }
            foreach (var cell in costs.Keys)
            {
                var path = new List<Vector2Int>(); var cursor = cell;
                while (cursor != start) { path.Add(cursor); cursor = previous[cursor]; }
                path.Reverse(); paths[cell] = path;
            }
            return paths;
        }
    }
}
