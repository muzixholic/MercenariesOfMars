using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MercenariesOfMars.PlayMode.Tests
{
    public sealed class MapIntegrationTests
    {
        readonly List<Object> owned = new();
        T New<T>() where T : ScriptableObject { var data = ScriptableObject.CreateInstance<T>(); owned.Add(data); return data; }
        BattleController Create()
        {
            var map = New<BattleMapDefinition>(); map.width = 5; map.height = 3; map.cellSize = 2;
            var forest = New<TerrainDefinition>(); forest.movementCost = 3; forest.defenseModifier = 5; map.Paint(Vector2Int.right,forest);
            var ally = New<CharacterDefinition>(); ally.faction = Faction.Ally; ally.movement = 2;
            var enemy = New<CharacterDefinition>(); enemy.faction = Faction.Enemy;
            map.units.Add(new UnitPlacement { id = "아군", character = ally, cell = Vector2Int.zero });
            map.units.Add(new UnitPlacement { id = "적군", character = enemy, cell = new Vector2Int(4,2) });
            var wall = New<StructureDefinition>(); wall.size = new Vector2Int(2,1);
            map.structures.Add(new StructurePlacement { cell = new Vector2Int(2,1), structure = wall });
            var root = new GameObject("맵 연결 검증"); root.SetActive(false); owned.Add(root);
            var board = new GameObject("보드").AddComponent<GridBoard>(); board.transform.SetParent(root.transform); board.map = map;
            var camera = new GameObject("카메라").AddComponent<Camera>(); camera.transform.SetParent(root.transform); camera.transform.position = new Vector3(0,10,-8); camera.transform.LookAt(new Vector3(5,0,3)); camera.orthographic = true;
            var battle = root.AddComponent<BattleController>(); battle.board = board; battle.battleCamera = camera;
            battle.automaticEnemy = false; battle.enableCombatPresentation = false; root.SetActive(true); return battle;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var item in owned) if (item != null) Object.Destroy(item); owned.Clear(); yield return null;
        }
        [UnityTest] public IEnumerator 맵데이터로_크기와_배치와_비용과_임시모델을_생성한다()
        {
            var c = Create(); yield return null;
            Assert.That(c.IsTitleScreen, Is.True); Assert.That(c.RestartBattle(), Is.True);
            Assert.That(c.board.width, Is.EqualTo(5)); Assert.That(c.board.cellSize, Is.EqualTo(2));
            Assert.That(c.Session.Units.Count, Is.EqualTo(2));
            Assert.That(c.Session.Grid.IsWalkable(new Vector2Int(3,1)), Is.False);
            Assert.That(c.Session.TryMove(c.Session.Find("아군"),Vector2Int.right), Is.False);
            Assert.That(c.board.GetComponentsInChildren<BattleMapView>().Length, Is.EqualTo(1));
            Assert.That(c.board.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(3));
            c.Session.ChangeHp("적군",-999); c.board.map.width = 6; c.board.map.structures.Clear();
            Assert.That(c.RestartBattle(), Is.True);
            Assert.That(c.board.GetComponentsInChildren<BattleMapView>().Length, Is.EqualTo(1));
            Assert.That(c.Session.Grid.Width, Is.EqualTo(6));
            Assert.That(c.Session.Grid.IsWalkable(new Vector2Int(3,1)), Is.True);
            Assert.That(c.board.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(2), "재시작 시 이전 구조물 모델도 제거해야 합니다.");
        }
        [UnityTest] public IEnumerator 아군_구역진입_이벤트는_한번만_실행하고_재시작하면_초기화한다()
        {
            var c = Create(); var sequence = New<EventSequence>();
            sequence.commands.Add(new EventCommand { kind = EventCommandKind.SetFlag, flag = "구역발동", flagValue = true });
            sequence.commands.Add(new EventCommand { kind = EventCommandKind.Stop });
            c.board.map.eventRegions.Add(new MapEventRegion { area = new RectInt(0,1,1,1), sequence = sequence });
            yield return null; Assert.That(c.RestartBattle(), Is.True);
            Assert.That(c.Session.TryMove(c.Session.Find("아군"),Vector2Int.up), Is.True);
            yield return null; yield return null;
            Assert.That(c.eventRunner.GetFlag("구역발동"), Is.True);
            c.eventRunner.ResetState(); yield return null;
            Assert.That(c.eventRunner.GetFlag("구역발동"), Is.False);
            c.Session.ChangeHp("적군",-999); Assert.That(c.RestartBattle(), Is.True);
            Assert.That(c.Session.TryMove(c.Session.Find("아군"),Vector2Int.up), Is.True);
            yield return null; yield return null;
            Assert.That(c.eventRunner.GetFlag("구역발동"), Is.True);
        }
        [UnityTest] public IEnumerator 목표칸에_이동하면_기존_승리화면이_표시된다()
        {
            var c = Create(); c.board.map.victoryCondition = MapVictoryCondition.ReachCell; c.board.map.goalCell = Vector2Int.up;
            yield return null; Assert.That(c.RestartBattle(), Is.True);
            Assert.That(c.Session.TryMove(c.Session.Find("아군"),Vector2Int.up), Is.True);
            Assert.That(c.IsVictory, Is.True); yield return null;
        }
    }
}
