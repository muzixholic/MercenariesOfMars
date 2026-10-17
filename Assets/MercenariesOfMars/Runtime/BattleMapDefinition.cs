using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MercenariesOfMars
{
    [Serializable] public sealed class TerrainCell
    {
        [InspectorName("칸")] public Vector2Int cell;
        [InspectorName("지형")] public TerrainDefinition terrain;
    }
    [Serializable] public sealed class StructurePlacement
    {
        [InspectorName("기준 칸")] public Vector2Int cell;
        [InspectorName("구조물")] public StructureDefinition structure;
        public IEnumerable<Vector2Int> Cells()
        {
            if (structure == null) yield break;
            for (int y = 0; y < Mathf.Clamp(structure.size.y, 1, 64); y++)
                for (int x = 0; x < Mathf.Clamp(structure.size.x, 1, 64); x++) yield return cell + new Vector2Int(x, y);
        }
    }
    [Serializable] public sealed class MapEventRegion
    {
        [InspectorName("구역 이름")] public string label = "이벤트 구역";
        [InspectorName("영역")] public RectInt area = new(0, 0, 1, 1);
        [InspectorName("실행 이벤트（아군 진입 시 1회）")] public EventSequence sequence;
    }
    public enum MapVictoryCondition
    {
        [InspectorName("적 전멸")] EliminateEnemies,
        [InspectorName("아군이 목표 칸 도달")] ReachCell
    }
    [CreateAssetMenu(menuName = "화성의 용병단/전투 맵", fileName = "새 맵")]
    public sealed class BattleMapDefinition : ScriptableObject
    {
        [InspectorName("맵 이름")] public string displayName = "새 맵";
        [Range(1, 64), InspectorName("가로 칸 수")] public int width = 10;
        [Range(1, 64), InspectorName("세로 칸 수")] public int height = 8;
        [Min(.1f), InspectorName("칸 크기")] public float cellSize = 1;
        [InspectorName("기본 지형（미지정 시 평지）")] public TerrainDefinition defaultTerrain;
        [InspectorName("칠한 지형")] public List<TerrainCell> terrainCells = new();
        [InspectorName("구조물 배치")] public List<StructurePlacement> structures = new();
        [InspectorName("유닛 배치")] public List<UnitPlacement> units = new();
        [InspectorName("시작 이벤트")] public EventSequence openingEvent;
        [InspectorName("진입 이벤트 구역")] public List<MapEventRegion> eventRegions = new();
        [InspectorName("승리 조건")] public MapVictoryCondition victoryCondition;
        [InspectorName("목표 칸")] public Vector2Int goalCell;

        public bool Contains(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;
        public TerrainDefinition TerrainAt(Vector2Int cell) => terrainCells.LastOrDefault(t => t != null && t.cell == cell)?.terrain ?? defaultTerrain;
        public StructurePlacement StructureAt(Vector2Int cell) => structures.FirstOrDefault(s => s != null && s.Cells().Contains(cell));
        public void Paint(Vector2Int cell, TerrainDefinition terrain)
        {
            if (!Contains(cell)) return;
            terrainCells.RemoveAll(t => t != null && t.cell == cell);
            if (terrain != null && terrain != defaultTerrain) terrainCells.Add(new TerrainCell { cell = cell, terrain = terrain });
        }
        public bool CanPlaceStructure(StructureDefinition definition, Vector2Int cell)
        {
            if (definition == null || definition.size.x < 1 || definition.size.y < 1) return false;
            return new StructurePlacement { cell = cell, structure = definition }.Cells().All(c => Contains(c)
                && StructureAt(c) == null && !units.Any(u => u != null && u.cell == c));
        }
        public BattleGrid CreateGrid()
        {
            var blocked = structures.Where(s => s?.structure != null && s.structure.blocksMovement).SelectMany(s => s.Cells());
            var grid = new BattleGrid(width, height, blocked);
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                var cell = new Vector2Int(x, y); var terrain = TerrainAt(cell);
                if (terrain != null) grid.SetTerrain(cell, terrain.walkable, terrain.movementCost, terrain.accuracyModifier, terrain.defenseModifier);
            }
            return grid;
        }
        public List<string> ValidateMap()
        {
            var errors = new List<string>();
            if (width < 1 || height < 1 || width > 64 || height > 64 || !float.IsFinite(cellSize) || cellSize < .1f)
            { errors.Add("맵 크기는 1~64칸, 칸 크기는 0.1 이상이어야 합니다."); return errors; }
            var grid = CreateGrid(); var occupied = new HashSet<Vector2Int>(); var ids = new HashSet<string>();
            foreach (var terrain in terrainCells.Where(t => t?.terrain != null).Select(t => t.terrain).Append(defaultTerrain).Where(t => t != null).Distinct())
                if (terrain.movementCost < 1) errors.Add($"지형 {terrain.displayName}: 이동 비용은 1 이상이어야 합니다.");
            foreach (var tile in terrainCells)
                if (tile == null || !Contains(tile.cell) || tile.terrain == null) errors.Add("칠한 지형에 비어 있거나 맵 밖인 항목이 있습니다.");
            if (terrainCells.Where(t => t != null).GroupBy(t => t.cell).Any(g => g.Count() > 1)) errors.Add("같은 칸에 지형 항목이 중복되어 있습니다.");
            foreach (var s in structures)
            {
                if (s?.structure == null) { errors.Add("구조물 데이터가 비어 있습니다."); continue; }
                if (s.structure.size.x < 1 || s.structure.size.y < 1 || s.structure.size.x > 64 || s.structure.size.y > 64)
                { errors.Add("구조물 크기는 1~64칸이어야 합니다."); continue; }
                foreach (var c in s.Cells())
                    if (!Contains(c) || !occupied.Add(c)) errors.Add($"구조물 {s.structure.displayName}: 범위 이탈 또는 겹침 {c}");
            }
            var unitCells = new HashSet<Vector2Int>();
            foreach (var u in units)
            {
                if (u?.character == null) { errors.Add("유닛 캐릭터가 비어 있습니다."); continue; }
                errors.AddRange(u.character.ValidateAssignment().Select(e=>$"유닛 {u.id}: {e}"));
                if (string.IsNullOrWhiteSpace(u.id) || !ids.Add(u.id)) errors.Add("유닛 ID가 비어 있거나 중복되었습니다.");
                if (!grid.IsWalkable(u.cell) || !unitCells.Add(u.cell) || occupied.Contains(u.cell)) errors.Add($"유닛 {u.id}: 이동 불가 칸 또는 겹친 배치입니다.");
            }
            if (!units.Any(u => u?.character != null && u.character.faction == Faction.Ally)) errors.Add("아군을 한 명 이상 배치하세요.");
            if (!units.Any(u => u?.character != null && u.character.faction == Faction.Enemy)) errors.Add("적군을 한 명 이상 배치하세요.");
            foreach (var r in eventRegions)
            {
                if (r == null || r.area.width < 1 || r.area.height < 1 || !Contains(r.area.min) || !Contains(r.area.max - Vector2Int.one))
                    errors.Add("이벤트 구역이 비어 있거나 맵 범위를 벗어납니다.");
                else if (r.sequence == null) errors.Add($"이벤트 구역 {r.label}: 이벤트를 지정하세요.");
                else errors.AddRange(r.sequence.ValidateCommands().Select(e => $"{r.label}: {e}"));
            }
            if (openingEvent != null) errors.AddRange(openingEvent.ValidateCommands().Select(e => "시작 이벤트: " + e));
            if (victoryCondition == MapVictoryCondition.ReachCell && !grid.IsWalkable(goalCell)) errors.Add("목표 칸은 이동 가능한 맵 안의 칸이어야 합니다.");
            return errors.Distinct().ToList();
        }
        public List<string> ReachabilityWarnings()
        {
            var warnings = new List<string>();
            if (width < 1 || height < 1 || width > 64 || height > 64) return warnings;
            var grid = CreateGrid(); var reached = new HashSet<Vector2Int>();
            foreach (var ally in units.Where(u => u?.character != null && u.character.faction == Faction.Ally))
                reached.UnionWith(grid.Reachable(ally.cell, int.MaxValue).Keys);
            if (victoryCondition == MapVictoryCondition.ReachCell && !reached.Contains(goalCell)) warnings.Add("어떤 아군 시작점에서도 목표 칸에 도달할 수 없습니다.");
            foreach (var enemy in units.Where(u => u?.character != null && u.character.faction == Faction.Enemy))
                if (!reached.Contains(enemy.cell)) warnings.Add($"적군 {enemy.id}의 칸에 아군이 걸어서 도달할 수 없습니다. 원거리 공격 가능 여부를 확인하세요.");
            foreach (var region in eventRegions.Where(r => r != null))
                if (!reached.Any(c => region.area.Contains(c))) warnings.Add($"이벤트 구역 {region.label}에 아군이 도달할 수 없습니다.");
            return warnings;
        }
    }
}
