using System.Collections.Generic;
using System.Linq;
using MercenariesOfMars.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MercenariesOfMars.Tests
{
    public sealed class MapTests
    {
        readonly List<Object> owned = new();
        T New<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); owned.Add(value); return value; }
        BattleMapDefinition Map()
        {
            var map = New<BattleMapDefinition>(); map.width = 5; map.height = 4;
            var ally = New<CharacterDefinition>(); ally.faction = Faction.Ally;
            var enemy = New<CharacterDefinition>(); enemy.faction = Faction.Enemy;
            var skill = New<SkillDefinition>(); skill.power = 10;
            ally.skills.Add(skill); enemy.skills.Add(skill);
            map.units.Add(new UnitPlacement { id = "아군", character = ally, cell = Vector2Int.zero });
            map.units.Add(new UnitPlacement { id = "적군", character = enemy, cell = new Vector2Int(4, 3) }); return map;
        }
        [TearDown] public void Cleanup() { foreach (var obj in owned) if (obj != null) Object.DestroyImmediate(obj); owned.Clear(); }
        [Test] public void 비용이_큰_직선보다_싼_우회로를_선택한다()
        {
            var grid = new BattleGrid(3, 2); grid.SetTerrain(Vector2Int.right, true, 5);
            var paths = grid.Reachable(Vector2Int.zero, 4);
            Assert.That(paths[new Vector2Int(2,0)], Is.EqualTo(new[] { Vector2Int.up, Vector2Int.one, new Vector2Int(2,1), new Vector2Int(2,0) }));
            Assert.That(paths.ContainsKey(Vector2Int.right), Is.False);
            Assert.That(grid.Reachable(Vector2Int.zero, 3).ContainsKey(new Vector2Int(2,0)), Is.False);
        }
        [Test] public void 출발칸_비용은_차감하지_않고_진입칸_비용을_차감한다()
        {
            var grid = new BattleGrid(3,1); grid.SetTerrain(Vector2Int.zero,true,99); grid.SetTerrain(Vector2Int.right,true,2);
            Assert.That(grid.Reachable(Vector2Int.zero,1).Count, Is.EqualTo(1));
            Assert.That(grid.Reachable(Vector2Int.zero,2).ContainsKey(Vector2Int.right), Is.True);
            Assert.That(grid.Reachable(Vector2Int.zero,2).ContainsKey(new Vector2Int(2,0)), Is.False);
        }
        [Test] public void 지형과_구조물_중_하나라도_막으면_이동할_수_없다()
        {
            var map = Map(); var sea = New<TerrainDefinition>(); sea.walkable = false;
            map.Paint(Vector2Int.up, sea);
            var wall = New<StructureDefinition>(); wall.size = new Vector2Int(2,1);
            map.structures.Add(new StructurePlacement { cell = Vector2Int.right, structure = wall });
            var grid = map.CreateGrid();
            Assert.That(grid.IsWalkable(Vector2Int.up), Is.False);
            Assert.That(grid.IsWalkable(new Vector2Int(2,0)), Is.False);
            Assert.That(grid.IsWalkable(Vector2Int.one), Is.True);
        }
        [Test] public void 구조물을_삭제해도_칠한_지형은_보존한다()
        {
            var map = Map(); var forest = New<TerrainDefinition>(); forest.movementCost = 2; map.Paint(Vector2Int.one,forest);
            var wall = New<StructureDefinition>(); map.structures.Add(new StructurePlacement { cell = Vector2Int.one, structure = wall });
            Assert.That(map.CreateGrid().IsWalkable(Vector2Int.one), Is.False);
            map.structures.Clear(); Assert.That(map.CreateGrid().MovementCost(Vector2Int.one), Is.EqualTo(2));
        }
        [Test] public void 지형_지우기는_기본지형으로_복귀하며_중복항목을_만들지_않는다()
        {
            var map = Map(); var forest = New<TerrainDefinition>(); var plain = New<TerrainDefinition>(); map.defaultTerrain = plain;
            map.Paint(Vector2Int.one,forest); map.Paint(Vector2Int.one,forest);
            Assert.That(map.terrainCells.Count, Is.EqualTo(1));
            map.Paint(Vector2Int.one,null); Assert.That(map.TerrainAt(Vector2Int.one), Is.SameAs(plain));
            Assert.That(map.terrainCells, Is.Empty);
        }
        [Test] public void 다칸구조물은_경계와_유닛과_다른구조물을_피해야_한다()
        {
            var map = Map(); var wall = New<StructureDefinition>(); wall.size = new Vector2Int(2,2);
            Assert.That(map.CanPlaceStructure(wall,new Vector2Int(4,0)), Is.False);
            Assert.That(map.CanPlaceStructure(wall,Vector2Int.zero), Is.False);
            Assert.That(map.CanPlaceStructure(wall,Vector2Int.one), Is.True);
            map.structures.Add(new StructurePlacement { cell = Vector2Int.one, structure = wall });
            Assert.That(map.CanPlaceStructure(wall,new Vector2Int(2,2)), Is.False);
            Assert.That(map.StructureAt(new Vector2Int(2,2)), Is.SameAs(map.structures[0]));
        }
        [Test] public void 정상맵은_오류없이_검증되고_중복ID와_겹침은_거절한다()
        {
            var map = Map(); Assert.That(map.ValidateMap(), Is.Empty);
            map.units[1].id = map.units[0].id; map.units[1].cell = map.units[0].cell;
            Assert.That(map.ValidateMap().Count, Is.EqualTo(2));
        }
        [Test] public void 맵밖_배치와_누락된_진입이벤트를_검출한다()
        {
            var map = Map(); map.units[1].cell = new Vector2Int(99,99);
            map.eventRegions.Add(new MapEventRegion());
            Assert.That(map.ValidateMap().Any(e => e.Contains("이벤트를 지정")), Is.True);
            Assert.That(map.ValidateMap().Any(e => e.Contains("유닛 적군")), Is.True);
        }
        [Test] public void 도달불가_목표와_적군은_경고로_구분한다()
        {
            var map = Map(); var sea = New<TerrainDefinition>(); sea.walkable = false;
            for (int y = 0; y < map.height; y++) map.Paint(new Vector2Int(2,y),sea);
            map.victoryCondition = MapVictoryCondition.ReachCell; map.goalCell = new Vector2Int(3,1);
            Assert.That(map.ValidateMap(), Is.Empty); Assert.That(map.ReachabilityWarnings().Count, Is.EqualTo(2));
        }
        [Test] public void 지형보정은_공격자_명중과_피격자_방어에_적용하고_반격시_역전한다()
        {
            var map = Map(); var forest = New<TerrainDefinition>(); forest.accuracyModifier = -15; forest.defenseModifier = 4;
            map.Paint(map.units[0].cell,forest);
            var units = map.units.Select(u => new BattleUnit(u.id,u.character,u.cell)).ToArray();
            var battle = new BattleSession(map.CreateGrid(),units);
            Assert.That(battle.GetHitChance(units[0],units[1],units[0].Skills[0]), Is.EqualTo(85));
            Assert.That(battle.GetHitChance(units[1],units[0],units[1].Skills[0]), Is.EqualTo(100));
            Assert.That(battle.GetDamage(units[1],units[0],units[1].Skills[0]), Is.EqualTo(13));
            Assert.That(battle.GetDamage(units[0],units[1],units[0].Skills[0]), Is.EqualTo(17));
            forest.defenseModifier = 999; forest.accuracyModifier = 50;
            Assert.That(battle.GetHitChance(units[0],units[1],units[0].Skills[0]), Is.EqualTo(85), "전투 데이터는 지형 에셋의 이후 편집으로 바뀌면 안 됩니다.");
        }
        [Test] public void 목표칸_도달은_승리이고_적전멸만으로_목표조건을_대신하지_않는다()
        {
            var map = Map(); var units = map.units.Select(u => new BattleUnit(u.id,u.character,u.cell)).ToArray();
            var battle = new BattleSession(map.CreateGrid(),units,victoryCell:Vector2Int.right);
            battle.ChangeHp(units[1].Id,-999);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Ongoing));
            Assert.That(battle.TryMove(units[0],Vector2Int.right), Is.True);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
        }
        [Test] public void 맵에_칠한지형은_실행취소와_다시실행을_지원한다()
        {
            var map = Map(); var terrain = New<TerrainDefinition>();
            Undo.RecordObject(map,"지형 칠하기 검증"); map.Paint(Vector2Int.one,terrain); Undo.FlushUndoRecordObjects();
            Undo.PerformUndo(); Assert.That(map.terrainCells, Is.Empty);
            Undo.PerformRedo(); Assert.That(map.TerrainAt(Vector2Int.one), Is.SameAs(terrain)); Undo.ClearUndo(map);
        }
        [Test] public void 맵에셋은_저장후_지형과_구조물과_유닛배치를_다시읽을_수_있다()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/맵저장검증.asset"); var map = Map();
            var terrain = New<TerrainDefinition>(); var wall = New<StructureDefinition>(); map.Paint(Vector2Int.one,terrain);
            map.structures.Add(new StructurePlacement { cell = new Vector2Int(2,2), structure = wall });
            try
            {
                AssetDatabase.CreateAsset(map,path);
                foreach (var data in owned.Where(o => o != map).ToArray()) AssetDatabase.AddObjectToAsset(data,map);
                EditorUtility.SetDirty(map); AssetDatabase.SaveAssetIfDirty(map);
                string json = EditorJsonUtility.ToJson(map);
                var copy = New<BattleMapDefinition>(); EditorJsonUtility.FromJsonOverwrite(json,copy);
                Assert.That(copy.TerrainAt(Vector2Int.one), Is.SameAs(terrain));
                Assert.That(copy.structures[0].cell, Is.EqualTo(new Vector2Int(2,2)));
                Assert.That(copy.units[1].id, Is.EqualTo("적군"));
                Assert.That(copy.ValidateMap(), Is.Empty);
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }
    }
}
